// D3D11Renderer.cs - Direct3D 11 Video Frame Renderer
// GPU-accelerated video rendering pipeline for the Cine native player
//
// Supports two rendering paths:
//   1. BGRA-direct (default):  decoder outputs RGB32/BGRA → memcpy to back buffer
//   2. NV12→BGRA shader:       decoder outputs NV12     → pixel shader converts YUV to BGRA
//
// Lifecycle:
//   1. Construct with HWND (WinForms panel handle)
//   2. Initialize() creates D3D11 device, context, swap chain, RTV, shaders
//   3. Present(IMFSample) renders each decoded frame
//   4. ResizeBuffers() handles window resize events
//   5. Dispose() releases all COM objects in reverse order
//
// Design notes:
//   - We use C# COM interop interfaces (defined in MfComInterop.cs) as
//     vtable layouts. Each method maps 1:1 to the native COM vtable slot.
//   - Marshal.GetObjectForIUnknown wraps raw COM pointers as these
//     managed interface types so we can call methods directly.
//   - All COM lifetime is manual (Marshal.Release) to avoid RCW overhead
//     and ensure deterministic cleanup on the rendering thread.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Cine.Media.Implementations;

/// <summary>
/// Manages a Direct3D 11 device, DXGI swap chain, render target,
/// and frame presentation for the native Media Foundation pipeline.
/// </summary>
internal unsafe class D3D11Renderer : IDisposable
{
    #region Constants

    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D_DRIVER_TYPE_WARP = 3;
    private const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    private const uint DXGI_FORMAT_R8_UNORM = 61;
    private const uint DXGI_FORMAT_R8G8_UNORM = 62;
    private const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
    private const uint DXGI_SWAP_EFFECT_DISCARD = 0;
    public const uint D3D11_SDK_VERSION = 7;

    // D3D11 bind/usage constants
    private const uint D3D11_BIND_SHADER_RESOURCE = 0x8;
    private const uint D3D11_BIND_RENDER_TARGET = 0x4;
    private const uint D3D11_CPU_ACCESS_WRITE = 0x10000;
    private const uint D3D11_CPU_ACCESS_READ = 0x20000;
    private const uint D3D11_USAGE_DEFAULT = 0;
    private const uint D3D11_USAGE_DYNAMIC = 2;
    private const uint D3D11_USAGE_STAGING = 4;
    private const uint D3D11_SRV_DIMENSION_TEXTURE2D = 8;
    private const uint D3D11_APPEND_ALIGNED_ELEMENT = 0xFFFFFFFF;
    private const uint D3D11_MAP_WRITE_DISCARD = 0x4;
    private const uint D3D11_MAP_READ = 0x08;

    // Filter: MIN_MAG_MIP_LINEAR
    private const uint D3D11_FILTER_MIN_MAG_MIP_LINEAR = 0x15;
    // Address mode: CLAMP
    private const uint D3D11_TEXTURE_ADDRESS_CLAMP = 1;
    // Comparison: NEVER
    private const uint D3D11_COMPARISON_NEVER = 0;

    // Float comparison epsilon
    private const float FLOAT_EPSILON = 1e-6f;

    #endregion

    #region Fields

    // --- Core D3D11 ---
    private IntPtr _device;        // ID3D11Device
    private IntPtr _context;       // ID3D11DeviceContext
    private IntPtr _swapChain;     // IDXGISwapChain1
    private IntPtr _rtv;           // ID3D11RenderTargetView
    private IntPtr _backBuffer;    // ID3D11Texture2D (back buffer)

    // --- NV12 Shader Pipeline (Phase 3) ---
    private IntPtr _yDefaultTex;   // ID3D11Texture2D — Y plane (GPU default)
    private IntPtr _uvDefaultTex;  // ID3D11Texture2D — UV plane (GPU default)
    private IntPtr _yStagingTex;   // ID3D11Texture2D — Y plane staging (CPU write)
    private IntPtr _uvStagingTex;  // ID3D11Texture2D — UV plane staging (CPU write)
    private IntPtr _ySrv;          // ID3D11ShaderResourceView — Y SRV
    private IntPtr _uvSrv;         // ID3D11ShaderResourceView — UV SRV
    private IntPtr _vertexShader;  // ID3D11VertexShader
    private IntPtr _pixelShader;   // ID3D11PixelShader
    private IntPtr _inputLayout;   // ID3D11InputLayout
    private IntPtr _vertexBuffer;  // ID3D11Buffer — fullscreen quad VB
    private IntPtr _samplerState;  // ID3D11SamplerState — linear clamp
    private IntPtr _psBlob;        // ID3DBlob — pixel shader bytecode (needed for input layout)
    private IntPtr _vsBlob;        // ID3DBlob — vertex shader bytecode (needed for input layout)

    private int _nv12Width;        // video width (0 = not yet created)
    private int _nv12Height;       // video height (0 = not yet created)
    private bool _useShaderPath;   // true when decoder outputs NV12 (not BGRA)
    private readonly IntPtr _hwnd;
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>Width of the current swap chain back buffer.</summary>
    public int BackBufferWidth { get; private set; }

    /// <summary>Height of the current swap chain back buffer.</summary>
    public int BackBufferHeight { get; private set; }

    /// <summary>Whether the D3D11 device and swap chain are initialized.</summary>
    public bool IsInitialized => _device != IntPtr.Zero && _swapChain != IntPtr.Zero;

    /// <summary>
    /// When true, NV12 → BGRA conversion is done via pixel shader.
    /// Must be set BEFORE Initialize() is called.
    /// </summary>
    public bool UseNv12ShaderPath
    {
        get => _useShaderPath;
        set
        {
            if (IsInitialized)
                throw new InvalidOperationException("Cannot change shader path after Initialize().");
            _useShaderPath = value;
        }
    }

    #endregion

    /// <summary>
    /// Creates a renderer targeting the specified WinForms panel handle.
    /// Call <see cref="Initialize"/> before use.
    /// </summary>
    public D3D11Renderer(IntPtr hwnd)
    {
        _hwnd = hwnd != IntPtr.Zero
            ? hwnd
            : throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));
    }

    ~D3D11Renderer() => ReleaseUnmanaged();

    public void Dispose()
    {
        ReleaseUnmanaged();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates the D3D11 device, DXGI factory, swap chain, render target view,
    /// and (when UseNv12ShaderPath is true) the NV12→BGRA shader pipeline.
    /// </summary>
    public void Initialize()
    {
        // ── 1. Create D3D11 device + immediate context ──
        uint flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

        int hr = NativeMethods.D3D11CreateDevice(
            pAdapter: IntPtr.Zero,
            DriverType: (int)D3D_DRIVER_TYPE_HARDWARE,
            Software: IntPtr.Zero,
            Flags: flags,
            pFeatureLevels: IntPtr.Zero,
            FeatureLevels: 0,
            SDKVersion: D3D11_SDK_VERSION,
            ppDevice: out _device,
            pFeatureLevel: out _,
            ppImmediateContext: out _context);

        if (hr < 0)
        {
            _device = IntPtr.Zero;
            _context = IntPtr.Zero;

            hr = NativeMethods.D3D11CreateDevice(
                pAdapter: IntPtr.Zero,
                DriverType: (int)D3D_DRIVER_TYPE_WARP,
                Software: IntPtr.Zero,
                Flags: flags,
                pFeatureLevels: IntPtr.Zero,
                FeatureLevels: 0,
                SDKVersion: D3D11_SDK_VERSION,
                ppDevice: out _device,
                pFeatureLevel: out _,
                ppImmediateContext: out _context);

            Marshal.ThrowExceptionForHR(hr);
        }

        // ── 2. Create DXGI factory ──
        Guid factoryGuid = MfGuids.IID_IDXGIFactory2;
        IntPtr dxgiFactory = IntPtr.Zero;

        hr = NativeMethods.CreateDXGIFactory2(Flags: 0, riid: ref factoryGuid, ppFactory: out dxgiFactory);
        if (hr < 0)
        {
            hr = NativeMethods.CreateDXGIFactory1(ref factoryGuid, out dxgiFactory);
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            var desc = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = 0,
                Height = 0,
                Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                Stereo = 0,
                SampleDesc_Count = 1,
                SampleDesc_Quality = 0,
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling = 0,
                SwapEffect = DXGI_SWAP_EFFECT_DISCARD,
                AlphaMode = 0,
                Flags = 0
            };

            var factory = (IDXGIFactory2)Marshal.GetObjectForIUnknown(dxgiFactory);
            hr = factory.CreateSwapChainForHwnd(_device, _hwnd, ref desc, IntPtr.Zero, IntPtr.Zero, out _swapChain);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            SafeRelease(ref dxgiFactory);
        }

        // ── 3. Create render target view ──
        CreateRenderTarget();

        // ── 4. Compile shader pipeline (if NV12 mode) ──
        if (_useShaderPath)
            CreateNv12Pipeline();
    }

    /// <summary>Creates the back-buffer RTV and caches dimensions.</summary>
    private void CreateRenderTarget()
    {
        Guid texGuid = MfGuids.IID_ID3D11Texture2D;

        var swapChain = (IDXGISwapChain1)Marshal.GetObjectForIUnknown(_swapChain);
        Marshal.ThrowExceptionForHR(swapChain.GetBuffer(Buffer: 0, ref texGuid, out _backBuffer));

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateRenderTargetView(pResource: _backBuffer, pDesc: IntPtr.Zero, out _rtv));

        var dxgiDesc = new DXGI_SWAP_CHAIN_DESC1();
        if (swapChain.GetDesc(out dxgiDesc) >= 0)
        {
            BackBufferWidth = (int)dxgiDesc.Width;
            BackBufferHeight = (int)dxgiDesc.Height;
        }

        Marshal.ReleaseComObject(device);
        Marshal.ReleaseComObject(swapChain);
    }

    /// <summary>Releases the RTV and back-buffer texture.</summary>
    private void DestroyRenderTarget()
    {
        SafeRelease(ref _rtv);
        SafeRelease(ref _backBuffer);
    }

    #region NV12 Shader Pipeline

    /// <summary>
    /// Compiles inline HLSL shaders and creates the NV12→BGRA rendering pipeline.
    /// Called from Initialize() when UseNv12ShaderPath is true.
    /// </summary>
    private void CreateNv12Pipeline()
    {
        // ── 1. Compile vertex shader ──
        string vsSource = @"
struct VS_IN {
    float4 pos  : POSITION;
    float2 uv   : TEXCOORD0;
};
struct VS_OUT {
    float4 pos  : SV_POSITION;
    float2 uv   : TEXCOORD0;
};
VS_OUT main(VS_IN input) {
    VS_OUT o;
    o.pos = input.pos;
    o.uv  = input.uv;
    return o;
}";
        _vsBlob = CompileShader(vsSource, "vs_5_0", "main");

        // ── 2. Create vertex shader ──
        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateVertexShader(
            pShaderBytecode: _vsBlob,
            BytecodeLength: ((ID3DBlob)Marshal.GetObjectForIUnknown(_vsBlob)).GetBufferSize(),
            pClassLinkage: IntPtr.Zero,
            out _vertexShader));

        // ── 3. Create input layout ──
        var layout = new[]
        {
            new D3D11_INPUT_ELEMENT_DESC
            {
                SemanticName     = "POSITION",
                SemanticIndex    = 0,
                Format           = 2, // DXGI_FORMAT_R32G32B32A32_FLOAT
                InputSlot        = 0,
                AlignedByteOffset= 0,
                InputSlotClass   = 0, // D3D11_INPUT_PER_VERTEX_DATA
                InstanceDataStepRate = 0
            },
            new D3D11_INPUT_ELEMENT_DESC
            {
                SemanticName     = "TEXCOORD",
                SemanticIndex    = 0,
                Format           = 16, // DXGI_FORMAT_R32G32_FLOAT
                InputSlot        = 0,
                AlignedByteOffset= D3D11_APPEND_ALIGNED_ELEMENT,
                InputSlotClass   = 0,
                InstanceDataStepRate = 0
            }
        };

        fixed (D3D11_INPUT_ELEMENT_DESC* pLayout = layout)
        {
            Marshal.ThrowExceptionForHR(device.CreateInputLayout(
                (IntPtr)pLayout,
                (uint)layout.Length,
                _vsBlob,
                ((ID3DBlob)Marshal.GetObjectForIUnknown(_vsBlob)).GetBufferSize(),
                out _inputLayout));
        }

        // ── 4. Compile pixel shader ──
        string psSource = @"
Texture2D<float> YTex  : register(t0);
Texture2D<float> UVTex : register(t1);
SamplerState    Sampler : register(s0);

struct PS_IN {
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float4 main(PS_IN input) : SV_TARGET {
    float y  = YTex.Sample(Sampler, input.uv).r;
    float u  = UVTex.Sample(Sampler, input.uv).r - 0.5;
    float v  = UVTex.Sample(Sampler, input.uv).g - 0.5;

    float r = y + 1.402 * v;
    float g = y - 0.344 * u - 0.714 * v;
    float b = y + 1.772 * u;

    return float4(b, g, r, 1.0);
}";
        _psBlob = CompileShader(psSource, "ps_5_0", "main");

        // ── 5. Create pixel shader ──
        Marshal.ThrowExceptionForHR(device.CreatePixelShader(
            pShaderBytecode: _psBlob,
            BytecodeLength: ((ID3DBlob)Marshal.GetObjectForIUnknown(_psBlob)).GetBufferSize(),
            pClassLinkage: IntPtr.Zero,
            out _pixelShader));

        // ── 6. Create VS → PS input layout ──
        Marshal.ThrowExceptionForHR(device.CreateInputLayout(
            pInputElementDescs: (IntPtr)layout,
            NumElements: (uint)layout.Length,
            pShaderBytecodeWithInputSignature: _vsBlob,
            BytecodeLength: ((ID3DBlob)Marshal.GetObjectForIUnknown(_vsBlob)).GetBufferSize(),
            out _inputLayout));

        // ── 7. Create fullscreen quad vertex buffer ──
        var vertices = new[]
        {
            new Vertex { X = -1, Y = -1, Z = 0, W = 1, U = 0, V = 1 },
            new Vertex { X = -1, Y =  1, Z = 0, W = 1, U = 0, V = 0 },
            new Vertex { X =  1, Y = -1, Z = 0, W = 1, U = 1, V = 1 },
            new Vertex { X =  1, Y =  1, Z = 0, W = 1, U = 1, V = 0 },
        };

        int vbSize = 4 * sizeof(Vertex);
        _vertexBuffer = CreateBuffer(
            data: vertices,
            size: vbSize,
            bindFlags: 0,
            usage: D3D11_USAGE_DEFAULT);

        // ── 8. Create sampler state (linear clamp) ──
        CreateSamplerState();

        // ── 9. Create Y and UV default textures + staging textures ──
        // Y plane: full resolution, 8-bit single channel
        CreateTexture2D(_nv12Width, _nv12Height, DXGI_FORMAT_R8_UNORM,
            D3D11_USAGE_DEFAULT, D3D11_BIND_SHADER_RESOURCE, out _yDefaultTex);
        CreateTexture2D(_nv12Width, _nv12Height, DXGI_FORMAT_R8_UNORM,
            D3D11_USAGE_STAGING, 0, out _yStagingTex);

        // UV plane: half resolution (for NV12, U and V are interleaved at half height)
        int uvWidth = (_nv12Width + 1) / 2;
        int uvHeight = (_nv12Height + 1) / 2;
        CreateTexture2D(uvWidth, uvHeight, DXGI_FORMAT_R8G8_UNORM,
            D3D11_USAGE_DEFAULT, D3D11_BIND_SHADER_RESOURCE, out _uvDefaultTex);
        CreateTexture2D(uvWidth, uvHeight, DXGI_FORMAT_R8G8_UNORM,
            D3D11_USAGE_STAGING, 0, out _uvStagingTex);

        // ── 10. Create shader resource views ──
        CreateTextureSRV(_yDefaultTex, DXGI_FORMAT_R8_UNORM, out _ySrv);
        CreateTextureSRV(_uvDefaultTex, DXGI_FORMAT_R8G8_UNORM, out _uvSrv);

        Marshal.ReleaseComObject(device);
    }

    /// <summary>Creates a vertex buffer from system-memory data.</summary>
    private IntPtr CreateBuffer(Array data, int size, uint bindFlags, uint usage)
    {
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var desc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)size,
                Usage = usage,
                BindFlags = bindFlags,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0
            };

            var subResource = new D3D11_SUBRESOURCE_DATA
            {
                pSysMem = handle.AddrOfPinnedObject(),
                SysMemPitch = 0,
                SysMemSlicePitch = 0
            };

            var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
            Marshal.ThrowExceptionForHR(device.CreateBuffer(ref desc, ref subResource, out IntPtr buffer));
            Marshal.ReleaseComObject(device);
            return buffer;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Creates a sampler state with linear filtering and clamp addressing.</summary>
    private void CreateSamplerState()
    {
        var desc = new D3D11_SAMPLER_DESC
        {
            Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressV = D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressW = D3D11_TEXTURE_ADDRESS_CLAMP,
            MipLODBias = 0,
            MaxAnisotropy = 1,
            ComparisonFunc = D3D11_COMPARISON_NEVER,
            BorderColor = new float[4] { 0, 0, 0, 0 },
            MinLOD = 0,
            MaxLOD = float.MaxValue
        };

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateSamplerState(ref desc, out _samplerState));
        Marshal.ReleaseComObject(device);
    }

    /// <summary>Creates a shader resource view for a 2D texture.</summary>
    private void CreateTextureSRV(IntPtr texture, uint format, out IntPtr srv)
    {
        var desc = new D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            Format = format,
            ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D,
            MostDetailedMip = 0,
            MipLevels = 1
        };

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateShaderResourceView(texture, ref desc, out srv));
        Marshal.ReleaseComObject(device);
    }

    /// <summary>Compiles an HLSL shader from source.</summary>
    private IntPtr CompileShader(string source, string target, string entryPoint)
    {
        int hr = NativeMethods.D3DCompile(
            pSrcData: source,
            SrcDataSize: (nuint)source.Length,
            pSourceName: null,
            pDefines: IntPtr.Zero,
            pInclude: IntPtr.Zero,
            pEntrypoint: entryPoint,
            pTarget: target,
            Flags1: 0,
            Flags2: 0,
            out IntPtr blob,
            out IntPtr errMsgs);

        Marshal.ThrowExceptionForHR(hr);
        return blob;
    }

    /// <summary>Releases all NV12 shader pipeline COM objects.</summary>
    private void DestroyNv12Textures()
    {
        SafeRelease(ref _yDefaultTex);
        SafeRelease(ref _uvDefaultTex);
        SafeRelease(ref _yStagingTex);
        SafeRelease(ref _uvStagingTex);
        SafeRelease(ref _ySrv);
        SafeRelease(ref _uvSrv);
        SafeRelease(ref _vertexShader);
        SafeRelease(ref _pixelShader);
        SafeRelease(ref _inputLayout);
        SafeRelease(ref _vertexBuffer);
        SafeRelease(ref _samplerState);
        SafeRelease(ref _psBlob);
        SafeRelease(ref _vsBlob);
    }

    #endregion

    /// <summary>
    /// Resizes the swap chain buffers and recreates the render target view.
    /// Called from the UI when the panel is resized.
    /// </summary>
    public void ResizeBuffers(int width, int height)
    {
        if (!IsInitialized || width <= 0 || height <= 0) return;

        DestroyRenderTarget();

        var swapChain = (IDXGISwapChain1)Marshal.GetObjectForIUnknown(_swapChain);
        Marshal.ThrowExceptionForHR(swapChain.ResizeBuffers(
            BufferCount: 2,
            Width: (uint)width,
            Height: (uint)height,
            NewFormat: DXGI_FORMAT_B8G8R8A8_UNORM,
            SwapChainFlags: 0));

        Marshal.ReleaseComObject(swapChain);
        CreateRenderTarget();

        // Update NV12 textures if in shader mode
        if (_useShaderPath)
        {
            DestroyNv12Textures();
            _nv12Width = width;
            _nv12Height = height;
            CreateNv12Pipeline();
        }
    }

    /// <summary>
    /// Presents a decoded video sample to the screen.
    /// Automatically selects the appropriate rendering path.
    /// </summary>
    public void Present(IMFSample? sample)
    {
        if (sample == null || _rtv == IntPtr.Zero || _context == IntPtr.Zero)
            return;

        if (_useShaderPath)
        {
            PresentNv12(sample);
            return;
        }

        // ── BGRA-direct path ──
        int hr = sample.ConvertToContiguousBuffer(out IMFMediaBuffer? buffer);
        if (hr < 0 || buffer == null) return;

        try
        {
            hr = buffer.Lock(out IntPtr srcPtr, out _, out uint srcLen);
            if (hr < 0) return;

            try
            {
                var mapped = new MappedSubresource();
                var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

                hr = context.Map(_backBuffer, Subresource: 0,
                    MapType: (uint)D3D11MapMode.Write, MapFlags: 0, out mapped);
                if (hr < 0) return;

                try
                {
                    uint srcPitch = (uint)BackBufferWidth * 4;
                    uint dstPitch = mapped.RowPitch;
                    uint rows = (uint)BackBufferHeight;

                    if (srcPitch == dstPitch)
                    {
                        Buffer.MemoryCopy(
                            (void*)srcPtr, (void*)mapped.pData,
                            srcPitch * rows, srcPitch * rows);
                    }
                    else
                    {
                        byte* dst = (byte*)mapped.pData;
                        byte* src = (byte*)srcPtr;
                        uint copyBytes = srcPitch < dstPitch ? srcPitch : dstPitch;

                        for (uint y = 0; y < rows; y++)
                        {
                            Buffer.MemoryCopy(
                                src, dst,
                                copyBytes, copyBytes);

                            src += srcPitch;
                            dst += dstPitch;
                        }
                    }
                }
                finally
                {
                    context.Unmap(_backBuffer, Subresource: 0);
                }

                var swapChain = (IDXGISwapChain1)Marshal.GetObjectForIUnknown(_swapChain);
                swapChain.Present(SyncInterval: 1, Flags: 0);
                Marshal.ReleaseComObject(swapChain);
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            if (buffer != null) Marshal.ReleaseComObject(buffer);
        }
    }

    /// <summary>
    /// Captures the current swap chain back buffer and saves it to a PNG file.
    /// </summary>
    /// <param name="outputPath">Full path to the output PNG file.</param>
    /// <returns>True if the screenshot was saved successfully.</returns>
    public bool TakeScreenshot(string outputPath)
    {
        if (!IsInitialized || _backBuffer == IntPtr.Zero || string.IsNullOrEmpty(outputPath))
            return false;

        try
        {
            int width = BackBufferWidth;
            int height = BackBufferHeight;

            if (width <= 0 || height <= 0)
                return false;

            // Create a staging texture that is CPU-readable
            IntPtr stagingTex = IntPtr.Zero;
            CreateTexture2D(width, height, DXGI_FORMAT_B8G8R8A8_UNORM,
                D3D11_USAGE_STAGING, 0, // No bind flags for staging
                out stagingTex);

            if (stagingTex == IntPtr.Zero)
                return false;

            var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

            try
            {
                // Copy the back buffer to the staging texture
                context.CopyResource(stagingTex, _backBuffer);

                // Map the staging texture to read the pixel data
                var mapped = new MappedSubresource();
                int hr = context.Map(
                    stagingTex,
                    Subresource: 0,
                    MapType: (uint)D3D11MapMode.Read,
                    MapFlags: 0,
                    out mapped);

                if (hr < 0)
                    return false;

                try
                {
                    // Create a Bitmap and save as PNG
                    using (var bitmap = new Bitmap(
                        width,
                        height,
                        PixelFormat.Format32bppArgb))
                    {
                        var bmpData = bitmap.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppArgb);

                        try
                        {
                            // Copy the D3D11 data (BGRA) to the bitmap
                            // Both D3D11 back buffer and Format32bppArgb are in BGRA order
                            byte* src = (byte*)mapped.pData;
                            byte* dst = (byte*)bmpData.Scan0;
                            int srcStride = (int)mapped.RowPitch;
                            int dstStride = bmpData.Stride;
                            int copyBytes = Math.Min(srcStride, dstStride);

                            for (int y = 0; y < height; y++)
                            {
                                Buffer.MemoryCopy(
                                    src, dst,
                                    (uint)copyBytes, (uint)copyBytes);

                                src += srcStride;
                                dst += dstStride;
                            }
                        }
                        finally
                        {
                            bitmap.UnlockBits(bmpData);
                        }

                        // Save as PNG
                        bitmap.Save(outputPath, ImageFormat.Png);
                    }

                    return true;
                }
                finally
                {
                    context.Unmap(stagingTex, Subresource: 0);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(context);
                SafeRelease(ref stagingTex);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Clears the render target to opaque black and presents a blank frame.</summary>
    public void ClearToBlack()
    {
        if (_rtv == IntPtr.Zero || _context == IntPtr.Zero) return;

        var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

        float[] black = new float[4] { 0f, 0f, 0f, 1f };
        context.ClearRenderTargetView(_rtv, black);

        var swapChain = (IDXGISwapChain1)Marshal.GetObjectForIUnknown(_swapChain);
        swapChain.Present(SyncInterval: 1, Flags: 0);

        Marshal.ReleaseComObject(context);
        Marshal.ReleaseComObject(swapChain);
    }

    #region Helpers

    /// <summary>Releases a COM pointer and zeros the field.</summary>
    private static void SafeRelease(ref IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.Release(ptr);
            ptr = IntPtr.Zero;
        }
    }

    /// <summary>Releases all COM objects in reverse creation order.</summary>
    private void ReleaseUnmanaged()
    {
        if (_disposed) return;

        DestroyRenderTarget();
        DestroyNv12Textures();
        SafeRelease(ref _swapChain);
        SafeRelease(ref _context);
        SafeRelease(ref _device);

        _disposed = true;
    }

    #endregion
}

/// <summary>Vertex structure for the fullscreen quad.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Vertex
{
    public float X, Y, Z, W;
    public float U, V;
}
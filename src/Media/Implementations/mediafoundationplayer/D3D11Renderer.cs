// D3D11Renderer.cs - Direct3D 11 Video Frame Renderer
// GPU-accelerated video rendering pipeline for the Simba native player
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

namespace Simba.Media.Implementations;

/// <summary>
/// Manages a Direct3D 11 GPU device, DXGI swap chain, render target,
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
    private const uint DXGI_FORMAT_R8G8_UNORM = 49;
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
    private const uint D3D11_USAGE_STAGING = 3;
    private const uint D3D11_SRV_DIMENSION_TEXTURE2D = 8;
    private const uint D3D11_APPEND_ALIGNED_ELEMENT = 0xFFFFFFFF;
    private const uint D3D11_MAP_WRITE_DISCARD = 4;
    private const uint D3D11_MAP_READ = 1;
    private const uint D3D11_MAP_WRITE = 2;

    // Filter: MIN_MAG_MIP_LINEAR
    private const uint D3D11_FILTER_MIN_MAG_MIP_LINEAR = 0x15;
    private const uint D3D11_FILTER_MIN_MAG_MIP_POINT = 0x0;
    // Address mode: CLAMP
    private const uint D3D11_TEXTURE_ADDRESS_CLAMP = 1;
    // Comparison: NEVER
    private const uint D3D11_COMPARISON_NEVER = 0;
    // Primitive topology: TRIANGLESTRIP
    private const uint D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP = 5;

    // Float comparison epsilon
    private const float FLOAT_EPSILON = 1e-6f;

    #endregion

    #region Fields

    // --- Core D3D11 ---
    private IntPtr _device;        // ID3D11Device
    private IntPtr _context;       // ID3D11DeviceContext
    private uint _featureLevel;    // D3D_FEATURE_LEVEL (as uint)
    private IntPtr _swapChain;     // IDXGISwapChain
    private IntPtr _rtv;           // ID3D11RenderTargetView
    private IntPtr _backBuffer;    // ID3D11Texture2D (back buffer)

    // --- NV12 Shader Pipeline (Phase 3) ---
    private IntPtr _bgraStagingTex; // ID3D11Texture2D — BGRA staging (CPU write)
    private IntPtr _bgraDefaultTex; // ID3D11Texture2D — BGRA default (GPU)
    private IntPtr _yDefaultTex;   // ID3D11Texture2D — Y plane (GPU default)
    private IntPtr _uvDefaultTex;  // ID3D11Texture2D — UV plane (GPU default)
    private IntPtr _yStagingTex;   // ID3D11Texture2D — Y plane staging (CPU write)
    private IntPtr _uvStagingTex;  // ID3D11Texture2D — UV plane staging (CPU write)
    private IntPtr _bgraSrv;       // ID3D11ShaderResourceView — BGRA SRV
    private IntPtr _ySrv;          // ID3D11ShaderResourceView — Y SRV
    private IntPtr _uvSrv;         // ID3D11ShaderResourceView — UV SRV
    private IntPtr _vertexShader;  // ID3D11VertexShader
    private IntPtr _pixelShader;   // ID3D11PixelShader
    private IntPtr _bgraPixelShader; // ID3D11PixelShader — BGRA path
    private IntPtr _inputLayout;   // ID3D11InputLayout
    private IntPtr _vertexBuffer;  // ID3D11Buffer — fullscreen quad VB
    private IntPtr _samplerState;  // ID3D11SamplerState — linear clamp
    private IntPtr _bgraSamplerState;
    private IntPtr _psBlob;        // ID3DBlob — pixel shader bytecode (needed for input layout)
    private IntPtr _vsBlob;        // ID3DBlob — vertex shader bytecode (needed for input layout)

    // --- Post-process filter pipeline (Phase 4) ---
    private IntPtr _filterPixelShader;     // ID3D11PixelShader — filter pass
    private IntPtr _filterPsBlob;          // ID3DBlob — filter shader bytecode
    private IntPtr _filterRenderTarget;    // ID3D11RenderTargetView — intermediate RT
    private IntPtr _filterTexture;         // ID3D11Texture2D — intermediate texture
    private IntPtr _filterSRV;             // ID3D11ShaderResourceView — input to filter
    private IntPtr _filterSamplerState;    // ID3D11SamplerState — linear clamp for filter

    private int _videoWidth;       // video width (0 = not yet created)
    private int _videoHeight;      // video height (0 = not yet created)
    private bool _useShaderPath;   // true when decoder outputs NV12 (not BGRA)
    private readonly IntPtr _hwnd;
    private bool _disposed;

    // --- Filter parameters ---
    private float _brightness;  // [-1.0, 1.0], default 0.0
    private float _contrast;    // [0.0, 3.0], default 1.0
    private float _gamma;       // [0.1, 3.0], default 1.0
    private float _saturation;  // [0.0, 3.0], default 1.0
    private float _hue;         // [-180, 180] in degrees, default 0.0

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

    /// <summary>Brightness adjustment [-1.0, 1.0], default 0.0.</summary>
    public float Brightness
    {
        get => _brightness;
        set { _brightness = Math.Clamp(value, -1.0f, 1.0f); }
    }

    /// <summary>Contrast adjustment [0.0, 3.0], default 1.0.</summary>
    public float Contrast
    {
        get => _contrast;
        set { _contrast = Math.Clamp(value, 0.0f, 3.0f); }
    }

    /// <summary>Gamma adjustment [0.1, 3.0], default 1.0.</summary>
    public float Gamma
    {
        get => _gamma;
        set { _gamma = Math.Clamp(value, 0.1f, 3.0f); }
    }

    /// <summary>Saturation adjustment [0.0, 3.0], default 1.0.</summary>
    public float Saturation
    {
        get => _saturation;
        set { _saturation = Math.Clamp(value, 0.0f, 3.0f); }
    }

    /// <summary>Hue adjustment [-180, 180] degrees, default 0.0.</summary>
    public float Hue
    {
        get => _hue;
        set { _hue = Math.Clamp(value, -180.0f, 180.0f); }
    }

    /// <summary>True when any video filter is active.</summary>
    public bool HasActiveFilters =>
        Math.Abs(_brightness) > 1e-5f ||
        Math.Abs(_contrast - 1.0f) > 1e-5f ||
        Math.Abs(_gamma - 1.0f) > 1e-5f ||
        Math.Abs(_saturation - 1.0f) > 1e-5f ||
        Math.Abs(_hue) > 1e-5f;

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

        _brightness = 0f;
        _contrast = 1f;
        _gamma = 1f;
        _saturation = 1f;
        _hue = 0f;
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
        uint flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

        var swapDesc = new DXGI_SWAP_CHAIN_DESC
        {
            BufferDesc = new DXGI_MODE_DESC
            {
                Width = 0,
                Height = 0,
                RefreshRate_Numerator = 0,
                RefreshRate_Denominator = 0,
                Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                ScanlineOrdering = 0,
                Scaling = 0
            },
            SampleDesc_Count = 1,
            SampleDesc_Quality = 0,
            BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
            BufferCount = 2,
            OutputWindow = _hwnd,
            Windowed = 1,
            SwapEffect = DXGI_SWAP_EFFECT_DISCARD,
            Flags = 0
        };

        int createdFeatureLevel;
        int hr = NativeMethods.D3D11CreateDeviceAndSwapChain(
            pAdapter: IntPtr.Zero,
            DriverType: (int)D3D_DRIVER_TYPE_HARDWARE,
            Software: IntPtr.Zero,
            Flags: flags,
            pFeatureLevels: IntPtr.Zero,
            FeatureLevels: 0,
            SDKVersion: D3D11_SDK_VERSION,
            pSwapChainDesc: ref swapDesc,
            ppSwapChain: out _swapChain,
            ppDevice: out _device,
            pFeatureLevel: out createdFeatureLevel,
            ppImmediateContext: out _context);

        if (hr < 0)
        {
            _swapChain = IntPtr.Zero;
            _device = IntPtr.Zero;
            _context = IntPtr.Zero;

            hr = NativeMethods.D3D11CreateDeviceAndSwapChain(
                pAdapter: IntPtr.Zero,
                DriverType: (int)D3D_DRIVER_TYPE_WARP,
                Software: IntPtr.Zero,
                Flags: flags,
                pFeatureLevels: IntPtr.Zero,
                FeatureLevels: 0,
                SDKVersion: D3D11_SDK_VERSION,
                pSwapChainDesc: ref swapDesc,
                ppSwapChain: out _swapChain,
                ppDevice: out _device,
                pFeatureLevel: out createdFeatureLevel,
                ppImmediateContext: out _context);
        }

        Marshal.ThrowExceptionForHR(hr);
        _featureLevel = (uint)createdFeatureLevel;
        if (_useShaderPath && _featureLevel < 0xA000)
            _useShaderPath = false;

        // ── 3. Create render target view ──
        CreateRenderTarget();

        // ── 4. Create Common Resources (VS, Layout, VB, Sampler) ──
        CreateCommonResources();

        // ── 5. Create BGRA staging texture ──
        if (!_useShaderPath && _videoWidth > 0 && _videoHeight > 0)
        {
            CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_B8G8R8A8_UNORM, 0, out _bgraStagingTex);
            CreateBgraPipeline();
        }

        // ── 6. Compile shader pipeline (if NV12 mode) ──
        if (_useShaderPath)
        {
            try
            {
                CreateNv12Pipeline();
            }
            catch
            {
                _useShaderPath = false;
                DestroyNv12Textures();
                EnsureBgraResources();
            }
        }

        // ── 7. Compile post-process filter pipeline (if filters active) ──
        if (HasActiveFilters)
            CreateFilterPipeline();
    }

    /// <summary>Creates the back-buffer RTV and caches dimensions.</summary>
    private void CreateRenderTarget()
    {
        Guid texGuid = MfGuids.IID_ID3D11Texture2D;

        var swapChain = (IDXGISwapChain)Marshal.GetObjectForIUnknown(_swapChain);
        Marshal.ThrowExceptionForHR(swapChain.GetBuffer(Buffer: 0, ref texGuid, out _backBuffer));

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateRenderTargetView(pResource: _backBuffer, pDesc: IntPtr.Zero, out _rtv));

        if (swapChain.GetDesc(out var dxgiDesc) >= 0)
        {
            BackBufferWidth = (int)dxgiDesc.BufferDesc.Width;
            BackBufferHeight = (int)dxgiDesc.BufferDesc.Height;
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

    private void CreateCommonResources()
    {
        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);

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
        _vsBlob = CompileShader(vsSource, GetVsTarget(), "main");

        // ── 2. Create vertex shader ──
        Marshal.ThrowExceptionForHR(device.CreateVertexShader(
            pShaderBytecode: GetBlobPointer(_vsBlob),
            BytecodeLength: GetBlobSize(_vsBlob),
            pClassLinkage: IntPtr.Zero,
            out _vertexShader));

        // ── 3. Create input layout ──
        IntPtr posName = Marshal.StringToHGlobalAnsi("POSITION");
        IntPtr texName = Marshal.StringToHGlobalAnsi("TEXCOORD");

        try
        {
            var layout = new[]
            {
                new D3D11_INPUT_ELEMENT_DESC
                {
                    SemanticName     = posName,
                    SemanticIndex    = 0,
                    Format           = 2, // DXGI_FORMAT_R32G32B32A32_FLOAT
                    InputSlot        = 0,
                    AlignedByteOffset= 0,
                    InputSlotClass   = 0, // D3D11_INPUT_PER_VERTEX_DATA
                    InstanceDataStepRate = 0
                },
                new D3D11_INPUT_ELEMENT_DESC
                {
                    SemanticName     = texName,
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
                    GetBlobPointer(_vsBlob),
                    GetBlobSize(_vsBlob),
                    out _inputLayout));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(posName);
            Marshal.FreeHGlobal(texName);
        }

        // ── 4. Create fullscreen quad vertex buffer ──
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

        // ── 5. Create sampler state (linear clamp) ──
        CreateSamplerState();

        Marshal.ReleaseComObject(device);
    }

    /// <summary>
    /// Compiles inline HLSL shaders and creates the NV12→BGRA rendering pipeline.
    /// Called from Initialize() when UseNv12ShaderPath is true.
    /// </summary>
    private void CreateNv12Pipeline()
    {
        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);

        // ── 4. Compile pixel shader ──
        string psSource = @"
Texture2D     YTex  : register(t0);
Texture2D     UVTex : register(t1);
SamplerState  Sampler : register(s0);

struct PS_IN {
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float4 main(PS_IN input) : SV_TARGET {
    float y  = YTex.Sample(Sampler, input.uv).r;
    float2 uv = UVTex.Sample(Sampler, input.uv).rg - float2(0.5, 0.5);
    float u = uv.x;
    float v = uv.y;

    float r = y + 1.402 * v;
    float g = y - 0.344 * u - 0.714 * v;
    float b = y + 1.772 * u;

    return float4(b, g, r, 1.0);
}";
        _psBlob = CompileShader(psSource, GetPsTarget(), "main");

        // ── 5. Create pixel shader ──
        Marshal.ThrowExceptionForHR(device.CreatePixelShader(
            pShaderBytecode: GetBlobPointer(_psBlob),
            BytecodeLength: GetBlobSize(_psBlob),
            pClassLinkage: IntPtr.Zero,
            out _pixelShader));



        // ── 9. Create Y and UV default textures + staging textures ──
        // Y plane: full resolution, 8-bit single channel
        CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_R8_UNORM,
            D3D11_BIND_SHADER_RESOURCE, out _yDefaultTex);
        CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_R8_UNORM,
            0, out _yStagingTex);

        // UV plane: half resolution (for NV12, U and V are interleaved at half height)
        int uvWidth = (_videoWidth + 1) / 2;
        int uvHeight = (_videoHeight + 1) / 2;
        CreateTexture2D(uvWidth, uvHeight, DXGI_FORMAT_R8G8_UNORM,
            D3D11_BIND_SHADER_RESOURCE, out _uvDefaultTex);
        CreateTexture2D(uvWidth, uvHeight, DXGI_FORMAT_R8G8_UNORM,
            0, out _uvStagingTex);

        // ── 10. Create shader resource views ──
        CreateTextureSRV(_yDefaultTex, DXGI_FORMAT_R8_UNORM, out _ySrv);
        CreateTextureSRV(_uvDefaultTex, DXGI_FORMAT_R8G8_UNORM, out _uvSrv);

        Marshal.ReleaseComObject(device);
    }

    // ======================
    //  POST-PROCESS FILTER PIPELINE
    // ======================

    /// <summary>
    /// Creates the post-process filter pipeline: intermediate texture, RTV, SRV,
    /// constant buffer, and pixel shader for brightness/contrast/gamma/saturation/hue.
    /// </summary>
    private void CreateFilterPipeline()
    {
        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);

        try
        {
            // 1. Create intermediate render target texture (same size as back buffer)
            var texDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)BackBufferWidth,
                Height = (uint)BackBufferHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc_Count = 1,
                SampleDesc_Quality = 0,
                Usage = D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };

            Marshal.ThrowExceptionForHR(device.CreateTexture2D(ref texDesc, IntPtr.Zero, out _filterTexture));

            // 2. Create render target view for the intermediate texture
            Marshal.ThrowExceptionForHR(device.CreateRenderTargetView(_filterTexture, IntPtr.Zero, out _filterRenderTarget));

            // 3. Create shader resource view for sampling the intermediate texture
            Marshal.ThrowExceptionForHR(device.CreateShaderResourceView(_filterTexture, IntPtr.Zero, out _filterSRV));

            // 4. Create a second sampler state for the filter pass
            var sampDesc = new D3D11_SAMPLER_DESC
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
            Marshal.ThrowExceptionForHR(device.CreateSamplerState(ref sampDesc, out _filterSamplerState));

            // 5. Compile the filter pixel shader
            string filterPsSource = @"
cbuffer FilterParams : register(b0)
{
    float Brightness;
    float Contrast;
    float Gamma;
    float Saturation;
    float Hue;
    float3 Pad;
};

Texture2D InputTex : register(t0);
SamplerState Sampler : register(s0);

struct PS_IN {
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float3 RGBtoHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
}

float3 HSVtoRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

float4 main(PS_IN input) : SV_TARGET
{
    float4 color = InputTex.Sample(Sampler, input.uv);
    color.rgb += Brightness;
    color.rgb = lerp(float3(0.5, 0.5, 0.5), color.rgb, Contrast);
    color.rgb = pow(max(color.rgb, 0.0), 1.0 / max(Gamma, 0.01));
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    color.rgb = lerp(float3(gray, gray, gray), color.rgb, Saturation);
    if (abs(Hue) > 1e-5)
    {
        float3 hsv = RGBtoHSV(color.rgb);
        float hueRad = radians(Hue);
        hsv.x = frac(hsv.x + hueRad / (2.0 * 3.14159265));
        color.rgb = HSVtoRGB(hsv);
    }
    color.a = 1.0;
    return color;
}
";
            IntPtr psBlob = CompileShader(filterPsSource, GetPsTarget(), "main");

            Marshal.ThrowExceptionForHR(device.CreatePixelShader(
                pShaderBytecode: GetBlobPointer(psBlob),
                BytecodeLength: GetBlobSize(psBlob),
                pClassLinkage: IntPtr.Zero,
                out _filterPixelShader));
            _filterPsBlob = psBlob;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    /// <summary>
    /// Applies the post-process filter by rendering a fullscreen quad through
    /// the filter shader, reading from the intermediate texture and writing to the back buffer.
    /// </summary>
    private unsafe void ApplyFilter()
    {
        if (!HasActiveFilters || _filterPixelShader == IntPtr.Zero || _filterSRV == IntPtr.Zero)
            return;

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

        try
        {
            // 1. Copy current back buffer to filter intermediate texture
            context.CopyResource(_filterTexture, _backBuffer);

            // 2. Set back buffer as render target
            IntPtr[] rts = new IntPtr[] { _rtv };
            fixed (IntPtr* pRts = rts)
            {
                context.OMSetRenderTargets(1, (IntPtr)pRts, IntPtr.Zero);
            }

            // 3. Set up filter shader resources and sampler
            IntPtr[] srvs = new IntPtr[] { _filterSRV };
            fixed (IntPtr* pSrvs = srvs)
            {
                context.PSSetShaderResources(0, 1, (IntPtr)pSrvs);
            }

            IntPtr[] samplers = new IntPtr[] { _filterSamplerState };
            fixed (IntPtr* pSamplers = samplers)
            {
                context.PSSetSamplers(0, 1, (IntPtr)pSamplers);
            }

            // 4. Create/update constant buffer with filter parameters
            int paramSize = 8 * sizeof(float);
            GCHandle handle = GCHandle.Alloc(new float[8]
            {
                _brightness,
                _contrast,
                _gamma,
                _saturation,
                _hue,
                0f, 0f, 0f
            }, GCHandleType.Pinned);
            try
            {
                var bufDesc = new D3D11_BUFFER_DESC
                {
                    ByteWidth = (uint)paramSize,
                    Usage = D3D11_USAGE_DYNAMIC,
                    BindFlags = 0x4, // D3D11_BIND_CONSTANT_BUFFER
                    CPUAccessFlags = 0x10000, // D3D11_CPU_ACCESS_WRITE
                    MiscFlags = 0,
                    StructureByteStride = 0
                };

                IntPtr cbBuffer;
                Marshal.ThrowExceptionForHR(device.CreateBuffer(ref bufDesc, IntPtr.Zero, out cbBuffer));

                try
                {
                    var mapped = new MappedSubresource();
                    Marshal.ThrowExceptionForHR(context.Map(cbBuffer, 0, 0x2, 0, out mapped));
                    try
                    {
                        byte* pSrc = (byte*)handle.AddrOfPinnedObject().ToPointer();
                        byte* pDst = (byte*)mapped.pData.ToPointer();
                        for (int i = 0; i < paramSize; i++)
                            pDst[i] = pSrc[i];
                    }
                    finally
                    {
                        context.Unmap(cbBuffer, 0);
                    }

                    IntPtr[] cbs = new IntPtr[] { cbBuffer };
                    fixed (IntPtr* pCbs = cbs)
                    {
                        context.PSSetConstantBuffers(0, 1, (IntPtr)pCbs);
                    }
                }
                finally
                {
                    SafeRelease(ref cbBuffer);
                }
            }
            finally
            {
                handle.Free();
            }

            // 5. Draw fullscreen quad
            uint[] strideArr = new uint[] { (uint)sizeof(Vertex) };
            uint[] offsetArr = new uint[] { 0 };
            IntPtr[] vbs = new IntPtr[] { _vertexBuffer };
            fixed (uint* pStride = strideArr)
            fixed (uint* pOffset = offsetArr)
            fixed (IntPtr* pVbs = vbs)
            {
                context.IASetVertexBuffers(0, 1, (IntPtr)pVbs,
                    (IntPtr)pStride, (IntPtr)pOffset);
            }

            context.IASetInputLayout(_inputLayout);
            context.IASetPrimitiveTopology(0x5); // TRIANGLESTRIP
            context.VSSetShader(_vertexShader, IntPtr.Zero, 0);
            context.PSSetShader(_filterPixelShader, IntPtr.Zero, 0);

            var viewport = new D3D11_VIEWPORT
            {
                TopLeftX = 0,
                TopLeftY = 0,
                Width = (float)BackBufferWidth,
                Height = (float)BackBufferHeight,
                MinDepth = 0,
                MaxDepth = 1
            };
            context.RSSetViewports(1, new IntPtr(&viewport));

            context.Draw(4, 0);

            Marshal.ReleaseComObject(context);
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    /// <summary>Creates a texture with the specified parameters.</summary>
    private void CreateTexture2D(int width, int height, uint format, uint bindFlags, out IntPtr texture)
    {
        var desc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDesc_Count = 1,
            SampleDesc_Quality = 0,
            Usage = bindFlags == 0 ? D3D11_USAGE_STAGING : D3D11_USAGE_DEFAULT,
            BindFlags = bindFlags,
            CPUAccessFlags = bindFlags == 0 ? D3D11_CPU_ACCESS_WRITE : 0,
            MiscFlags = 0
        };

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateTexture2D(ref desc, IntPtr.Zero, out texture));
        Marshal.ReleaseComObject(device);
    }

    private void CreateReadbackTexture2D(int width, int height, uint format, out IntPtr texture)
    {
        var desc = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDesc_Count = 1,
            SampleDesc_Quality = 0,
            Usage = D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11_CPU_ACCESS_READ,
            MiscFlags = 0
        };

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateTexture2D(ref desc, IntPtr.Zero, out texture));
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

            var init = new D3D11_SUBRESOURCE_DATA
            {
                pSysMem = handle.AddrOfPinnedObject(),
                SysMemPitch = 0,
                SysMemSlicePitch = 0
            };

            var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
            IntPtr buffer;
            unsafe
            {
                D3D11_SUBRESOURCE_DATA* pInit = &init;
                Marshal.ThrowExceptionForHR(device.CreateBuffer(ref desc, (IntPtr)pInit, out buffer));
            }
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
        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);
        Marshal.ThrowExceptionForHR(device.CreateShaderResourceView(texture, IntPtr.Zero, out srv));
        Marshal.ReleaseComObject(device);
    }

    /// <summary>Compiles an HLSL shader from source.</summary>
    private IntPtr CompileShader(string source, string target, string entryPoint)
    {
        IntPtr pSource = Marshal.StringToHGlobalAnsi(source);
        try
        {
            int hr = NativeMethods.D3DCompile(
                pSrcData: pSource,
                SrcDataSize: (nuint)source.Length,
                pSourceName: IntPtr.Zero,
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
        finally
        {
            Marshal.FreeHGlobal(pSource);
        }
    }

    private string GetVsTarget()
    {
        if (_featureLevel >= 0xB000) return "vs_5_0";
        if (_featureLevel >= 0xA000) return "vs_4_0";
        if (_featureLevel >= 0x9300) return "vs_4_0_level_9_3";
        return "vs_4_0_level_9_1";
    }

    private string GetPsTarget()
    {
        if (_featureLevel >= 0xB000) return "ps_5_0";
        if (_featureLevel >= 0xA000) return "ps_4_0";
        if (_featureLevel >= 0x9300) return "ps_4_0_level_9_3";
        return "ps_4_0_level_9_1";
    }

    /// <summary>Releases all NV12 shader pipeline COM objects.</summary>
    private void DestroyNv12Textures()
    {
        SafeRelease(ref _bgraStagingTex);
        SafeRelease(ref _bgraDefaultTex);
        SafeRelease(ref _bgraSrv);
        SafeRelease(ref _yDefaultTex);
        SafeRelease(ref _uvDefaultTex);
        SafeRelease(ref _yStagingTex);
        SafeRelease(ref _uvStagingTex);
        SafeRelease(ref _ySrv);
        SafeRelease(ref _uvSrv);
        SafeRelease(ref _vertexShader);
        SafeRelease(ref _pixelShader);
        SafeRelease(ref _bgraPixelShader);
        SafeRelease(ref _inputLayout);
        SafeRelease(ref _vertexBuffer);
        SafeRelease(ref _samplerState);
        SafeRelease(ref _bgraSamplerState);
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

        var swapChain = (IDXGISwapChain)Marshal.GetObjectForIUnknown(_swapChain);
        Marshal.ThrowExceptionForHR(swapChain.ResizeBuffers(
            BufferCount: 2,
            Width: (uint)width,
            Height: (uint)height,
            NewFormat: DXGI_FORMAT_B8G8R8A8_UNORM,
            SwapChainFlags: 0));

        Marshal.ReleaseComObject(swapChain);
        CreateRenderTarget();

        if (_useShaderPath)
        {
            DestroyNv12Textures();
            _videoWidth = width;
            _videoHeight = height;
            try
            {
                CreateNv12Pipeline();
            }
            catch
            {
                _useShaderPath = false;
                DestroyNv12Textures();
                EnsureBgraResources();
            }
        }
        else if (_videoWidth > 0 && _videoHeight > 0)
        {
            SafeRelease(ref _bgraStagingTex);
            CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_B8G8R8A8_UNORM, 0, out _bgraStagingTex);
            CreateBgraPipeline();
        }
    }

    public void SetVideoDimensions(int width, int height)
    {
        if (_videoWidth == width && _videoHeight == height) return;
        _videoWidth = width;
        _videoHeight = height;
        if (IsInitialized)
        {
            if (_useShaderPath)
            {
                DestroyNv12Textures();
                try
                {
                    CreateNv12Pipeline();
                }
                catch
                {
                    _useShaderPath = false;
                    DestroyNv12Textures();
                    EnsureBgraResources();
                }
            }
            else
            {
                SafeRelease(ref _bgraStagingTex);
                CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_B8G8R8A8_UNORM, 0, out _bgraStagingTex);
                CreateBgraPipeline();
            }
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

        if (_videoWidth <= 0 || _videoHeight <= 0)
            return;

        int hr = sample.ConvertToContiguousBuffer(out IMFMediaBuffer? buffer);
        if (hr < 0 || buffer == null) return;

        try
        {
            hr = buffer.Lock(out IntPtr srcPtr, out _, out uint srcLen);
            if (hr < 0) return;

            try
            {
                ulong requiredBgra = (ulong)_videoWidth * (ulong)_videoHeight * 4UL;
                ulong requiredNv12 = (ulong)_videoWidth * (ulong)_videoHeight * 3UL / 2UL;

                if ((ulong)srcLen >= requiredBgra)
                {
                    EnsureBgraResources();
                    if (_bgraStagingTex == IntPtr.Zero) return;

                    var mapped = new MappedSubresource();
                    var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

                    try
                    {
                        hr = context.Map(_bgraStagingTex, Subresource: 0,
                            MapType: D3D11_MAP_WRITE, MapFlags: 0, out mapped);
                        if (hr < 0) return;

                        try
                        {
                            uint srcPitch = (uint)_videoWidth * 4;
                            if (_videoHeight > 0)
                            {
                                uint candidate = srcLen / (uint)_videoHeight;
                                if (candidate >= srcPitch && (candidate % 4) == 0 && (srcLen % (uint)_videoHeight) == 0)
                                    srcPitch = candidate;
                            }
                            uint dstPitch = mapped.RowPitch;
                            uint rows = (uint)_videoHeight;

                            byte* dst = (byte*)mapped.pData;
                            byte* src = (byte*)srcPtr;
                            uint copyBytes = Math.Min(srcPitch, dstPitch);

                            for (uint y = 0; y < rows; y++)
                            {
                                Buffer.MemoryCopy(src, dst, copyBytes, copyBytes);
                                src += srcPitch;
                                dst += dstPitch;
                            }
                        }
                        finally
                        {
                            context.Unmap(_bgraStagingTex, Subresource: 0);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(context);
                    }

                    RenderQuad(_bgraStagingTex);
                    ApplyFilter();
                    PresentSwapChain();
                    return;
                }

                if ((ulong)srcLen >= requiredNv12)
                {
                    EnsureBgraResources();
                    if (_bgraStagingTex == IntPtr.Zero) return;

                    PresentNv12Cpu(srcPtr, srcLen);
                    return;
                }
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

    private void PresentSwapChain()
    {
        var swapChain = (IDXGISwapChain)Marshal.GetObjectForIUnknown(_swapChain);
        swapChain.Present(SyncInterval: 1, Flags: 0);
        Marshal.ReleaseComObject(swapChain);
    }

    private void EnsureBgraResources()
    {
        if (_videoWidth <= 0 || _videoHeight <= 0) return;

        if (_bgraStagingTex == IntPtr.Zero)
            CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_B8G8R8A8_UNORM, 0, out _bgraStagingTex);

        if (_bgraPixelShader == IntPtr.Zero || _bgraSrv == IntPtr.Zero || _bgraDefaultTex == IntPtr.Zero)
            CreateBgraPipeline();
    }

    private void PresentNv12Cpu(IntPtr srcPtr, uint srcLen)
    {
        ulong required = (ulong)_videoWidth * (ulong)_videoHeight * 3UL / 2UL;
        if ((ulong)srcLen < required) return;

        var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);
        try
        {
            var mapped = new MappedSubresource();
            int hr = context.Map(_bgraStagingTex, Subresource: 0,
                MapType: D3D11_MAP_WRITE, MapFlags: 0, out mapped);
            if (hr < 0) return;

            try
            {
                int w = _videoWidth;
                int h = _videoHeight;

                byte* yPlane = (byte*)srcPtr;
                byte* uvPlane = (byte*)srcPtr + (uint)(w * h);
                byte* dstRow = (byte*)mapped.pData;

                for (int y = 0; y < h; y++)
                {
                    byte* yRow = yPlane + (uint)(y * w);
                    byte* uvRow = uvPlane + (uint)((y >> 1) * w);
                    byte* dst = dstRow;

                    for (int x = 0; x < w; x++)
                    {
                        int yy = yRow[x];
                        int uvIndex = (x & ~1);
                        int u = uvRow[uvIndex] - 128;
                        int v = uvRow[uvIndex + 1] - 128;

                        int c = yy - 16;
                        if (c < 0) c = 0;

                        int r = (298 * c + 409 * v + 128) >> 8;
                        int g = (298 * c - 100 * u - 208 * v + 128) >> 8;
                        int b = (298 * c + 516 * u + 128) >> 8;

                        if ((uint)r > 255) r = r < 0 ? 0 : 255;
                        if ((uint)g > 255) g = g < 0 ? 0 : 255;
                        if ((uint)b > 255) b = b < 0 ? 0 : 255;

                        dst[0] = (byte)b;
                        dst[1] = (byte)g;
                        dst[2] = (byte)r;
                        dst[3] = 255;
                        dst += 4;
                    }

                    dstRow += mapped.RowPitch;
                }
            }
            finally
            {
                context.Unmap(_bgraStagingTex, Subresource: 0);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(context);
        }

        RenderQuad(_bgraStagingTex);
        ApplyFilter();
        PresentSwapChain();
    }

    private void RenderQuad(IntPtr texture)
    {
        if (_bgraPixelShader == IntPtr.Zero || _bgraSrv == IntPtr.Zero || _bgraDefaultTex == IntPtr.Zero) return;

        var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

        // 0. Copy from staging to default
        context.CopyResource(_bgraDefaultTex, texture);

        // 1. Set render target
        IntPtr[] rts = new IntPtr[] { _rtv };
        fixed (IntPtr* pRts = rts)
        {
            context.OMSetRenderTargets(1, (IntPtr)pRts, IntPtr.Zero);
        }

        context.ClearRenderTargetView(_rtv, new float[4] { 0, 0, 0, 1 });

        // 2. Set up shader resources and sampler
        IntPtr[] srvs = new IntPtr[] { _bgraSrv };
        fixed (IntPtr* pSrvs = srvs)
        {
            context.PSSetShaderResources(0, 1, (IntPtr)pSrvs);
        }

        IntPtr[] samplers = new IntPtr[] { _bgraSamplerState != IntPtr.Zero ? _bgraSamplerState : _samplerState };
        fixed (IntPtr* pSamplers = samplers)
        {
            context.PSSetSamplers(0, 1, (IntPtr)pSamplers);
        }

        // 3. Draw fullscreen quad
        uint[] strideArr = new uint[] { (uint)sizeof(Vertex) };
        uint[] offsetArr = new uint[] { 0 };
        IntPtr[] vbs = new IntPtr[] { _vertexBuffer };
        fixed (uint* pStride = strideArr)
        fixed (uint* pOffset = offsetArr)
        fixed (IntPtr* pVbs = vbs)
        {
            context.IASetVertexBuffers(0, 1, (IntPtr)pVbs,
                (IntPtr)pStride, (IntPtr)pOffset);
        }

        context.IASetInputLayout(_inputLayout);
        context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
        context.VSSetShader(_vertexShader, IntPtr.Zero, 0);
        context.PSSetShader(_bgraPixelShader, IntPtr.Zero, 0);

        // Calculate letterboxing/pillarboxing
        float videoAspect = (float)_videoWidth / _videoHeight;
        float windowAspect = (float)BackBufferWidth / BackBufferHeight;
        
        float drawW, drawH;
        if (videoAspect > windowAspect)
        {
            drawW = BackBufferWidth;
            drawH = BackBufferWidth / videoAspect;
        }
        else
        {
            drawH = BackBufferHeight;
            drawW = BackBufferHeight * videoAspect;
        }
        
        float x = (BackBufferWidth - drawW) / 2;
        float y = (BackBufferHeight - drawH) / 2;

        var viewport = new D3D11_VIEWPORT
        {
            TopLeftX = x,
            TopLeftY = y,
            Width = drawW,
            Height = drawH,
            MinDepth = 0,
            MaxDepth = 1
        };
        context.RSSetViewports(1, new IntPtr(&viewport));

        context.Draw(4, 0);

        Marshal.ReleaseComObject(context);
    }

    private void CreateBgraPipeline()
    {
        if (_device == IntPtr.Zero || _videoWidth <= 0 || _videoHeight <= 0) return;

        var device = (ID3D11Device)Marshal.GetObjectForIUnknown(_device);

        // 1. Create BGRA Default Texture
        SafeRelease(ref _bgraDefaultTex);
        CreateTexture2D(_videoWidth, _videoHeight, DXGI_FORMAT_B8G8R8A8_UNORM, D3D11_BIND_SHADER_RESOURCE, out _bgraDefaultTex);
        
        // 2. Create SRV
        SafeRelease(ref _bgraSrv);
        CreateTextureSRV(_bgraDefaultTex, DXGI_FORMAT_B8G8R8A8_UNORM, out _bgraSrv);

        // 3. Create simple BGRA pixel shader
        string psSource = @"
Texture2D    InputTex : register(t0);
SamplerState Sampler  : register(s0);

struct PS_IN {
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float catmullRom(float x)
{
    x = abs(x);
    if (x < 1.0) return 1.5 * x * x * x - 2.5 * x * x + 1.0;
    if (x < 2.0) return -0.5 * x * x * x + 2.5 * x * x - 4.0 * x + 2.0;
    return 0.0;
}

float4 sampleBicubic(Texture2D tex, SamplerState samp, float2 uv)
{
    uint w, h;
    tex.GetDimensions(w, h);
    float2 texSize = float2((float)w, (float)h);
    float2 pos = uv * texSize - 0.5;
    float2 base = floor(pos);
    float2 f = pos - base;

    float4 sum = 0;
    float wsum = 0;

    [unroll] for (int j = -1; j <= 2; j++)
    {
        [unroll] for (int i = -1; i <= 2; i++)
        {
            float wx = catmullRom((float)i - f.x);
            float wy = catmullRom((float)j - f.y);
            float wxy = wx * wy;
            float2 p = (base + float2((float)i, (float)j) + 0.5) / texSize;
            sum += tex.SampleLevel(samp, p, 0) * wxy;
            wsum += wxy;
        }
    }

    return sum / max(wsum, 1e-6);
}

float4 main(PS_IN input) : SV_TARGET {
    float4 c = sampleBicubic(InputTex, Sampler, input.uv);
    c.a = 1.0;
    return c;
}";
        _psBlob = CompileShader(psSource, GetPsTarget(), "main");
        
        Marshal.ThrowExceptionForHR(device.CreatePixelShader(
            pShaderBytecode: GetBlobPointer(_psBlob),
            BytecodeLength: GetBlobSize(_psBlob),
            pClassLinkage: IntPtr.Zero,
            out _bgraPixelShader));

        if (_bgraSamplerState == IntPtr.Zero)
        {
            var desc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER_MIN_MAG_MIP_POINT,
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
            Marshal.ThrowExceptionForHR(device.CreateSamplerState(ref desc, out _bgraSamplerState));
        }

        Marshal.ReleaseComObject(device);
    }

    /// <summary>
    /// Presents an NV12 sample using the shader pipeline.
    /// </summary>
    private void PresentNv12(IMFSample sample)
    {
        int hr = sample.ConvertToContiguousBuffer(out IMFMediaBuffer? buffer);
        if (hr < 0 || buffer == null) return;

        try
        {
            hr = buffer.Lock(out IntPtr srcPtr, out _, out uint srcLen);
            if (hr < 0) return;

            try
            {
                if (_videoWidth <= 0 || _videoHeight <= 0)
                    return;

                if (_yStagingTex == IntPtr.Zero || _uvStagingTex == IntPtr.Zero || _yDefaultTex == IntPtr.Zero || _uvDefaultTex == IntPtr.Zero)
                    return;

                ulong required = (ulong)_videoWidth * (ulong)_videoHeight;
                required += required / 2UL;
                if ((ulong)srcLen < required)
                    return;

                var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

                var mapped = new MappedSubresource();

                // Map Y staging texture
                hr = context.Map(_yStagingTex, Subresource: 0,
                    MapType: D3D11_MAP_WRITE, MapFlags: 0, out mapped);
                if (hr < 0) return;

                try
                {
                    int yWidth = _videoWidth;
                    int yHeight = _videoHeight;
                    uint srcPitch = (uint)yWidth;

                    byte* dst = (byte*)mapped.pData;
                    byte* src = (byte*)srcPtr;

                    for (int y = 0; y < yHeight; y++)
                    {
                        Buffer.MemoryCopy(src, dst, (uint)yWidth, (uint)yWidth);
                        src += srcPitch;
                        dst += mapped.RowPitch;
                    }
                }
                finally
                {
                    context.Unmap(_yStagingTex, Subresource: 0);
                }

                // Map UV staging texture
                hr = context.Map(_uvStagingTex, Subresource: 0,
                    MapType: D3D11_MAP_WRITE, MapFlags: 0, out mapped);
                if (hr < 0) return;

                try
                {
                    int uvWidth = (_videoWidth + 1) / 2;
                    int uvHeight = (_videoHeight + 1) / 2;
                    uint srcPitch = (uint)_videoWidth;

                    byte* dst = (byte*)mapped.pData;
                    byte* src = (byte*)srcPtr + (uint)(_videoWidth * _videoHeight);

                    for (int y = 0; y < uvHeight; y++)
                    {
                        Buffer.MemoryCopy(src, dst, (uint)(uvWidth * 2), (uint)(uvWidth * 2));
                        src += srcPitch;
                        dst += mapped.RowPitch;
                    }
                }
                finally
                {
                    context.Unmap(_uvStagingTex, Subresource: 0);
                }

                context.CopyResource(_yDefaultTex, _yStagingTex);
                context.CopyResource(_uvDefaultTex, _uvStagingTex);

                uint[] strides = new uint[] { (uint)sizeof(Vertex) };
                uint[] offsets = new uint[] { 0 };

                // Create a temp pointer to pass the stride and offset arrays
                // Create array for vertex buffers
                IntPtr[] vertexBuffers = new IntPtr[] { _vertexBuffer };
                fixed (uint* pStride = strides, pOffset = offsets)
                fixed (IntPtr* pVertexBuffers = vertexBuffers)
                {
                    context.IASetVertexBuffers(0, 1, (IntPtr)pVertexBuffers,
                        (IntPtr)pStride, (IntPtr)pOffset);
                }

                context.IASetInputLayout(_inputLayout);
                context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);

                context.VSSetShader(_vertexShader, IntPtr.Zero, 0);
                context.PSSetShader(_pixelShader, IntPtr.Zero, 0);
                
                // Create arrays for shader resources
                IntPtr[] shaderResources = new IntPtr[] { _ySrv, _uvSrv };
                fixed (IntPtr* pShaderResources = shaderResources)
                {
                    context.PSSetShaderResources(0, 2, (IntPtr)pShaderResources);
                }
                
                // Create array for sampler state
                IntPtr[] samplers = new IntPtr[] { _samplerState };
                fixed (IntPtr* pSamplers = samplers)
                {
                    context.PSSetSamplers(0, 1, (IntPtr)pSamplers);
                }

                // Create array for render targets
                IntPtr[] renderTargets = new IntPtr[] { _rtv };
                fixed (IntPtr* pRenderTargets = renderTargets)
                {
                    context.OMSetRenderTargets(1, (IntPtr)pRenderTargets, IntPtr.Zero);
                }

                var viewport = new D3D11_VIEWPORT
                {
                    TopLeftX = 0,
                    TopLeftY = 0,
                    Width = (float)BackBufferWidth,
                    Height = (float)BackBufferHeight,
                    MinDepth = 0,
                    MaxDepth = 1
                };
                
                // Create array for viewports
                D3D11_VIEWPORT[] viewports = new D3D11_VIEWPORT[] { viewport };
                fixed (D3D11_VIEWPORT* pViewports = viewports)
                {
                    context.RSSetViewports(1, (IntPtr)pViewports);
                }

                context.ClearRenderTargetView(_rtv, new float[4] { 0, 0, 0, 1 });
                context.Draw(4, 0);

                var swapChain = (IDXGISwapChain)Marshal.GetObjectForIUnknown(_swapChain);
                swapChain.Present(SyncInterval: 1, Flags: 0);
                Marshal.ReleaseComObject(swapChain);

                Marshal.ReleaseComObject(context);
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

            IntPtr stagingTex = IntPtr.Zero;
            CreateReadbackTexture2D(width, height, DXGI_FORMAT_B8G8R8A8_UNORM, out stagingTex);

            if (stagingTex == IntPtr.Zero)
                return false;

            var context = (ID3D11DeviceContext)Marshal.GetObjectForIUnknown(_context);

            try
            {
                context.CopyResource(stagingTex, _backBuffer);

                var mapped = new MappedSubresource();
                int hr = context.Map(stagingTex, Subresource: 0,
                    MapType: D3D11_MAP_READ, MapFlags: 0, out mapped);

                if (hr < 0)
                    return false;

                try
                {
                    using (var bitmap = new Bitmap(
                        width, height,
                        PixelFormat.Format32bppArgb))
                    {
                        var bmpData = bitmap.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppArgb);

                        try
                        {
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

        var swapChain = (IDXGISwapChain)Marshal.GetObjectForIUnknown(_swapChain);
        swapChain.Present(SyncInterval: 1, Flags: 0);

        Marshal.ReleaseComObject(context);
        Marshal.ReleaseComObject(swapChain);
    }

    #region Helpers

    /// <summary>Helper to get pointer from blob.</summary>
    private static IntPtr GetBlobPointer(IntPtr blob)
    {
        if (blob == IntPtr.Zero) return IntPtr.Zero;

        var b = (ID3DBlob)Marshal.GetTypedObjectForIUnknown(blob, typeof(ID3DBlob));
        try { return b.GetBufferPointer(); }
        finally { Marshal.ReleaseComObject(b); }
    }

    /// <summary>Helper to get size from blob.</summary>
    private static nuint GetBlobSize(IntPtr blob)
    {
        if (blob == IntPtr.Zero) return 0;

        var b = (ID3DBlob)Marshal.GetTypedObjectForIUnknown(blob, typeof(ID3DBlob));
        try { return b.GetBufferSize(); }
        finally { Marshal.ReleaseComObject(b); }
    }

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

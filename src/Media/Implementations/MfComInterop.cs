// MFComInterop.cs - Media Foundation COM interop interfaces and P/Invoke
// Maps to Windows Media Foundation SDK: mfapi.h, mfidl.h, mfobjects.h, mfuuid.lib
// Native replacement for WPF MediaElement (Stage 2)

using System;
using System.Runtime.InteropServices;

namespace Cine.Media.Implementations;

#region COM GUIDs

internal static class MfGuids
{
    // CLSIDs
    public static readonly Guid CLSID_MFSourceReader     = new Guid("5B6DAB93-2486-4A0E-BC92-6AD4AC96AAFC");
    public static readonly Guid CLSID_MFMediaSession     = new Guid("4A457EEA-E0A2-4E6B-BE08-7F9C4B578F97");
    public static readonly Guid CLSID_MFMediaSource      = new Guid("6A963014-1CDC-4DF3-8D4C-9DFB68183E11");

    // IIDs
    public static readonly Guid IID_IMFSourceReader        = new Guid("DEEC8D99-FA1D-4D82-84C2-2A5DE92B126C");
    public static readonly Guid IID_IMFMediaSession        = new Guid("927E4E0C-E3A5-4658-B56B-19D4E9ED3E34");
    public static readonly Guid IID_IMFMediaEventGenerator = new Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF");
    public static readonly Guid IID_IMFAttributes          = new Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF");
    public static readonly Guid IID_IMFActivate            = new Guid("58722D43-7ABB-4EE5-8F1B-685B7A4E4DE9");
    public static readonly Guid IID_IMFTransform           = new Guid("BF94C121-5B05-4662-AFDA-7A4E4DC0B9E2");
    public static readonly Guid IID_ID3D11Device           = new Guid("DB6F6D46-D8FA-4285-A54B-AD0260FED5EC");
    public static readonly Guid IID_ID3D11DeviceContext    = new Guid("BB2C6FAA-B5FB-4082-876F-E879428B7647");
    public static readonly Guid IID_ID3D11Texture2D        = new Guid("091EF1C0-D2C2-4BA9-ACBE-213990D74582");
    public static readonly Guid IID_IDXGIDevice            = new Guid("54EC77FA-1377-44E6-9867-B87D78477247");
    public static readonly Guid IID_IDXGIFactory2          = new Guid("50C83A1C-E072-4C48-87B0-3630FA36A6D0");
    public static readonly Guid IID_IDXGISwapChain1        = new Guid("790A45F7-0DDF-4782-A291-9974D5898B71");
    public static readonly Guid IID_IMFSourceReaderCallback = new Guid("DEEC8D99-FA1D-4D82-84C2-2A5DE92B126D");
    public static readonly Guid IID_IMFMediaType           = new Guid("C49A51E4-BDBF-4EF9-8DA6-DE805C58FB4F");
    public static readonly Guid IID_IMFSample              = new Guid("C40A00F2-B93A-4D80-AE83-10A4943828AA");
    public static readonly Guid IID_IMFMediaBuffer         = new Guid("35FE3BBE-2EE0-4393-8BC6-E78C77FDB003");
    public static readonly Guid IID_IMFMediaEvent          = new Guid("DF598931-F10C-4E71-86AB-34BE8F8F8CE9");
    public static readonly Guid IID_IMFVideoDisplayControl = new Guid("C10BA4D5-A2FA-4C9D-84D6-0C1E3DBC4DB6");

    // MF format (subtype) GUIDs
    public static readonly Guid MFVideoFormat_NV12  = new Guid("3231564E-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_I420  = new Guid("30323449-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_YUY2  = new Guid("32595559-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFAudioFormat_PCM    = new Guid("00000001-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFAudioFormat_Float  = new Guid("00000003-0000-0010-8000-00AA00389B71");

    // WASAPI
    public static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid IID_IMMDeviceEnumerator  = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

    // Media type major type GUIDs.
    // MF uses GUIDs where Data1 holds the ASCII FOURCC in little-endian:
    //   'vids' = 0x73646976   'auds' = 0x73647561
    public static readonly Guid MFMediaType_VIDEO = new Guid(0x73646976, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);
    public static readonly Guid MFMediaType_AUDIO = new Guid(0x73647561, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

    // Attribute / format key GUIDs
    public static readonly Guid MF_MT_MAJOR_TYPE            = new Guid("49E6A19A-9C55-4D4A-AF6C-711FB20A655D");
    public static readonly Guid MF_MT_SUBTYPE             = new Guid("F7E34C9F-41B8-4B7D-A165-538A87FCB55B");
    public static readonly Guid MF_MT_FRAME_SIZE          = new Guid("1652C33D-D6B2-4012-B834-72030D6E9F73");
    public static readonly Guid MF_MT_FRAME_RATE          = new Guid("C459A2E8-2711-42E0-8214-FB946F4B151C");
    public static readonly Guid MF_MT_PIXEL_ASPECT_RATIO  = new Guid("3AB58A87-AFD7-4A8E-8F73-D3D216B6B580");

    // Audio-specific MF attribute GUIDs (Phase 2)
    public static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE      = new Guid("F2DEE55C-916C-4E51-B0D0-C1E0E7D618B1");
    public static readonly Guid MF_MT_AUDIO_NUM_CHANNELS         = new Guid("2595B5D0-FA3A-4EA5-86A8-FDF5DA6C19D4");
    public static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND   = new Guid("4D5B4FA1-62A2-4B59-9B9D-2B7A0E6E4608");
    public static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT      = new Guid("36584CC5-04FB-42CE-8C1E-5D4D7C5AC6D4");
    public static readonly Guid MF_MT_ALL_SAMPLES_INDEPENDENT    = new Guid("E2724B62-7B9C-4f0E-B5EA-6B6AC4E33BFA");
    public static readonly Guid MF_MT_AUDIO_SUBTYPE              = new Guid("E562344C-0A6C-4B96-8D61-1E1FBCDA5917");

    // Presentation descriptor GUIDs (Phase 2: duration + seeking)
    public static readonly Guid MF_PD_START_TIME                 = new Guid("C831E049-6B64-4CAB-875A-741BA7047C0C");
    public static readonly Guid MF_PD_DURATION                   = new Guid("279A824D-A0CB-4DE0-81BE-5A0B51413D80");

    // IMFSourceReader settable attributes (Phase 2: seeking)
    public static readonly Guid MF_SOURCE_READER_MEDIASOURCE     = new Guid("27E58F2E-5D3B-4E11-8B4D-2DE03F7941B3");

    // Media source stream GUID (for GetServiceForStream)
    public static readonly Guid MR_STREAM_MEDIASOURCE            = new Guid("C6399CF4-8B8A-48DA-ADA8-E6B5D70BA04F");

    // Time format GUID for seeking
    public static readonly Guid MF_TIME_FORMAT_MEDIA_TIME_GUID   = new Guid("449A51A0-5E2B-4F69-BF56-E0343A9A9D9C");

    // Constant: first source stream index for GetServiceForStream
    public const uint MF_SOURCE_READER_FIRST_SOURCE_STREAM_IDX = 0xFFFFFFFE;

    // Event types
    public const uint MESourceStarted       = 0x0200;
    public const uint MESourceSeeked        = 0x0203;
    public const uint MEEndOfPresentation   = 0x0205;
    public const uint MEBufferingStarted    = 0x0206;
    public const uint MEBufferingStopped    = 0x0207;
    public const uint MEError               = 0x0209;
    public const uint MENewPresentation     = 0x020F;

    // MFSessionMessage enum — sent to ProcessMessage for source control
    internal enum MFSessionMessage : uint
    {
        NotifySeek = 0x1000  // Notify the reader of a seek on the media source
    }

    // MF startup version (0x00010001 = MF 1.0)
    public const uint MF_VERSION_1_0 = 0x00010001;
}

#endregion

#region COM Interfaces

// ===========================================================================
// COM interfaces are defined with the full vtable layout matching native.
// IMPORTANT: Each interface inherits from its base — all base methods must
// appear first in the C# definition, in the same order as the native vtable.
// ===========================================================================

#region Audio WASAPI Interfaces (Phase 2)

// -------------------------------------------------------------------------
// IMMDevice
// -------------------------------------------------------------------------
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMMDevice
{
    int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
    int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
    int GetId(out IntPtr ppstrId);
    int GetState(out uint pdwState);
}

// -------------------------------------------------------------------------
// IMMDeviceCollection
// -------------------------------------------------------------------------
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMMDeviceCollection
{
    int GetCount(out uint pcDevices);
    int Item(uint nDevice, out IntPtr ppDevice);
}

// -------------------------------------------------------------------------
// IMMDeviceEnumerator
// -------------------------------------------------------------------------
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppDevice);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IntPtr ppDevice);
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

/// <summary>EDataFlow: matches Windows Core Audio.</summary>
internal enum EDataFlow : int
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
}

/// <summary>ERole: default device role.</summary>
internal enum ERole : int
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

// -------------------------------------------------------------------------
// IAudioClient
// -------------------------------------------------------------------------
[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IAudioClient
{
    int Initialize(int ShareMode, uint StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr pAudioSessionGuid);
    int GetBufferSize(out uint pNumBufferFrames);
    int GetStreamLatency(out long phnsLatency);
    int GetCurrentPadding(out uint pNumPaddingFrames);
    int IsFormatSupported(int ShareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
    int GetMixFormat(out IntPtr ppDeviceFormat);
    int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
    int Start();
    int Stop();
    int Reset();
    int SetEventHandle(IntPtr eventHandle);
    int GetService(ref Guid riid, out IntPtr ppv);
}

/// <summary>AUDCLNT_SHAREMODE: how an audio stream is shared.</summary>
internal enum AUDCLNT_SHAREMODE : int
{
    AUDCLNT_SHAREMODE_SHARED = 0,
    AUDCLNT_SHAREMODE_EXCLUSIVE = 1,
}

/// <summary>Common AUDCLNT_STREAMFLAGS.</summary>
internal static class AudClntStreamFlags
{
    public const uint AUDCLNT_STREAMFLAGS_CROSSPROCESS  = 0x00100000;
    public const uint AUDCLNT_STREAMFLAGS_LOOPBACK       = 0x00020000;
    public const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK  = 0x00040000;
    public const uint AUDCLNT_STREAMFLAGS_NOPERSIST      = 0x00080000;
}

// -------------------------------------------------------------------------
// IAudioRenderClient
// -------------------------------------------------------------------------
[ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA70263E3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IAudioRenderClient
{
    int GetBuffer(uint NumFrames, out IntPtr ppData);
    int ReleaseBuffer(uint NumFrames, uint dwFlags);
}

#endregion

// -------------------------------------------------------------------------
// IMFAttribute  (base of IMFAttributes → base of IMFMediaType)
//  VTable:
//   0: QueryInterface    (from IUnknown, handled by CLR)
//   1: AddRef            (from IUnknown)
//   2: Release           (from IUnknown)
//   3: GetItem
//   4: GetItemType
//   5: CompareItem
//   6: SetItem
//   7: DeleteItem
//   8: DeleteAllItems
// -------------------------------------------------------------------------
[ComImport, Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFAttribute
{
    int GetItem(ref Guid key, out IntPtr pvValue);
    int GetItemType(ref Guid key, out uint pType);
    int CompareItem(ref Guid key, IntPtr value, out int pbResult);
    int SetItem(ref Guid key, IntPtr value);
    int DeleteItem(ref Guid key);
    int DeleteAllItems();
}

// -------------------------------------------------------------------------
// IMFAttributes  (extends IMFAttribute)
//  Adds: GetCount, GetItemByIndex, Compare, then all Get/Set methods
// -------------------------------------------------------------------------
[ComImport, Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFAttributes
{
    // -- inherited from IMFAttribute --
    int GetItem(ref Guid key, out IntPtr pvValue);
    int GetItemType(ref Guid key, out uint pType);
    int CompareItem(ref Guid key, IntPtr value, out int pbResult);
    int SetItem(ref Guid key, IntPtr value);
    int DeleteItem(ref Guid key);
    int DeleteAllItems();

    // -- own methods --
    int GetCount(out uint pcItems);
    int GetItemByIndex(uint unIndex, out Guid pKey, out IntPtr ppValue);
    int Compare(IMFAttributes? pTheirs, uint dwMatchType, out int pbResult);
    int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint punValue);
    int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out ulong punValue);
    int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out double pfValue);
    int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out Guid pguid);
    int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint pcchLength);
    int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] out string? wszValue);
    int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr ppwszValue, out uint pcchLength);
    int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint pcbBlobSize);
    int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[]? pBuf, uint cbBufSize, out uint pcbBlobSize);
    int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr ip, out uint pcbSize);
    int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, ref Guid riid, out IntPtr ppv);
    int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, uint unValue);
    int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, ulong unValue);
    int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, double fValue);
    int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guid);
    int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[]? pBuf, uint cbBufSize);
    int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);
}

// -------------------------------------------------------------------------
// IMFMediaType  (extends IMFAttributes)
// -------------------------------------------------------------------------
[ComImport, Guid("C49A51E4-BDBF-4EF9-8DA6-DE805C58FB4F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaType
{
    // -- inherited from IMFAttribute --
    int GetItem(ref Guid key, out IntPtr pvValue);
    int GetItemType(ref Guid key, out uint pType);
    int CompareItem(ref Guid key, IntPtr value, out int pbResult);
    int SetItem(ref Guid key, IntPtr value);
    int DeleteItem(ref Guid key);
    int DeleteAllItems();

    // -- inherited from IMFAttributes --
    int GetCount(out uint pcItems);
    int GetItemByIndex(uint unIndex, out Guid pKey, out IntPtr ppValue);
    int Compare(IMFAttributes? pTheirs, uint dwMatchType, out int pbResult);
    int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint punValue);
    int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out ulong punValue);
    int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out double pfValue);
    int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out Guid pguid);
    int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint pcchLength);
    int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] out string? wszValue);
    int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr ppwszValue, out uint pcchLength);
    int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out uint pcbBlobSize);
    int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[]? pBuf, uint cbBufSize, out uint pcbBlobSize);
    int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr ip, out uint pcbSize);
    int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, ref Guid riid, out IntPtr ppv);
    int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, uint unValue);
    int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, ulong unValue);
    int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, double fValue);
    int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guid);
    int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[]? pBuf, uint cbBufSize);
    int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.IUnknown)] object? pUnknown);

    // -- own methods --
    int GetMajorType(out Guid pguidMajorType);
    int IsCompressedFormat(out int pfCompressed);
    int IsEqual(IMFMediaType? pIMediaType, out uint pdwFlags);
    int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
    void FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
}

// -------------------------------------------------------------------------
// IMFSourceReaderCallback
// -------------------------------------------------------------------------
[ComImport, Guid("DEEC8D99-FA1D-4D82-84C2-2A5DE92B126D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSourceReaderCallback
{
    int OnReadSample(int hrStatus, uint dwStreamIndex, uint dwStreamFlags, long llTimestamp, IMFSample? pSample);
    int OnFlush(uint dwStreamIndex);
    int OnEvent(uint dwStreamIndex, IMFMediaEvent? pEvent);
}

// -------------------------------------------------------------------------
// IMFSourceReader
// -------------------------------------------------------------------------
[ComImport, Guid("DEEC8D99-FA1D-4D82-84C2-2A5DE92B126C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFSourceReader
{
    int GetNativeMediaType(uint dwStreamIndex, uint dwTypeIndex, out IMFMediaType? ppMediaType);
    int GetCurrentMediaType(uint dwStreamIndex, out IMFMediaType? ppMediaType);
    int SetCurrentMediaType(uint dwStreamIndex, uint pReserved, IMFMediaType? pMediaType);
    int SetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] int fSelected);
    int GetStreamSelection(uint dwStreamIndex, [MarshalAs(UnmanagedType.Bool)] out int pfSelected);
    int GetServiceForStream(uint dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    int ProcessMessage(uint eMessage, ulong ulParam);
    int ReadSample(uint dwStreamIndex, uint dwFlags, out int pdwActualStreamIndex, out uint pdwFlags, out long pTimestamp, out IMFSample? ppSample);
    int Flush(uint dwStreamIndex);
    int ShutDown();
    int NotifyReadSample(IMFSourceReaderCallback? pCallback);
}

// -------------------------------------------------------------------------
// IMFMediaSession
// -------------------------------------------------------------------------
[ComImport, Guid("4A457EEA-E0A2-4E6B-BE08-7F9C4B578F97"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaSession
{
    int GetEvent(uint dwFlags, out IMFMediaEvent? ppEvent);
    int BeginGetEvent(IMFAsyncCallback? pCallback, [MarshalAs(UnmanagedType.IUnknown)] object? punkState);
    int EndGetEvent(IMFAsyncResult? pResult, out IMFMediaEvent? ppEvent);
    int QueueEventParamVar(ushort met, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidExtendedType, int hrStatus, [In] IntPtr pvValue);
    int QueueEventUnkParam(ushort met, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidExtendedType, [MarshalAs(UnmanagedType.IUnknown)] object? punkValue);
    int SetTopology(uint dwSetTopologyFlags, IMFTopology? pTopology);
    int ClearTopologies();
    int Start([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidTimeFormat, [In] long pvarStartPosition);
    int Stop();
    int Pause();
    int GetState(uint dwTimeoutMS, out uint pState);
}

// -------------------------------------------------------------------------
// IMFMediaEventGenerator
// -------------------------------------------------------------------------
[ComImport, Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaEventGenerator
{
    int GetEvent(uint dwFlags, out IMFMediaEvent? ppEvent);
    int BeginGetEvent(IMFAsyncCallback? pCallback, [MarshalAs(UnmanagedType.IUnknown)] object? punkState);
    int EndGetEvent(IMFAsyncResult? pResult, out IMFMediaEvent? ppEvent);
    int QueueEventParamVar(ushort met, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidExtendedType, int hrStatus, [In] IntPtr pvValue);
    int QueueEventUnkParam(ushort met, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidExtendedType, [MarshalAs(UnmanagedType.IUnknown)] object? punkValue);
}

// -------------------------------------------------------------------------
// IMFMediaEvent
// -------------------------------------------------------------------------
[ComImport, Guid("DF598931-F10C-4E71-86AB-34BE8F8F8CE9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaEvent
{
    int GetType(out ushort met);
    int GetExtendedType(out Guid pguidExtendedType);
    int GetStatus(out int phrStatus);
    int GetValue(out IntPtr ppvValue);
}

// -------------------------------------------------------------------------
// IMFSample
// -------------------------------------------------------------------------
[ComImport, Guid("C40A00F2-B93A-4D80-AE83-10A4943828AA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFSample
{
    int GetSampleFlags(out uint pdwSampleFlags);
    int GetSampleTime(out long phnsSampleTime);
    int GetSampleDuration(out long phnsSampleDuration);
    int GetBufferCount(out uint pdwBufferCount);
    int GetBufferByIndex(uint dwIndex, out IMFMediaBuffer? ppBuffer);
    int ConvertToContiguousBuffer(out IMFMediaBuffer? ppBuffer);
    int AddBuffer(IMFMediaBuffer? pBuffer);
    int RemoveBufferByIndex(uint dwIndex);
    int RemoveAllBuffers();
    int GetTotalLength(out uint pcbTotalLength);
    int CopyToBuffer(IMFMediaBuffer? pBuffer);
}

// -------------------------------------------------------------------------
// IMFMediaBuffer
// -------------------------------------------------------------------------
[ComImport, Guid("35FE3BBE-2EE0-4393-8BC6-E78C77FDB003"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaBuffer
{
    int Lock(out IntPtr ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
    int Unlock();
    int GetCurrentLength(out uint pcbCurrentLength);
    int SetCurrentLength(uint cbCurrentLength);
    int GetMaxLength(out uint pcbMaxLength);
}

// -------------------------------------------------------------------------
// IMFTopology / IMFTopoNode  (stubs — not used in source-reader path)
// -------------------------------------------------------------------------
[ComImport, Guid("A046FA16-6816-4E4F-A340-C0DE6E184F68"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFTopology { }

[ComImport, Guid("FBE5A32D-A4FE-429C-AB92-D7E0C10BA4D5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFTopoNode { }

// -------------------------------------------------------------------------
// IMFAsyncCallback
// -------------------------------------------------------------------------
[ComImport, Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAsyncCallback
{
    int GetParameters(out uint pdwFlags, out uint pdwQueue);
    int Invoke(IMFAsyncResult? pAsyncResult);
}

// -------------------------------------------------------------------------
// IMFAsyncResult
// -------------------------------------------------------------------------
[ComImport, Guid("2CD0BD52-BCD5-4B89-B62C-EB1F79D166FF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAsyncResult
{
    int GetStatus();
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object? GetObject();
    IMFAsyncCallback GetStateNoAddRef();
}

// -------------------------------------------------------------------------
// IMFPresentationDescriptor  (Duration + Seeking)
// -------------------------------------------------------------------------
[ComImport, Guid("03CB2711-24D7-4DB6-A17F-F3170D10802F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFPresentationDescriptor
{
    int GetStreamDescriptorCount(out uint pdwDescriptorCount);
    int GetStreamDescriptorByIndex(uint dwIndex, out IntPtr pfSelected, out IntPtr ppDescriptor);
    int SelectStream(uint dwDescriptorIndex);
    int DeselectStream(uint dwDescriptorIndex);
    int Clone(out IMFPresentationDescriptor? ppPresentationDescriptor);
    int GetItemByKey(ref Guid guidKey, out IntPtr pValue);
    int GetItemTypeByKey(ref Guid guidKey, out uint pType);
    int CompareItem(ref Guid guidKey, ref Guid Value, out bool pbResult);
    int Compare(IMFAttributes? pTheirs, uint MatchType, out bool pbResult);
    int GetUINT32(ref Guid guidKey, out uint punValue);
    int GetUINT64(ref Guid guidKey, out ulong punValue);
    int GetDouble(ref Guid guidKey, out double pfValue);
    int GetGUID(ref Guid guidKey, out Guid pguidValue);
    int GetStringLength(ref Guid guidKey, out uint pcchLength);
    int GetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, uint cchBufSize, out uint pcchLength);
    int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
    int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
    int SetItem(ref Guid guidKey, IntPtr Value);
    int SetUINT32(ref Guid guidKey, uint unValue);
    int SetUINT64(ref Guid guidKey, ulong unValue);
    int SetDouble(ref Guid guidKey, double fValue);
    int SetGUID(ref Guid guidKey, ref Guid guidValue);
    int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    int SetBlob(ref Guid guidKey, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, uint cbBufSize);
    int SetUnknown(ref Guid guidKey, IntPtr pUnknown);
    int LockStore();
    int UnlockStore();
    int GetCount(out uint pcItems);
    int GetItemByIndex(uint unIndex, out Guid pKey, out IntPtr ppValue);
    int GetAllKeys(out IntPtr ppKeys, out uint pcKeys);
}

// -------------------------------------------------------------------------
// IMFMediaSource  (needed for seeking via presentation descriptor)
// -------------------------------------------------------------------------
[ComImport, Guid("279A824D-A0CB-4DE0-81BE-5A0B51413D80"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMFMediaSource
{
    int GetCharacteristics(out uint pdwCharacteristics);
    int CreatePresentationDescriptor(out IMFPresentationDescriptor? ppPresentationDescriptor);
    int Start(IMFPresentationDescriptor? pPresentationDescriptor, ref Guid pguidTimeFormat, ref long llPosition);
    int Stop();
    int Pause();
    int Shutdown();
}

// -------------------------------------------------------------------------
// IMFVideoDisplayControl
// -------------------------------------------------------------------------
[ComImport, Guid("C10BA4D5-A2FA-4C9D-84D6-0C1E3DBC4DB6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFVideoDisplayControl { }

// -------------------------------------------------------------------------
// DXGI interfaces
// -------------------------------------------------------------------------
[ComImport, Guid("54EC77FA-1377-44E6-9867-B87D78477247"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIDevice
{
    int SetGPUThreadPriority(int Priority);
    int GetGPUThreadPriority(out int pPriority);
}

[ComImport, Guid("2411E7E1-12AC-4FCF-BD14-979828264082"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIAdapter
{
    int EnumOutputs(uint Output, out IntPtr ppOutput);
    int GetDesc(out DXGI_ADAPTER_DESC pDesc);
    int CheckInterfaceSupport([In, MarshalAs(UnmanagedType.LPStruct)] Guid InterfaceName, out long pUMDVersion);
}

[ComImport, Guid("50C83A1C-E072-4C48-87B0-3630FA36A6D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIFactory2
{
    int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
    int CreateSwapChain(IntPtr pDevice, ref DXGI_SWAP_CHAIN_DESC1 pDesc, out IntPtr ppSwapChain);
    int CreateSwapChainForHwnd(IntPtr pDevice, IntPtr hWnd, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pFullscreenDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);
}

[ComImport, Guid("790A45F7-0DDF-4782-A291-9974D5898B71"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGISwapChain1
{
    int Present(uint SyncInterval, uint Flags);
    int GetBuffer(uint Buffer, ref Guid riid, out IntPtr ppSurface);
    int SetFullscreenState([MarshalAs(UnmanagedType.Bool)] int Fullscreen, IntPtr pTarget);
    int GetFullscreenState([MarshalAs(UnmanagedType.Bool)] out int pFullscreen, out IntPtr ppTarget);
    int GetDesc(out DXGI_SWAP_CHAIN_DESC1 pDesc);
    int ResizeBuffers(uint BufferCount, uint Width, uint Height, uint NewFormat, uint SwapChainFlags);
    int ResizeTarget(ref DXGI_MODE_DESC pNewTargetParameters);
    int GetContainingOutput(out IntPtr ppOutput);
    int GetFrameStatistics(out DXGI_FRAME_STATISTICS pStats);
    int GetLastPresentCount(out uint pLastPresentCount);
}

// -------------------------------------------------------------------------
// D3D11 interfaces
// -------------------------------------------------------------------------
[ComImport, Guid("DB6F6D46-D8FA-4285-A54B-AD0260FED5EC"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Device
{
    int CreateBuffer(ref D3D11_BUFFER_DESC pDesc, IntPtr pInitialData, out IntPtr ppBuffer);
    int CreateTexture2D(ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);
    int CreateRenderTargetView(IntPtr pResource, IntPtr pDesc, out IntPtr ppRTView);
    int CreateDepthStencilView(IntPtr pResource, IntPtr pDesc, out IntPtr ppDepthStencilView);
    int CreateShaderResourceView(IntPtr pResource, ref D3D11_SHADER_RESOURCE_VIEW_DESC pDesc, out IntPtr ppSRView);
    int CreateInputLayout(IntPtr pInputElementDescs, uint NumElements, IntPtr pShaderBytecodeWithInputSignature, nuint BytecodeLength, out IntPtr ppInputLayout);
    int CreateVertexShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppVertexShader);
    int CreatePixelShader(IntPtr pShaderBytecode, nuint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppPixelShader);
    int CreateSamplerState(ref D3D11_SAMPLER_DESC pSamplerDesc, out IntPtr ppSamplerState);
    int CreateBlendState(ref D3D11_BLEND_DESC pBlendStateDesc, out IntPtr ppBlendState);
    int CreateRasterizerState(ref D3D11_RASTERIZER_DESC pRasterizerDesc, out IntPtr ppRasterizerState);
    int CreateDepthStencilState(ref D3D11_DEPTH_STENCIL_DESC pDepthStencilDesc, out IntPtr ppDepthStencilState);
    int GetImmediateContext(out IntPtr ppImmediateContext);
}

[ComImport, Guid("E707DCDE-D1CD-41CF-B4C5-51842004F539"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface ID3D11DeviceContext
{
    void VSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void VSSetShader(IntPtr pVertexShader, IntPtr pClassInstances, uint NumClassInstances);
    void PSSetShaderResources(uint StartSlot, uint NumViews, IntPtr ppShaderResourceViews);
    void PSSetShader(IntPtr pPixelShader, IntPtr pClassInstances, uint NumClassInstances);
    void PSSetSamplers(uint StartSlot, uint NumSamplers, IntPtr ppSamplers);
    void PSSetConstantBuffers(uint StartSlot, uint NumBuffers, IntPtr ppConstantBuffers);
    void Draw(uint VertexCount, uint StartVertexLocation);
    void ClearRenderTargetView(IntPtr pRenderTargetView, [MarshalAs(UnmanagedType.LPArray, SizeConst = 4)] float[] ColorRGBA);
    void ClearDepthStencilView(IntPtr pDepthStencilView, uint ClearFlags, float Depth, byte Stencil);
    void CopyResource(IntPtr pDstResource, IntPtr pSrcResource);
    void OMSetRenderTargets(uint NumViews, IntPtr ppRenderTargetViews, IntPtr pDepthStencilView);
    void OMSetBlendState(IntPtr pBlendState, float[] BlendFactor, uint SampleMask);
    void RSSetState(IntPtr pRasterizerState);
    void RSSetViewports(uint NumViewports, IntPtr pViewports);
    void IASetInputLayout(IntPtr pInputLayout);
    void IASetPrimitiveTopology(uint Topology);
    void IASetVertexBuffers(uint StartSlot, uint NumBuffers, IntPtr ppVertexBuffers, IntPtr pStrides, IntPtr pOffsets);
    int Map(IntPtr pResource, uint Subresource, uint MapType, uint MapFlags, out MappedSubresource pMappedResource);
    void Unmap(IntPtr pResource, uint Subresource);
}

// -------------------------------------------------------------------------
// ID3D11ShaderResourceView  (Phase 3: NV12→BGRA shader input)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC159-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11ShaderResourceView
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, out uint pDataSize, out IntPtr pData);
    int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
    void GetResource(out IntPtr ppResource, out uint pSubresource);
}

// -------------------------------------------------------------------------
// ID3D11VertexShader  (Phase 3: NV12→BGRA shader)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC160-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11VertexShader
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, out uint pDataSize, out IntPtr pData);
    int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
}

// -------------------------------------------------------------------------
// ID3D11PixelShader  (Phase 3: NV12→BGRA conversion)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC161-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11PixelShader
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, out uint pDataSize, out IntPtr pData);
    int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
}

// -------------------------------------------------------------------------
// ID3D11InputLayout  (Phase 3: full-screen quad vertices)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC15C-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11InputLayout
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, out uint pDataSize, out IntPtr pData);
    int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
}

// -------------------------------------------------------------------------
// ID3D11Buffer  (Phase 3: vertex buffer for full-screen quad)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC16C-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11Buffer
{
    void GetResource(out IntPtr ppParentResource, out uint pSubresource);
}

// -------------------------------------------------------------------------
// ID3D11SamplerState  (Phase 3: linear sampler for YUV textures)
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC16D-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11SamplerState
{
    void GetDevice(out IntPtr ppDevice);
    void GetPrivateData(ref Guid guid, out uint pDataSize, out IntPtr pData);
    int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
    int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
}

// -------------------------------------------------------------------------
// ID3DBlob  (shader compilation output)
// -------------------------------------------------------------------------
[ComImport, Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3DBlob
{
    IntPtr GetBufferPointer();
    nuint GetBufferSize();
}

// -------------------------------------------------------------------------
// ID3D11BlendState
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC16E-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11BlendState { }

// -------------------------------------------------------------------------
// ID3D11RasterizerState
// -------------------------------------------------------------------------
[ComImport, Guid("8DBEC15D-E8F9-40B3-A516-0D8ED7BA3A2D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ID3D11RasterizerState
{
    void GetDevice(out IntPtr ppDevice);
}

#endregion

#region COM Enums

internal enum MFMediaEventType : uint
{
    MEUnknown            = 0x0100,
    GenericV1Start       = 0x0100,
    GenericV1End         = 0x0101,
    MESourceStarted      = 0x0200,
    MESourceSeeked       = 0x0203,
    MEEndOfPresentation   = 0x0205,
    MEBufferingStarted   = 0x0206,
    MEBufferingStopped   = 0x0207,
    MEError              = 0x0209,
    MENewPresentation    = 0x020F,
}

internal enum MFSessionState : uint
{
    Closed = 0, Ready = 1, Started = 2, Paused = 3, Stopped = 4, ShutDown = 5,
}

internal enum D3D11Format : uint
{
    FormatB8G8R8A8Unorm = 87,
    FormatB8G8R8X8Unorm = 88,
    FormatR8G8B8A8Unorm = 28,
}

[Flags]
internal enum SwapChainFlag : uint
{
    None = 0,
    AllowModeSwitch = 0x04,
    AllowTearing = 0x200,
    GdiCompatible = 0x01,
}

internal enum D3D11BindFlag : uint
{
    RenderTarget = 0x4,
    DepthStencil = 0x10,
    ShaderResource = 0x8,
}

internal enum D3D11ResourceMiscFlag : uint
{
    Shared = 0x2,
    SharedKeyedmutex = 0x1000000,
}

internal enum D3D11MapMode : uint
{
    Read = 1,
    Write = 2,
    ReadWrite = 3,
    WriteDiscard = 4,
    WriteNoOverwrite = 5,
}

internal enum D3D11Usage : uint
{
    Default = 0,
    Immutable = 1,
    Dynamic = 2,
    Staging = 4,
}

internal enum DXGI_FORMAT : uint
{
    DXGI_FORMAT_B8G8R8A8_UNORM = 87,
    DXGI_FORMAT_R8G8B8A8_UNORM = 28,
    DXGI_FORMAT_B8G8R8X8_UNORM = 88,
    DXGI_FORMAT_NV12 = 103,
    DXGI_FORMAT_P010 = 105,
}

#endregion

#region Structures

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_SWAP_CHAIN_DESC1
{
    public uint Width;
    public uint Height;
    public uint Format;
    public uint Stereo;
    public uint SampleDesc_Count;
    public uint SampleDesc_Quality;
    public uint BufferUsage;
    public uint BufferCount;
    public uint Scaling;
    public uint SwapEffect;
    public uint AlphaMode;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_MODE_DESC
{
    public uint Width;
    public uint Height;
    public uint RefreshRate_Numerator;
    public uint RefreshRate_Denominator;
    public uint Format;
    public uint ScanlineOrdering;
    public uint Scaling;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_FRAME_STATISTICS
{
    public uint PresentCount;
    public uint PresentRefreshCount;
    public long SyncRefreshCount;
    public long SyncQPCTime;
    public long SyncGPUTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MFVideoArea
{
    public int OffsetX;
    public int OffsetY;
    public uint AreaCX;
    public uint AreaCY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MFRatio
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Explicit)]
internal struct MFVideoAreaUnion
{
    [FieldOffset(0)] public MFVideoArea Offset;
    [FieldOffset(0)] public MFRatio AppMode;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MFVideoNormalizedRect
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;
}

#pragma warning disable CS0649
internal struct MappedSubresource
{
    public IntPtr pData;
    public uint RowPitch;
    public uint DepthPitch;
}
#pragma warning restore CS0649

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_BUFFER_DESC
{
    public uint ByteWidth;
    public uint Usage;
    public uint BindFlags;
    public uint CPUAccessFlags;
    public uint MiscFlags;
    public uint StructureByteStride;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_TEXTURE2D_DESC
{
    public uint Width;
    public uint Height;
    public uint MipLevels;
    public uint ArraySize;
    public uint Format;
    public uint SampleDesc_Count;
    public uint SampleDesc_Quality;
    public uint Usage;
    public uint BindFlags;
    public uint CPUAccessFlags;
    public uint MiscFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_SAMPLER_DESC
{
    public uint Filter;
    public uint AddressU;
    public uint AddressV;
    public uint AddressW;
    public float MipLODBias;
    public uint MaxAnisotropy;
    public uint ComparisonFunc;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BorderColor;
    public float MinLOD;
    public float MaxLOD;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_BLEND_DESC
{
    public byte AlphaToCoverageEnable;
    public byte IndependentBlendEnable;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] RenderTarget;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_RASTERIZER_DESC
{
    public uint FillMode;
    public uint CullMode;
    public byte FrontCounterClockwise;
    public int DepthBias;
    public float DepthBiasClamp;
    public float SlopeScaledDepthBias;
    public byte DepthClipEnable;
    public byte ScissorEnable;
    public byte MultisampleEnable;
    public byte AntialiasedLineEnable;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_DEPTH_STENCIL_DESC
{
    public byte DepthEnable;
    public byte DepthWriteMask;
    public uint DepthFunc;
    public byte StencilEnable;
    public byte StencilReadMask;
    public byte StencilWriteMask;
    public uint FrontFace_StencilFailOp;
    public uint FrontFace_StencilDepthFailOp;
    public uint FrontFace_StencilPassOp;
    public uint FrontFace_StencilFunc;
    public uint BackFace_StencilFailOp;
    public uint BackFace_StencilDepthFailOp;
    public uint BackFace_StencilPassOp;
    public uint BackFace_StencilFunc;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_ADAPTER_DESC
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Description;
    public uint VendorId;
    public uint DeviceId;
    public ulong SubSysId;
    public uint Revision;
    public ulong DedicatedVideoMemory;
    public ulong DedicatedSystemMemory;
    public ulong SharedSystemMemory;
    public IntPtr AdapterLuid;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_VIEWPORT
{
    public float TopLeftX;
    public float TopLeftY;
    public float Width;
    public float Height;
    public float MinDepth;
    public float MaxDepth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_BOX
{
    public uint Left;
    public uint Top;
    public uint Front;
    public uint Right;
    public uint Bottom;
    public uint Back;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DXGI_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

// -------------------------------------------------------------------------
// D3D11_INPUT_ELEMENT_DESC (Phase 3: NV12→BGRA shader pipeline)
// -------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_INPUT_ELEMENT_DESC
{
    public IntPtr SemanticName;
    public uint SemanticIndex;
    public uint Format;
    public uint InputSlot;
    public uint AlignedByteOffset;
    public uint InputSlotClass;
    public uint InstanceDataStepRate;
}

// -------------------------------------------------------------------------
// D3D11_SUBRESOURCE_DATA (Phase 3: texture initialization)
// -------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_SUBRESOURCE_DATA
{
    public IntPtr pSysMem;
    public uint SysMemPitch;
    public uint SysMemSlicePitch;
}

// -------------------------------------------------------------------------
// D3D11_SHADER_RESOURCE_VIEW_DESC (Phase 3: NV12 texture SRVs)
// -------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_SHADER_RESOURCE_VIEW_DESC
{
    public uint Format;
    public uint ViewDimension;
    public uint MostDetailedMip;
    public uint MipLevels;
}

// -------------------------------------------------------------------------
// D3D11_TEX2D_SRV (Phase 3: part of SRV desc union for texture2D)
// -------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct D3D11_TEX2D_SRV
{
    public uint MostDetailedMip;
    public uint MipLevels;
}

#endregion

#region Audio Structures (WASAPI Phase 2)

[StructLayout(LayoutKind.Sequential)]
internal struct WAVEFORMATEX
{
    public ushort wFormatTag;
    public ushort nChannels;
    public uint nSamplesPerSec;
    public uint nAvgBytesPerSec;
    public ushort nBlockAlign;
    public ushort wBitsPerSample;
    public ushort cbSize;
}

#endregion

#region Helper: MfLockedBuffer

/// <summary>
/// Wraps an IMFMediaBuffer for safe locking/unlocking.
/// RAII pattern: the buffer is unlocked in Dispose().
/// </summary>
internal sealed class MfLockedBuffer : IDisposable
{
    private IMFMediaBuffer? _buffer;
    private IntPtr _lockedPtr = IntPtr.Zero;
    private uint _currentLength;
    private uint _maxLength;
    private bool _disposed;

    public IntPtr Data => _lockedPtr;
    public int Length => (int)_currentLength;
    public int MaxLength => (int)_maxLength;

    internal MfLockedBuffer(IMFMediaBuffer buffer)
    {
        _buffer = buffer;
        int hr = _buffer.Lock(out _lockedPtr, out _maxLength, out _currentLength);
        if (hr < 0)
        {
            _lockedPtr = IntPtr.Zero;
            _maxLength = 0;
            _currentLength = 0;
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    public static MfLockedBuffer? FromSample(IMFSample? sample)
    {
        if (sample == null) return null;
        int hr = sample.ConvertToContiguousBuffer(out IMFMediaBuffer? buffer);
        if (hr < 0 || buffer == null) return null;
        return new MfLockedBuffer(buffer);
    }

    public void Dispose()
    {
        if (!_disposed && _buffer != null)
        {
            _buffer.Unlock();
            Marshal.ReleaseComObject(_buffer);
            _buffer = null;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~MfLockedBuffer() => Dispose();
}

#endregion

#region Native P/Invoke Methods

internal static class NativeMethods
{
    // ---- Media Foundation Platform ----
    [DllImport("mfplat.dll", SetLastError = true)]
    internal static extern int MFStartup(uint Version, uint dwFlags);

    [DllImport("mfplat.dll", SetLastError = true)]
    internal static extern int MFShutdown();

    // ---- Media Foundation Read/Write ----
    // Returns S_OK on success. PreserveSig=false means the CLR auto-throws
    // on failed HRESULTs. On success, the out parameter is populated.
    [DllImport("mfreadwrite.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateSourceReaderFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string pwszURL,
        IntPtr pAttributes,
        out IntPtr ppSourceReader);

    // ---- Media Foundation Core ----
    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateAttributes(out IntPtr ppMFAttributes, uint cInitialSize);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateMediaType(out IntPtr ppMFMediaType);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateTopology(out IntPtr ppTopology);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateMediaSession(IntPtr pConfiguration, out IntPtr ppSession);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateSample(out IntPtr ppIMFSample);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IntPtr ppBuffer);

    [DllImport("mf.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int MFCreateDXGISurfaceBuffer(
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        IntPtr punkSurface,
        [MarshalAs(UnmanagedType.Bool)] int fBottomUpWhenLinear,
        out IntPtr ppBuffer);

    // ---- DXGI ----
    [DllImport("dxgi.dll", SetLastError = true, PreserveSig = true)]
    internal static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    [DllImport("dxgi.dll", SetLastError = true, PreserveSig = true)]
    internal static extern int CreateDXGIFactory2(uint Flags, ref Guid riid, out IntPtr ppFactory);

    // ---- D3D11 ----
    [DllImport("d3d11.dll", SetLastError = true, PreserveSig = true)]
    internal static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int DriverType,
        IntPtr Software,
        uint Flags,
        IntPtr pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    // ---- Audio/Threading ----
    [DllImport("avrt.dll", SetLastError = true)]
    internal static extern int AvSetMmThreadCharacteristics(
        [MarshalAs(UnmanagedType.LPWStr)] string TaskName, out ulong TaskIndex);

    [DllImport("avrt.dll", SetLastError = true)]
    internal static extern int AvRevertMmThreadCharacteristics(ulong Handle);

    // ---- COM ----
    [DllImport("ole32.dll")]
    internal static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    internal static extern void CoUninitialize();

    // ---- COM Creation ----
    [DllImport("ole32.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    // ---- Kernel32 (AudioRenderer) ----
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateEventW(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        IntPtr lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    // ---- PropVariant helpers ----
    [DllImport("propvarutil.dll", SetLastError = true)]
    internal static extern int InitPropVariantFromInt64(long llVal, IntPtr ppropvar);

    // ---- Shader compilation (Phase 3: NV12→BGRA) ----
    [DllImport("d3dcompiler_47.dll", SetLastError = true, PreserveSig = false)]
    internal static extern int D3DCompileFromFile(
        [MarshalAs(UnmanagedType.LPWStr)] string pFileName,
        IntPtr pDefines,
        IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1,
        uint Flags2,
        out IntPtr ppCode,
        out IntPtr ppErrorMsgs);

    [DllImport("d3dcompiler_47.dll", SetLastError = true, PreserveSig = true)]
    internal static extern int D3DCompile(
        IntPtr pSrcData,
        nuint SrcDataSize,
        IntPtr pSourceName,
        IntPtr pDefines,
        IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1,
        uint Flags2,
        out IntPtr ppCode,
        out IntPtr ppErrorMsgs);

    internal static uint D3D11_CREATE_DEVICE_BGRA_SUPPORT2 = 0x20;
}

#endregion

#region Error Codes (HRESULTS)

internal static class HResult
{
    public const int S_OK         = 0;
    public const int S_FALSE      = 1;
    public const int E_FAIL       = unchecked((int)0x80004005);
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_POINTER    = unchecked((int)0x80004003);
    public const int E_INVALIDARG = unchecked((int)0x80070057);
    public const int VFW_E_NOT_FOUND = unchecked((int)0x80040215);
}

#endregion
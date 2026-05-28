# Debug Session: video-open-crash

Status: [OPEN]

Owner Priority:
- P0: Opening any video file crashes the app; playback never starts.

Symptom:
- UI is visible, but when using Open/Add video, the app crashes and video does not play.

Expected:
- Selecting a video file should start playback (or at least load media without crashing).

Repro Steps:
1) Launch app
2) Click Open (or drag/drop a video)
3) Select any supported video (mp4/mkv/etc)
4) App crashes before playback

Known Crash Signature (from logs):
- InvalidCastException casting COM object to `IMFSourceReader`
- `E_NOINTERFACE (0x80004002)`
- Stack: `MfHelper.EnumerateStreams()` -> `MfHelper.OpenFile()` -> `MediaFoundationPlayer.Open()` -> `MainViewModel.OpenFile()`

Hypotheses (falsifiable):
- H1: `IMFSourceReader` interface GUID in `MfComInterop.cs` is wrong, so `QueryInterface` fails with `E_NOINTERFACE`.
- H2: `IMFSourceReader` method order/signatures are wrong (vtable mismatch) causing an invalid cast / unusable RCW.
- H3: `MFCreateSourceReaderFromURL` succeeds but returns an object that is not an `IMFSourceReader` (attributes/activation path wrong); the right way is to call `MFCreateSourceReaderFromMediaSource` or use attributes.
- H4: Media Foundation startup/COM apartment or initialization sequence is incomplete (missing `MFStartup`/COM init) so returned COM object is unexpected.
- H5: The crash is triggered by a wrong interop layer boundary (storing the pointer as `IntPtr` and using `Marshal.GetObjectForIUnknown`), not by the MF API call itself.

Phased Plan:
- Phase 0 (Evidence): Add runtime instrumentation in Media layer around:
  - MF initialization (`MFStartup`/COM init results)
  - `MFCreateSourceReaderFromURL` HRESULT + returned pointer
  - `QueryInterface` results for `IMFSourceReader` IID(s)
  - First `GetNativeMediaType` HRESULTs per stream
- Phase 1 (Fix): Apply minimal interop correction based on evidence:
  - Correct `IMFSourceReader` IID and/or interface definitions
  - Adjust creation/casting strategy (e.g., explicit `QueryInterface` to a correct IID)
- Phase 2 (Verify): Re-run Open and confirm:
  - No crash
  - Media opens, duration populates, first frames render (or at least reads samples)
- Phase 3 (Cleanup): Remove instrumentation only after user confirms fix.

Progress Log:
- 2026-05-28:
  - Reproduced failure mode with a deterministic console smoke test (`src/MediaSmoke`) that generates a valid WAV and calls `MfHelper.OpenFile`.
  - Confirmed interop issues beyond the original `IMFSourceReader` cast:
    - `IMFSourceReader` IID and method surface were incorrect.
    - `IMFAttributes` IID was incorrect.
    - `IMFMediaType` IID was incorrect (caused `E_NOINTERFACE`).
    - `IMFSample` IID was incorrect.
    - `IMFMediaBuffer` IID was incorrect.
  - Fixed COM interop to match Win32 headers (using mingw-w64 mfobjects.h as reference):
    - Corrected IIDs and adjusted `IMFSourceReader` marshalling to avoid exceptions.
    - Added `PreserveSig` for `IMFSourceReader` to return HRESULTs instead of throwing.
    - Adjusted `SetStreamSelection` marshalling to use `bool`.
  - Result:
    - `dotnet run --project src/MediaSmoke/MediaSmoke.csproj` completes successfully (no crash in `MfHelper.OpenFile` path).

Artifacts:
- Env file: `.dbg/video-open-crash.env`
- Log file: `trae-debug-log-video-open-crash.ndjson`

[OPEN] Debug Session: no-playback

## Symptom
- App no longer crashes.
- Opening a video results in no visible playback (black/empty video area or still frame).

## Expected
- Video frames render and update during playback.

## Environment
- OS: Windows
- Repo: x:\Development\Cine_C#_Dot
- Date: 2026-05-28

## Hypotheses (falsifiable)
1) MF decoding loop is running, but no video samples are reaching the renderer (no `SampleReady` events or they stop immediately).
2) Samples arrive, but `D3D11Renderer.Present` is failing (device removed, swap chain invalid, size 0, HRESULT < 0).
3) Samples arrive and Present succeeds, but we are presenting into a hidden / 0x0 / wrong-position child HWND (video host created late or moved offscreen).
4) Pixel format mismatch (e.g., NV12/BGRA path disagreement) causes shader to output black even though Present succeeds.
5) Playback is paused/stopped at the MF layer (state mismatch) even though UI shows “playing”.

## Evidence to collect (pre-fix)
- VideoHost: bounds, HWND create success, SetWindowPos sizes, visible state.
- MF: MediaOpened, stream info (W/H/subtype), SampleReady cadence.
- D3D: renderer init success, ResizeBuffers inputs, Present counts + failures.

## Plan
- Start debug server and collect NDJSON logs while reproducing: launch app → open sample mp4 → press Play → wait 5s → toggle fullscreen → close app.
- Analyze logs and confirm/reject hypotheses.


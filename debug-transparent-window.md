# Debug Session: transparent-window

Status: [OPEN]

Symptom:
- App launches as a transparent window instead of showing the expected startup UI/content.

Expected:
- Main window should render the start page or player UI with an opaque background and visible controls/content.

Hypotheses:
- H1: A top-level Avalonia window or template is still forcing composition/transparency at runtime despite the XAML property changes.
- H2: The main content tree loads, but the startup content is hidden or sized to zero, leaving only the transparent shell visible.
- H3: A custom control or native host layer is interfering with top-level rendering and causing the visible region to remain transparent.
- H4: A later lifecycle event overrides window properties after XAML load and re-enables transparency-like behavior.
- H5: The actual binary being launched is stale or loading resources different from the edited source files.

Evidence Plan:
- Capture runtime logs for window property values before and after XAML load and when the window opens.
- Capture visibility/bounds/state for `StartPage`, `MainOverlay`, `VideoHost`, and controls on startup.
- Confirm the built binary path and startup log output correspond to the current source tree.

Progress Log:
- Session initialized.
- Instrumented `App.axaml.cs` and `MainWindow.axaml.cs` to report runtime window state into `.dbg/trae-debug-log-transparent-window.ndjson`.
- Confirmed the app launches from the expected binary path and the window background is opaque at runtime.
- Confirmed the content tree exists and is sized correctly on open (`Grid` content at `800x600`, `StartPage` found and visible).
- Identified the likely root cause: startup code was acting on uninitialized named controls, so `VideoHost` was not hidden on idle startup.
- Applied a minimal fix in `MainWindow.axaml.cs` to resolve named controls immediately after XAML load.

Verification Table:
- H1: Top-level window transparency still forced at runtime -> REJECTED. Runtime background reported `#ff0c0c0e`.
- H2: Content tree missing or zero-sized after open -> REJECTED. Content and `StartPage` were present at `800x600`.
- H3: Native video host remains visible above startup UI -> CONFIRMED. Pre-fix logs showed `videoHostVisible=true` on open; post-fix logs showed `videoHostVisible=false`.
- H4: Later lifecycle code re-enables shell transparency -> REJECTED. Open-state snapshots remained opaque; only `VideoHost` visibility changed.
- H5: Stale binary/resources being launched -> REJECTED. Runtime logs matched the current debug session and build output path.

Current Fix:
- `MainWindow` now resolves named controls immediately after `AvaloniaXamlLoader.Load(this)` so the existing startup-state logic can actually hide `VideoHost` and keep the idle start page visible.

Recheck Round:
- User reports the startup window still appears transparent after the previous fix attempt.
- This round reopens the same session for a full-surface audit instead of assuming the prior hypothesis remains sufficient.

Updated Hypotheses:
- H6: The top-level window chrome/client-area configuration still produces a transparent composition surface on Windows even with an opaque `Background`.
- H7: A child/native host or overlay is being created early and visually exposing the desktop because the root client area is not painting as expected.
- H8: Another style/template/resource in the app is overriding the effective window background or root container background after startup.
- H9: The current binary/runtime state differs from source expectations due to stale output or an unverified launch path.

Next Evidence Plan:
- Re-read startup logs and current debug events for the latest run.
- Search the entire app for transparency-, acrylic-, blur-, and client-area-related properties.
- If needed, add one more narrow instrumentation point around effective window transparency/platform transparency level after open.

Latest Evidence:
- Runtime recheck after the window-surface patch shows `extendClientArea=false` before XAML load, after XAML load, at DI resolution, and on `OnOpened`.
- Fresh startup debug events still show an opaque `#ff0c0c0e` background, `StartPageVisible=true`, and `VideoHostVisible=false` after initial state.
- The remaining runtime failure is the previously known Media Foundation `IMFSourceReader` cast crash when the user opens a file; that occurs after startup and is separate from the transparent-window path.

Recheck Conclusion:
- H6: Extended client-area chrome still active at runtime -> CONFIRMED before patch, REJECTED after patch.
- H7: Native host exposing the desktop on idle startup -> REJECTED in current startup logs; `VideoHost` is hidden after initial state.
- H8: Another style/resource is overriding the main window background -> REJECTED by fresh runtime snapshots.
- H9: Stale binary/runtime path -> REJECTED; fresh build and fresh logs reflect the updated window settings.

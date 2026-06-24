# Debug Session: subtitle-not-rendering
- **Status**: [OPEN]
- **Issue**: External subtitle tracks are added and appear in the subtitle menu, but subtitle text does not render on screen.
- **Debug Server**: not-started
- **Log File**: pending

## Reproduction Steps
1. Launch the app.
2. Open a video.
3. Open the subtitle menu.
4. Click `Add Subtitle Track...` and choose an external subtitle file.
5. Observe that subtitle options appear, but the subtitle text is still not visible on screen.

## Hypotheses & Verification
| ID | Hypothesis | Likelihood | Effort | Evidence |
|----|------------|------------|--------|----------|
| A | The app is selecting the wrong subtitle track ID after `sub-add`, so the external track is loaded but not the one actually rendered. | High | Low | Confirmed |
| B | The app state shows a subtitle as selected, but `sid` and `sub-visibility` inside mpv diverge from the UI state after rebuild/selection. | High | Low | Rejected in post-fix run |
| C | The subtitle track is selected correctly, but the render path is not compositing subtitle/OSD output into the visible frame. | Medium | Medium | Inconclusive |
| D | Track rebuild logic maps subtitle items to unstable IDs or fallback indices, so the selected menu item does not correspond to the actual mpv subtitle track. | High | Low | Partially confirmed |
| E | The external subtitle is loaded, but it has timing/format characteristics that make it appear absent even though mpv accepted the file. | Low | Medium | Rejected for the observed failing run |

## Log Evidence
- `C:\Users\paval\AppData\Local\Cine\logs\Cine_2026-06-24.log`: after `DispatchAddExternal: added`, `OnSubtitleTrackListChanged` reports subtitle IDs `1`, `2`, and new external `3`, where track `3` is `enabled=True`.
- `C:\Users\paval\AppData\Local\Cine\logs\Cine_2026-06-24.log`: immediately after that, the manager logs `DispatchAddExternal: auto-selecting track Sub: eng (off)` and `OnSelectSubtitle ... id=1`, which switches selection back to internal subtitle track `1`.
- `C:\Users\paval\AppData\Local\Cine\MpvPlayer.log`: the same run shows `track-list: parsed 5 tracks` before the add and `track-list: parsed 6 tracks` after the add, confirming mpv accepted the extra subtitle track.
- `C:\Users\paval\AppData\Local\Cine\MpvPlayer.log`: the external subtitle title appears as `--sub-codepage=utf-8:cp1252`, which indicates the current `sub-add` call is passing codepage data as a positional command argument instead of setting the mpv option separately.
- `C:\Users\paval\AppData\Local\Cine\logs\Cine_2026-06-24.log` after the fix: `DispatchAddExternal: auto-selecting track id=3 'Sub: AG-en (on)'`, followed by `OnSelectSubtitle ... id=3` and `OnSubtitlePropertyChanged: sid=3`, confirming the external track remains selected.
- `C:\Users\paval\AppData\Local\Cine\logs\Cine_2026-06-24.log` on a second add after the fix: the newly added duplicate external subtitle is selected as `id=4`, showing the selection logic now follows the actual newly added track rather than the first existing internal subtitle.
- `C:\Users\paval\Downloads\Murder.On.The.Orient.Express.2017.720p.10bit.BluRay.6CH.x265.HEVC-PSA\Murder.On.The.Orient.Express.2017.720p.BluRay.x264-YTS.AG-en.srt`: the first subtitle cue starts at `00:01:50,720`, so no subtitle text should be visible near the beginning of playback.

## Verification Conclusion
- Root cause identified in the failing run: the external subtitle loads into mpv, but the app then re-selects an older internal subtitle track, so the expected newly added subtitle is not what remains active.
- Secondary command bug identified: `sub-add` is being called with a positional `--sub-codepage=...` argument, which mpv treats as subtitle metadata/title rather than a real option.
- Post-fix evidence shows the app now keeps the external subtitle selected and labels it correctly as `AG-en`.
- Remaining user-facing ambiguity is likely playback position: the external subtitle file does not contain any cue until `00:01:50.720`, so testing before that timestamp will look like "subtitles still not visible" even when the load/selection flow is correct.

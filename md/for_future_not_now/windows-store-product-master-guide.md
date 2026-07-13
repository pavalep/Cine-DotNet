# Simba Windows Store Product Master Guide

Date: `2026-07-13`  
Project: `x:\Development\Cine_CSharp_DotNet`  
Target: Turn the current Avalonia/mpv app into a polished, sellable Windows Store media product

## How To Use This Guide

- This file is the master execution guide from start to finish.
- Keep it updated as work progresses.
- Use the checkboxes as the canonical status tracker.
- Do not skip phase exit criteria just because UI "looks close enough".
- If a phase fails its quality bar, fix that phase before moving forward.

## Status Legend

- `[x]` Done / already present in current codebase
- `[ ]` Pending
- `[~]` In progress / partially complete / needs validation
- `[!]` Risk / blocked / needs decision

## Product Goal

Build a real desktop media player product that is:

- visually premium
- fast during resize and playback
- cleanly structured for long-term maintenance
- complete enough for Windows Store submission
- not a demo, not a one-screen mockup, not a style-only pass

Core product pillars:

1. Home dashboard
2. Music library
3. Video library
4. Search
5. Recent / history
6. Playlist + queue
7. Favorites
8. Audio now-playing mode
9. Video now-playing mode
10. Quick settings + full settings
11. Keyboard-first desktop UX
12. MSIX/store-ready assets, metadata, and packaging

---

## 1. Current Baseline Snapshot

### Already present

- [x] Custom Avalonia shell with layered overlay architecture
- [x] `StartPage` and `PlayerPage` navigation flow
- [x] mpv integration
- [x] Reusable player chrome components:
  - `src/App/Views/Components/Chrome/HeaderBar.axaml`
  - `src/App/Views/Components/Chrome/ControlsBox.axaml`
- [x] Overlay panels:
  - `src/App/Views/Components/Panels/PlaylistPanel.axaml`
  - `src/App/Views/Components/Panels/EqualizerPanel.axaml`
  - `src/App/Views/Components/Panels/VolumePanel.axaml`
  - `src/App/Views/Components/Panels/SubtitlePanel.axaml`
  - `src/App/Views/Components/Panels/AudioTrackPanel.axaml`
  - `src/App/Views/Components/Panels/ChaptersPanel.axaml`
- [x] Resource token files already exist:
  - `src/App/Views/Resources/Colors.axaml`
  - `src/App/Views/Resources/Spacing.axaml`
  - `src/App/Views/Resources/Sizes.axaml`
  - `src/App/Views/Resources/Radius.axaml`
  - `src/App/Views/Resources/Typography.axaml`
  - `src/App/Views/Resources/Motion.axaml`
  - `src/App/Views/Resources/App.axaml`
- [x] MSIX manifest and generated app asset pipeline already exist
- [x] Basic asset folder already exists:
  - `src/App/Assets`

### Current architectural limitations

- [!] Only two app routes exist today:
  - `Start`
  - `Player`
- [!] `StartPage` is still a page-specific composition, not a reusable dashboard framework
- [!] A lot of view orchestration still lives in `MainWindow.*`
- [!] Inline text and page-local styling still exist in multiple XAML files
- [!] Color/token system is partially duplicated (`Accent` vs `AppAccent`, XAML vs code constants)
- [!] Search is only partial and local, not a product-level feature
- [!] There is no real library information architecture yet

---

## 2. Non-Negotiable Product Standards

These rules apply to every phase.

### Code and UI standards

- [ ] No inline colors in feature/page XAML unless the value is temporary during active refactor
- [ ] No inline typography values unless token does not yet exist
- [ ] No inline hardcoded text in final product screens
- [ ] No page-specific local color systems for long-term UI
- [ ] No duplicated token values between code and XAML
- [ ] No control-specific one-off styling when a reusable component style should exist
- [ ] No feature logic inside `MainWindow` if it belongs to a page, service, or component
- [ ] No "temporary" empty states shipped to production

### Performance standards

- [ ] Keep `UseLayoutRounding="False"` on shell windows
- [ ] Do not use `DynamicResource` for resize-critical live layout values
- [ ] Do not use `InvalidateVisual()` during resize loops
- [ ] Keep direct-property responsive updates for controls that must animate smoothly during resize
- [ ] Avoid file I/O or logging on hot resize paths
- [ ] Do not add heavy effects that degrade video playback overlays

### UX standards

- [ ] Mouse, keyboard, and context-menu flows all work
- [ ] Every interactive item has clear hover, focus, pressed, disabled states
- [ ] Empty, loading, error, and no-results states exist for every data-driven screen
- [ ] Search must be forgiving and fast
- [ ] Navigation must always tell the user where they are
- [ ] Audio mode and video mode must feel intentional, not accidental

### Product standards

- [ ] Real settings persistence
- [ ] Real media metadata and artwork support
- [ ] Real playlist and queue behavior
- [ ] Real search, not just a textbox shell
- [ ] Real assets for store packaging
- [ ] No dummy buttons in release build

---

## 3. Target Folder Structure

This is the target structure to move toward. Not every folder must be created in one commit, but this is the intended end state.

```text
src/App
├── Assets
│   ├── Brand
│   │   ├── simba-logo.svg
│   │   ├── app-icon.ico
│   │   └── store/
│   ├── Icons
│   │   ├── navigation/
│   │   ├── actions/
│   │   ├── media/
│   │   └── status/
│   ├── Artwork
│   │   ├── placeholders/
│   │   └── defaults/
│   └── Store
│       ├── screenshots/
│       ├── tiles/
│       └── splash/
├── Core
│   ├── Navigation
│   ├── Search
│   ├── Library
│   ├── Playback
│   ├── Settings
│   └── Storage
├── Models
│   ├── Library
│   ├── Navigation
│   ├── Search
│   ├── Playback
│   └── Settings
├── Services
│   ├── Library
│   ├── Search
│   ├── Artwork
│   ├── Metadata
│   ├── Settings
│   └── UI
├── ViewModels
│   ├── Shell
│   ├── Pages
│   ├── Components
│   ├── Dialogs
│   └── Panels
├── Views
│   ├── Shell
│   ├── Pages
│   ├── Components
│   │   ├── Chrome
│   │   ├── Cards
│   │   ├── Lists
│   │   ├── Settings
│   │   ├── Surfaces
│   │   └── Overlays
│   ├── Dialogs
│   ├── Panels
│   └── Resources
│       ├── Theme
│       ├── Typography
│       ├── Spacing
│       ├── Motion
│       ├── Icons
│       └── Strings
└── Utilities
```

### Asset placement rules

- [ ] All icons go under `src/App/Assets/Icons`
- [ ] Brand art goes under `src/App/Assets/Brand`
- [ ] Store package art goes under `src/App/Assets/Store`
- [ ] Fallback album/video art goes under `src/App/Assets/Artwork`
- [ ] Do not keep final production icons scattered in XAML geometry unless they are explicitly tokenized and reused

### Text placement rules

- [ ] Final user-facing text must come from a standard string source
- [ ] Recommended target location: `src/App/Views/Resources/Strings`
- [ ] All screen titles, button labels, empty states, menu items, and tooltips should be tokenized
- [ ] Contextual runtime text can be composed in code, but base phrases must still come from standard resources

---

## 4. File Ownership Rules

Use these rules to prevent future architecture drift.

### Shell

- `src/App/Views/Shell/MainWindow.axaml`
- `src/App/Views/Shell/MainWindow.*.cs`

Shell should own:

- app window lifecycle
- top-level navigation host
- overlay host
- window chrome behavior
- global shortcuts registration

Shell should not own:

- page-specific layout logic
- playlist rendering details
- search filtering logic
- settings field validation

### Pages

Pages should own:

- screen layout
- screen-specific interactions
- binding to their page viewmodel

Pages should not own:

- global services orchestration
- store packaging logic
- unrelated dialogs or panel host logic

### Components

Components should own:

- reusable visual units
- reusable small interaction patterns
- shared chrome pieces

### Services

Services should own:

- metadata loading
- search indexing
- artwork lookup/cache
- persistence
- playback-adjacent business logic

### Resource dictionaries

Resource dictionaries should own:

- color tokens
- spacing tokens
- radius tokens
- typography tokens
- motion tokens
- shared control styles
- standardized string resources

---

## 5. Delivery Phases

## Phase 0: Lock Baseline And Define Architecture

Status: `[~]`

### Goal

Freeze the current app as a stable baseline and define the real target architecture before major UI work.

### Files to inspect/update

- `src/App/Views/Shell/MainWindow.axaml`
- `src/App/Views/Shell/MainWindow.*.cs`
- `src/App/Views/Pages/StartPage.axaml`
- `src/App/Views/Pages/PlayerPage.axaml`
- `src/App/ViewModels/Shell/MainViewModel*.cs`
- `src/App/ViewModels/Pages/StartPageViewModel.cs`
- `src/App/Core/Navigation/AppRoute.cs`

### Checklist

- [x] Confirm backup exists
- [ ] Create target navigation map
- [ ] Define final page list:
  - Home
  - Music Library
  - Video Library
  - Playlists
  - Favorites
  - Search Results
  - Settings
  - Player
- [ ] Decide which screens are full pages vs panels vs dialogs
- [ ] Document state ownership:
  - shell state
  - playback state
  - page state
  - persistent settings state
- [ ] Remove ambiguity around "playlist" vs "queue" vs "history"

### Watch out for

- Do not start building pages before the route model is expanded
- Do not keep adding features to `StartPage` if it will be replaced by `HomePage`
- Do not let `MainWindow` become the dumping ground for everything

### Test cases

- [ ] App launches with no media
- [ ] App returns to landing screen after playback stop/close
- [ ] Existing playback, overlays, and navigation still work
- [ ] No regression in resize smoothness

### Exit criteria

- [ ] Approved information architecture
- [ ] Approved route model
- [ ] Approved page/panel/dialog split

---

## Phase 1: Design System Cleanup

Status: `[ ]`

### Goal

Create one coherent design system so every future screen uses standard resources, not inline values.

### Existing files to refactor

- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/Colors.axaml`
- `src/App/Views/Resources/Colors.json`
- `src/App/Views/Resources/AppColors.cs`
- `src/App/Views/Resources/Spacing.axaml`
- `src/App/Views/Resources/Sizes.axaml`
- `src/App/Views/Resources/Radius.axaml`
- `src/App/Views/Resources/Typography.axaml`
- `src/App/Views/Resources/Motion.axaml`
- `src/App/Views/Resources/UiConstants.cs`
- `src/App/Views/Resources/Token.cs`

### New files to add

- `src/App/Views/Resources/Strings/Strings.en-US.axaml`
- `src/App/Views/Resources/Theme/Surfaces.axaml`
- `src/App/Views/Resources/Theme/Controls.axaml`
- `src/App/Views/Resources/Theme/Navigation.axaml`
- `src/App/Views/Resources/Theme/Cards.axaml`
- `src/App/Views/Resources/Theme/Settings.axaml`

### Checklist

- [ ] Merge duplicate color concepts (`Accent` vs `AppAccent`)
- [ ] Align code constants and XAML constants
- [ ] Remove page-local color systems where possible
- [ ] Add semantic tokens:
  - `Color.BrandPrimary`
  - `Color.SurfaceBase`
  - `Color.SurfaceRaised`
  - `Color.SurfaceGlass`
  - `Color.TextPrimary`
  - `Color.TextSecondary`
  - `Color.TextMuted`
  - `Color.BorderSubtle`
  - `Color.BorderStrong`
  - `Color.StateHover`
  - `Color.StatePressed`
  - `Color.StateFocus`
- [ ] Add semantic spacing tokens for shells, sections, cards, dialogs
- [ ] Add shared text styles for:
  - page title
  - section title
  - body
  - caption
  - overline
  - nav label
  - card title
  - metadata row
- [ ] Move strings out of XAML into shared resource keys
- [ ] Decide and document brand accent usage

### Watch out for

- `UiConstants.cs` and XAML tokens must not drift
- Avoid creating too many ultra-specific tokens
- Keep semantic tokens above component tokens
- Do not use inline text for final UI once string resources exist

### Test cases

- [ ] Theme resources resolve without missing key exceptions
- [ ] All shared buttons still render correctly
- [ ] Header/controls box still render correctly
- [ ] Focus-visible states remain accessible
- [ ] Resize still stays smooth

### Recovery steps if standard not met

- If a page still uses inline colors, replace them with semantic tokens before phase sign-off
- If typography values repeat 3 or more times, promote them to shared styles
- If multiple controls need the same structure, convert to reusable component style

### Exit criteria

- [ ] No core screen depends on private page-local color system
- [ ] Tokens are the primary source of layout, color, motion, and text style

---

## Phase 2: Shell Refactor

Status: `[ ]`

### Goal

Replace the current two-screen mental model with a real product shell.

### Files to refactor heavily

- `src/App/Views/Shell/MainWindow.axaml`
- `src/App/Views/Shell/MainWindow.axaml.cs`
- `src/App/Views/Shell/MainWindow.Lifecycle.cs`
- `src/App/Views/Shell/MainWindow.State.cs`
- `src/App/ViewModels/Shell/MainViewModel.cs`
- `src/App/Core/Navigation/AppRoute.cs`
- `src/App/Core/Navigation/NavigationService.cs`

### New views/viewmodels expected

- `src/App/Views/Components/Chrome/AppSidebar.axaml`
- `src/App/Views/Components/Chrome/AppTopBar.axaml`
- `src/App/Views/Components/Chrome/MiniPlayerBar.axaml`
- `src/App/ViewModels/Components/AppSidebarViewModel.cs`
- `src/App/ViewModels/Components/AppTopBarViewModel.cs`
- `src/App/ViewModels/Components/MiniPlayerBarViewModel.cs`

### Checklist

- [ ] Expand `AppRoute` beyond `Start` and `Player`
- [ ] Add main content host for page swapping
- [ ] Add persistent left navigation rail
- [ ] Add top bar with global search and utility actions
- [ ] Add bottom mini-player zone
- [ ] Keep player overlays working when in playback routes
- [ ] Ensure shell supports both desktop-wide content pages and immersive player mode

### Watch out for

- Shell refactor must not break playback renderer lifecycle
- Overlay host must remain outside clipped content where needed
- Do not regress window resize/corner behavior
- Navigation should not instantiate pages blindly on every switch if state persistence matters

### Test cases

- [ ] Shell opens and navigates between all routes
- [ ] Switching pages does not freeze or flicker
- [ ] Video playback still enters immersive player correctly
- [ ] Returning home from player preserves app stability
- [ ] Window drag, resize, maximize, restore still work

### Exit criteria

- [ ] A stable shell exists independent of `StartPage`
- [ ] Navigation rail and top search bar render consistently
- [ ] Old landing page is no longer the architectural center of the app

---

## Phase 3: Domain Model And Data Layer

Status: `[ ]`

### Goal

Build real product data models for search, libraries, favorites, history, and queue.

### New model/service areas

- `src/App/Models/Library`
- `src/App/Models/Search`
- `src/App/Models/Settings`
- `src/App/Services/Library`
- `src/App/Services/Search`
- `src/App/Services/Artwork`
- `src/App/Services/Metadata`

### Core entities

- [ ] `MediaItem`
- [ ] `AlbumItem`
- [ ] `ArtistItem`
- [ ] `PlaylistSummary`
- [ ] `SearchResultItem`
- [ ] `RecentItem`
- [ ] `FavoriteItem`
- [ ] `PlaybackQueueItem`

### Checklist

- [ ] Create media library indexing strategy
- [ ] Define file/folder import flow
- [ ] Define metadata extraction path
- [ ] Define artwork fallback path
- [ ] Define favorites persistence
- [ ] Define history persistence
- [ ] Define search indexing fields

### Watch out for

- Do not mix UI-only card data with canonical library data
- Do not make playlist the same thing as history
- Do not depend on file path alone when richer metadata exists

### Test cases

- [ ] Adding media folder creates deterministic library entries
- [ ] Missing tags still produce usable UI data
- [ ] Missing artwork falls back cleanly
- [ ] Corrupt files do not crash indexing
- [ ] Favorites persist across restart
- [ ] History order is correct across restart

### Exit criteria

- [ ] Product data model exists independent of page layout
- [ ] Search, library, favorites, and history have real backing services

---

## Phase 4: Asset And Icon System

Status: `[ ]`

### Goal

Move all product art, icon assets, and store assets into a clean standard structure.

### Existing files to preserve or relocate

- `src/App/Assets/simba-logo.svg`
- `src/App/Views/Resources/AppIcon.ico`
- `src/App/generate-app-icons.ps1`
- `src/App/Package.appxmanifest`

### Checklist

- [ ] Move brand icon source to `Assets/Brand`
- [ ] Keep MSIX-generated PNG outputs under `Assets/Store`
- [ ] Define icon naming rules
- [ ] Separate brand assets from UI action icons
- [ ] Add fallback cover art assets for audio/video cards
- [ ] Add empty-state illustrations only if consistent with premium visual direction

### Watch out for

- Do not hardcode filesystem paths to old icon locations
- Keep manifest references updated after asset moves
- Ensure all store-required image sizes are generated and valid

### Test cases

- [ ] Window icon loads in debug and publish builds
- [ ] MSIX package contains all required assets
- [ ] SVG assets render correctly in Avalonia
- [ ] Missing assets fail loudly during dev, not silently in store build

### Exit criteria

- [ ] All final assets live in standard asset folders
- [ ] No production icons depend on ad hoc scattered files

---

## Phase 5: Shared Card And List System

Status: `[ ]`

### Goal

Build reusable media presentation components for both card view and list view.

### New component targets

- `src/App/Views/Components/Cards/MediaCard.axaml`
- `src/App/Views/Components/Cards/MediaCardCompact.axaml`
- `src/App/Views/Components/Cards/AlbumCard.axaml`
- `src/App/Views/Components/Cards/PlaylistCard.axaml`
- `src/App/Views/Components/Lists/MediaListRow.axaml`
- `src/App/Views/Components/Lists/PlaylistRow.axaml`
- `src/App/Views/Components/Lists/SearchResultRow.axaml`

### Checklist

- [ ] Build one card system for image + title + metadata + actions
- [ ] Build one list row system for dense/tabular layouts
- [ ] Support hover, focus, pressed, selected, playing, favorite states
- [ ] Support context menu entry points
- [ ] Support thumbnail fallback and skeleton loading
- [ ] Support both audio and video metadata layouts
- [ ] Support variable size presets instead of one-off sizing

### Watch out for

- Do not clone `RecentCard` logic into five separate views
- Avoid card-specific inline styles in page XAML
- Keep artwork loading async and cache-aware

### Test cases

- [ ] Long titles trim correctly
- [ ] Missing metadata does not break alignment
- [ ] Cards remain stable during rapid resize
- [ ] Keyboard focus ring is visible on every interactive card/list row
- [ ] Right-click works from both card and list variants

### Exit criteria

- [ ] Pages consume shared card/list components instead of rolling their own

---

## Phase 6: Home Dashboard

Status: `[ ]`

### Goal

Replace `StartPage` with a true home dashboard.

### Files to replace or migrate from

- `src/App/Views/Pages/StartPage.axaml`
- `src/App/Views/Pages/StartPage.axaml.cs`
- `src/App/ViewModels/Pages/StartPageViewModel.cs`

### New targets

- `src/App/Views/Pages/HomePage.axaml`
- `src/App/Views/Pages/HomePage.axaml.cs`
- `src/App/ViewModels/Pages/HomePageViewModel.cs`

### Home sections

- [ ] Welcome header
- [ ] Global search shortcut entry
- [ ] Primary actions: open file, open folder, scan library
- [ ] Recent items
- [ ] Continue listening / continue watching
- [ ] Pinned playlists
- [ ] Favorites preview
- [ ] Optional featured mixes/recently added section

### Watch out for

- Do not rebuild home as another page-local styling island
- Avoid giant page-specific code-behind except resize-critical behavior
- Do not use fake counts or fake content in release

### Test cases

- [ ] Home loads with empty library
- [ ] Home loads with recent items only
- [ ] Home loads with large library
- [ ] Search box from home routes correctly
- [ ] Open file/folder commands still work

### Exit criteria

- [ ] Home is the real landing page
- [ ] `StartPage` can be retired or reduced to migration stub

---

## Phase 7: Search

Status: `[ ]`

### Goal

Build app-wide search that feels product-grade.

### New targets

- `src/App/Views/Pages/SearchPage.axaml`
- `src/App/ViewModels/Pages/SearchPageViewModel.cs`
- `src/App/Services/Search/SearchIndexService.cs`
- `src/App/Services/Search/SearchQueryService.cs`
- `src/App/Models/Search/SearchResultItem.cs`

### Search scope

- [ ] songs
- [ ] videos
- [ ] albums
- [ ] artists
- [ ] playlists
- [ ] favorites
- [ ] history

### Checklist

- [ ] Add global search entry in top bar
- [ ] Add debounced search input
- [ ] Add grouped result sections
- [ ] Add no-results state
- [ ] Add recent searches
- [ ] Add keyboard navigation
- [ ] Add enter-to-play or enter-to-open behavior

### Watch out for

- Search should not block UI thread
- Search should not rescan disk on every keypress
- Search must be tolerant of punctuation, spacing, partial names

### Test cases

- [ ] Search with exact file name
- [ ] Search with partial title
- [ ] Search with uppercase/lowercase mismatch
- [ ] Search with missing metadata
- [ ] Search while playback is ongoing
- [ ] Search with 10k+ indexed items stays responsive

### Exit criteria

- [ ] Search is globally usable and not just visual chrome

---

## Phase 8: Music Library And Video Library

Status: `[ ]`

### Goal

Build separate, real library experiences for audio and video.

### New targets

- `src/App/Views/Pages/MusicLibraryPage.axaml`
- `src/App/Views/Pages/VideoLibraryPage.axaml`
- `src/App/ViewModels/Pages/MusicLibraryPageViewModel.cs`
- `src/App/ViewModels/Pages/VideoLibraryPageViewModel.cs`

### Music library requirements

- [ ] Tabs or filters for songs, albums, artists, genres
- [ ] Card/list toggle
- [ ] Sort and filter controls
- [ ] Multi-select support where useful
- [ ] Play/add/queue/favorite context actions

### Video library requirements

- [ ] Recently added
- [ ] folders/collections if needed
- [ ] duration, resolution, type metadata
- [ ] poster/thumbnail fallback
- [ ] card/list toggle

### Watch out for

- Avoid separate styling systems for music and video pages
- Do not duplicate sorting/filtering infrastructure
- Keep data loading incremental where necessary

### Test cases

- [ ] Card view works with 0 items
- [ ] Card view works with 1000+ items
- [ ] List view sorts correctly
- [ ] Toggle between card/list does not lose selection unexpectedly
- [ ] Context actions work from both views

### Exit criteria

- [ ] Libraries are usable as standalone product screens

---

## Phase 9: Playlist, Queue, Favorites, History

Status: `[ ]`

### Goal

Turn playlist-related features into a complete product subsystem.

### Existing files to refactor

- `src/App/Views/Components/Panels/PlaylistPanel.axaml`
- `src/App/Views/Dialogs/PlaylistDialog.axaml`
- `src/App/ViewModels/Shell/MainViewModel.Playlist.cs`

### New/expanded targets

- `src/App/Views/Pages/PlaylistsPage.axaml`
- `src/App/Views/Pages/FavoritesPage.axaml`
- `src/App/Views/Pages/HistoryPage.axaml`
- `src/App/ViewModels/Pages/PlaylistsPageViewModel.cs`
- `src/App/ViewModels/Pages/FavoritesPageViewModel.cs`
- `src/App/ViewModels/Pages/HistoryPageViewModel.cs`

### Checklist

- [ ] Define queue behavior clearly
- [ ] Support add to queue
- [ ] Support save playlist
- [ ] Support reorder playlist
- [ ] Support remove from playlist
- [ ] Support favorite/unfavorite
- [ ] Support history clear/remove
- [ ] Support "play next" and "play later"

### Watch out for

- Queue, playlist, and history must remain distinct in code and UI
- Avoid duplicated logic between panel and page variants
- Reordering must update both UI and underlying state reliably

### Test cases

- [ ] Queue survives active playback state changes
- [ ] Playlist reorder is stable
- [ ] Save/load playlist works
- [ ] Favorites persist across restarts
- [ ] History order is correct and can be cleared safely

### Exit criteria

- [ ] Playlist/queue/favorites/history are all product-grade features

---

## Phase 10: Audio Now-Playing Experience

Status: `[ ]`

### Goal

Build a dedicated audio experience matching the mockups instead of reusing video layout with hidden video.

### New targets

- `src/App/Views/Pages/AudioPlayerPage.axaml`
- `src/App/ViewModels/Pages/AudioPlayerPageViewModel.cs`
- `src/App/Views/Components/Chrome/NowPlayingHeader.axaml`
- `src/App/Views/Components/Chrome/PlaybackTransportStrip.axaml`

### Checklist

- [ ] Album art region
- [ ] title/artist/album metadata block
- [ ] progress + transport controls
- [ ] quick favorite / queue / more actions
- [ ] volume and output controls
- [ ] optional simple waveform visualization

### Watch out for

- Do not degrade keyboard and mouse transport reliability
- Avoid visual over-animation during playback
- Keep consistent with mini-player and queue state

### Test cases

- [ ] Audio file opens directly into audio mode
- [ ] No-video media never shows dead video chrome
- [ ] Transport and seek remain in sync
- [ ] Album art fallback works

### Exit criteria

- [ ] Audio playback has a dedicated, intentional UX

---

## Phase 11: Video Player Refinement

Status: `[ ]`

### Goal

Polish video mode into a shippable premium experience.

### Existing files to refine

- `src/App/Views/Pages/PlayerPage.axaml`
- `src/App/Views/Components/Chrome/HeaderBar.axaml`
- `src/App/Views/Components/Chrome/ControlsBox.axaml`
- `src/App/Views/Components/Overlays/*`

### Checklist

- [ ] Top bar visual cleanup
- [ ] Better right-side quick actions
- [ ] Refined seek bar and chapter marks
- [ ] Cleaner info hierarchy
- [ ] Better quick settings access
- [ ] Better PIP affordance
- [ ] Consistent subtitle/audio/equalizer access

### Watch out for

- Do not harm playback smoothness for visual polish
- Keep overlay transitions light and responsive
- Maintain fullscreen and PIP stability

### Test cases

- [ ] Mouse idle hide/show works
- [ ] Fullscreen works
- [ ] PIP works
- [ ] Right click menu works
- [ ] Subtitle/audio track switching works
- [ ] High-frequency seeking does not desync UI

### Exit criteria

- [ ] Video mode feels premium and stable under real usage

---

## Phase 12: Settings System

Status: `[ ]`

### Goal

Unify quick settings and deep settings into a complete settings product.

### Existing files to refactor

- `src/App/Views/Dialogs/PreferencesWindow.axaml`
- `src/App/Views/Dialogs/PreferencesDialog.axaml`
- `src/App/Views/Dialogs/SubtitleSettingsDialog.axaml`
- `src/App/Views/Components/Panels/EqualizerPanel.axaml`

### New targets

- `src/App/Views/Pages/SettingsPage.axaml`
- `src/App/ViewModels/Pages/SettingsPageViewModel.cs`
- `src/App/Views/Components/Settings/SettingRow.axaml`
- `src/App/Views/Components/Settings/SettingsSection.axaml`

### Settings categories

- [ ] Playback
- [ ] Audio
- [ ] Video
- [ ] Subtitles
- [ ] Library
- [ ] Search
- [ ] Theme / appearance
- [ ] Shortcuts
- [ ] Privacy / diagnostics
- [ ] About

### Watch out for

- Avoid duplicated settings UIs in multiple dialogs and windows
- Quick settings should be lightweight, full settings should be comprehensive
- Ensure every setting actually persists and reloads

### Test cases

- [ ] Toggle settings persist
- [ ] Numeric settings persist
- [ ] Reset to defaults works
- [ ] Corrupt settings file recovers safely
- [ ] Per-file and global settings stay separate where intended

### Exit criteria

- [ ] Settings system is coherent, persistent, and complete

---

## Phase 13: Text, Localization, Accessibility, Input

Status: `[ ]`

### Goal

Remove inline text, improve accessibility, and make the app keyboard-clean.

### Checklist

- [ ] Move strings into standard resource files
- [ ] Add access keys/tooltips where appropriate
- [ ] Audit automation names
- [ ] Ensure focus order on all forms and pages
- [ ] Audit keyboard shortcuts for conflicts
- [ ] Add screen-reader-friendly names to core controls

### Watch out for

- Do not keep tooltips and button labels out of sync
- Do not leave text in XAML after strings system is established
- Avoid shortcut collisions between shell and player mode

### Test cases

- [ ] Tab order is logical on every major page
- [ ] Search box is keyboard reachable
- [ ] Cards are keyboard activatable
- [ ] Dialogs trap focus correctly
- [ ] Screen-reader names exist for major actions

### Exit criteria

- [ ] No important production text remains hardcoded inline
- [ ] Accessibility baseline is acceptable for store release

---

## Phase 14: Error Handling, Empty States, Debuggability

Status: `[ ]`

### Goal

Make the product resilient under real-world failure conditions.

### Checklist

- [ ] Add loading states for index/search/library views
- [ ] Add no-results states
- [ ] Add file-missing states
- [ ] Add unsupported-media states
- [ ] Add import/index failure notifications
- [ ] Add retry flows where practical
- [ ] Add structured logging around critical operations

### Watch out for

- No silent failures in import/search/settings persistence
- No blank screens with no explanation
- Errors must help the user recover

### Test cases

- [ ] Missing file path
- [ ] Unsupported media format
- [ ] Broken subtitle file
- [ ] Corrupt playlist file
- [ ] Search service unavailable or empty index
- [ ] Settings file invalid JSON

### Exit criteria

- [ ] Product remains usable when common failures occur

---

## Phase 15: Windows Store Readiness

Status: `[ ]`

### Goal

Package, validate, and polish the app to Windows Store quality.

### Relevant files

- `src/App/Package.appxmanifest`
- `src/App/App.csproj`
- `src/App/generate-app-icons.ps1`
- `installer/*`
- `publish*`

### Checklist

- [ ] Validate package identity and naming
- [ ] Validate app icons and tiles
- [ ] Validate splash assets
- [ ] Validate screenshots for listing
- [ ] Validate privacy policy and support URL requirements
- [ ] Validate crash handling and app stability
- [ ] Validate app startup time
- [ ] Validate clean install and clean uninstall
- [ ] Validate settings/data paths under AppData
- [ ] Validate no dev/debug placeholders remain

### Store listing package

- [ ] App name
- [ ] Short description
- [ ] Full description
- [ ] Keywords
- [ ] Screenshots
- [ ] Feature list
- [ ] Support email/site
- [ ] Privacy policy URL
- [ ] Versioning plan

### Test cases

- [ ] Install MSIX on clean machine/profile
- [ ] Launch with no media
- [ ] Import library
- [ ] Play local music
- [ ] Play local video
- [ ] Use search
- [ ] Create playlist
- [ ] Change settings
- [ ] Close and relaunch
- [ ] Upgrade existing install
- [ ] Uninstall and verify cleanup expectations

### Exit criteria

- [ ] Store package is valid
- [ ] Product quality is consistent with paid/premium expectations

---

## 6. Code Style Rules For This Refactor

### XAML rules

- [ ] Prefer `Classes` + shared styles over inline styling
- [ ] Use resource keys for colors, radii, spacing, typography
- [ ] Use inline values only for temporary exploration, then remove
- [ ] No final hardcoded UI strings in XAML
- [ ] Use reusable controls for repeated visual patterns

### C# rules

- [ ] Keep page code-behind limited to view concerns and resize-critical behavior
- [ ] Put business logic in services/viewmodels
- [ ] Put reusable UI lookup logic in helpers or component classes
- [ ] Prefer explicit naming over generic names like `Manager2`, `HelperX`
- [ ] Keep async operations cancellable where needed

### Review rules

- [ ] Every PR/phase review checks:
  - architecture
  - visual consistency
  - performance
  - keyboard UX
  - failure behavior
  - token usage

---

## 7. When The Standard Is Not Met

Use this as the correction playbook.

### If styling starts drifting

- [ ] Pause feature work
- [ ] Extract repeated inline values into tokens/styles
- [ ] Replace page-local ad hoc styles with shared component classes

### If shell starts getting overloaded

- [ ] Move logic down into page/component/service ownership
- [ ] Reduce `MainWindow` responsibilities
- [ ] Re-check routing/state boundaries

### If resize becomes stuttery

- [ ] Audit for new `DynamicResource` usage in resize-critical paths
- [ ] Audit for new heavy bindings/converters
- [ ] Audit for new expensive effects/logging on live resize
- [ ] Revert to direct property updates where needed

### If product screens become inconsistent

- [ ] Compare to token set
- [ ] Compare to shared card/list/settings primitives
- [ ] Refactor before adding more screens

### If a feature feels fake

- [ ] Ask whether it has:
  - real data
  - real empty state
  - real error state
  - real persistence
  - real keyboard behavior
  - real context actions

If not, it is not done.

---

## 8. Master QA Checklist

- [ ] Clean startup
- [ ] Fast resize on home, search, library, and player screens
- [ ] No clipped text on 100%, 125%, 150%, 200% display scaling
- [ ] Keyboard navigation across shell works
- [ ] Search works with large libraries
- [ ] Playlist/queue/favorites/history persistence works
- [ ] Audio mode and video mode both feel intentional
- [ ] Settings persist correctly
- [ ] App survives missing media and corrupted files
- [ ] Store packaging assets are valid
- [ ] No debug visuals, placeholder text, or dummy actions remain

---

## 9. Recommended Execution Order

Do the phases in this order:

1. Phase 0: Architecture lock
2. Phase 1: Design system cleanup
3. Phase 2: Shell refactor
4. Phase 3: Domain/data layer
5. Phase 4: Asset/icon system
6. Phase 5: Shared card/list system
7. Phase 6: Home dashboard
8. Phase 7: Search
9. Phase 8: Libraries
10. Phase 9: Playlist/queue/favorites/history
11. Phase 10: Audio now-playing
12. Phase 11: Video player refinement
13. Phase 12: Settings system
14. Phase 13: Text/accessibility/input
15. Phase 14: Error handling/debuggability
16. Phase 15: Windows Store readiness

---

## 10. Definition Of Done For The Full Product

The product is done only when:

- [ ] every major screen is real and connected to real data
- [ ] no critical screen depends on inline text/colors/styles
- [ ] no critical screen is a one-off architecture island
- [ ] shell/navigation/search/library/settings all work together
- [ ] app remains smooth during resize and playback
- [ ] app is stable enough for public users
- [ ] packaging/listing/store assets are complete
- [ ] product can be confidently published to Windows Store

---

## 11. Immediate Next Step After This Guide

Create a second execution file derived from this master guide:

- `md/2026-07-13/phase-01-action-plan.md`

That file should break Phase 0 and Phase 1 into implementation-sized tasks, owners, dependencies, and concrete commits.

# UNIVERSAL PROMPT ENGINE: AXAML ↔ Penpot

> **For ANY AI model** (Claude, GPT, Gemini, etc.) — the master prompt engineering guide for converting Avalonia UI to/from Penpot designs.  
> **For ANY project** — no hardcoded paths, no specific element assumptions, no project-specific config needed.  
> **For ANY person** — whether you're a designer reading code, a developer prototyping UI, or an AI agent tooling.

---

## INTELLIGENT ARRANGEMENT IN PENPOT

### Single Canvas Rule (Figma-style)

ALL screens/boards go on ONE shared page called **"Avalonia - Design System"**. Do NOT create separate Penpot pages per screen — like Figma, everything lives on the same canvas, arranged spatially.

Each conversion adds its board to this single page alongside existing ones:
- Use `storage.prepareSinglePage()` (not `preparePage()`) — this **appends** boards without clearing existing content
- Board x-position is auto-offset: `board.x = 40 + counter * (boardWidth + 40)`
- The Model may rearrange boards spatially after all conversions

### Grouping by Category

```
SCREENS          → top-left area, large boards (1280×800)
DIALOGS          → center section, medium boards (600×400)
FLYOUTS/OVERLAYS → right section, smaller boards (400×300)
COMPONENTS       → bottom section, compact boards (as needed)
```

### Layout Principles

| Principle | How to Apply |
|---|---|
| **Top-down hierarchy** | Headers above content, control bars below content |
| **Category grouping** | All flyouts together, all dialogs together — with gaps |
| **Size-aware grid** | Large boards get full width; small boards share rows (2 per row) |
| **Labels** | Add a `storage.createText()` label above each board showing its name |
| **Section backgrounds** | Optional subtle rect behind each category group |
| **Breathing room** | 30–50px padding between groups, 20–24px between items in a group |
| **Alignment** | Left-align in columns, center single boards |

### Example Layout Structure

```
┌──────────────────────────────────────────────────────┐
│  🖥️  SCREENS                                         │
│  ┌──────────────┐  ┌──────────────┐                 │
│  │ PlaybackScrn  │  │  StartPage   │                 │
│  └──────────────┘  └──────────────┘                 │
├──────────────────────────────────────────────────────┤
│  💬  DIALOGS                                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │ AboutDlg │  │  PrefsDlg│  │SubtitleD │           │
│  └──────────┘  └──────────┘  └──────────┘           │
├──────────────────────────────────────────────────────┤
│  🧩  COMPONENTS                                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐           │
│  │ HeaderBar│  │  SeekBar  │  │ControlBar│           │
│  └──────────┘  └──────────┘  └──────────┘           │
└──────────────────────────────────────────────────────┘
```

### Code Pattern for Arrangement

```javascript
// Example: adding a label above a board
var label = storage.createText('_lbl_BoardName', 'BoardName', 13, 600, '#B0B0B0', 0.85, 'center');
label.x = boardX + boardW / 2;
label.y = boardY - 22;
root.appendChild(label);
storage.centerTextX(label, boardX + boardW / 2);
```

### Model Adjustment Rule

The converter outputs AXAML-native coordinates, but the model **may reposition elements** for visual polish when the main shape structure is correct. This is **expected** — like how Figma/Stitch intelligently aligns and spaces elements.

| Situation | Allowed Adjustment |
|---|---|
| Button at x=0 but clearly should be centered | Re-center horizontally within parent |
| Elements clipping into board edges | Add consistent padding |
| Stacked elements with no spacing | Distribute with even gaps |
| Overlapping labels | Shift for readability |

**What must NOT change**: shape types, colors, sizes, font properties, SVG paths, gradient stops. Only **position (x/y)** and **spacing** may be adjusted.

**No manual correction files**: Do NOT create separate fix/correction JS files (e.g., `xxx_corrected.js`). The model must produce correct output directly using its own capabilities. If the output is wrong, fix the converter or adjust the approach — never leave a manual fix file alongside the real output.

### Pre-Execution Verification Checklist

Before executing generated JS in Penpot, verify these items to avoid alignment bugs:

| # | Check | How to Verify | Example Failure |
|---|---|---|---|
| 1 | **Button dimensions** | Read the AXAML style selector (e.g., `Button.start-page-button`) for `MinWidth`, `Height`, `Padding`. Don't guess sizes. | Button used 120px width but actual `MinWidth="140"` |
| 2 | **Button colors** | Read the style selector for `Background`/`Foreground` values. Check modifier styles (e.g., `start-page-suggested-action`). | "Open..." used purple (#6C5CE7) but style said `#E5E5E5` |
| 3 | **Text centering** | `align='center'` only works when the text element has a **fixed width** matching its container. With `growType='auto-width'`, the X position places the **left edge**, not center. | Title placed at x=640 but appeared ~180px right of center |
| 4 | **Use `centerTextX()`** | Always call `storage.centerTextX(text, centerX)` **after** appending text to the board (width is 0 before append). | Title centered by hand calculation failed |
| 5 | **Code-behind elements** | Check `.axaml.cs` for programmatic elements (e.g., `RecentFilesList` buttons created in `RebuildRecentFiles()`). Don't assume everything is in AXAML markup. | Recent files rendered as plain text but code-behind creates Button elements with Padding=(40,4) |
| 6 | **Dummy data context** | Match dummy data to the app domain. A media player gets `.mp4`/`.mkv` filenames, not `.pdf`/`.docx`. Check the app's `videoExtensions` list. | Recent files showed "report.pdf" instead of "Big Buck Bunny.mkv" |
| 7 | **Resize text to match button** | For button text: call `txt.resize(btnW, lineHeight)` so `align='center'` actually centers within the button width. | Text was auto-width, center alignment had no effect |

### Post-Execution Verification

After running JS in Penpot, always read back shape positions to confirm:

```javascript
// Read all shapes positions to verify alignment
var page = penpot.root.children[0];  // first page
var children = page.children;
var result = [];
for (var i = 0; i < children.length; i++) {
  var s = children[i];
  result.push(s.name + ' | x=' + Math.round(s.x) + ' y=' + Math.round(s.y)
    + ' w=' + Math.round(s.width) + ' h=' + Math.round(s.height));
}
return result.join('\n');
```

Check that:
- All centered elements: `x + width/2 === centerX` (not just `x === centerX`)
- Button text: same `x` and `width` as the button background
- Colors: match the style selector values (check `.fills[0].fillColor`)
- Recent files: positioned with correct left padding matching code-behind

### Library Assets & Design Tokens

Penpot's Library stores reusable **Colors**, **Typography**, and **Components** — analogous to `Colors.axaml` and `Typography.axaml` resource dictionaries in Avalonia. **Design Tokens** are typed properties (color, spacing, sizing) grouped into sets and themes.

**When generating shapes, prefer library references over hardcoded values:**

| Scenario | Hardcoded (bad) | Library reference (good) |
|---|---|---|
| Color fills | `fills: [{ fillColor: '#6CB4FF' }]` | Reference a Library Color by name |
| Typography | `fontSize: 28, fontWeight: '700'` | Apply a Library Typography style |
| Component reuse | Duplicate shapes | Create/instance a Library Component |

**Setup:** Run `storage.createLibraryAssets()` once to import all colors from `Colors.axaml` and typographies from `Typography.axaml` into the Penpot Library. This creates:
- ~70+ Colors (grays, accent, app surface/overlay/hover/divider colors)
- 7 Typography styles (caption, body1/2, subtitle1, headline6/4/2)
- 2 Design Token Sets (`cine/colors`, `cine/typography`)

**One-time execution after page setup:**
```javascript
// Import all AXAML resources as Penpot Library Assets + Tokens
storage.createLibraryAssets();
```

**Usage in shape generation:**
```javascript
// Instead of: txt.fills = [{ fillColor: '#E5E5E5' }]
// Reference library color (check penpot.library.local.colors[i].name)
// For now, use hex values — library reference API TBD

// Apply library typography:
var typography = penpot.library.local.typographies.find(function(t) {
  return t.name === 'Typography/body1';
});
if (typography) typography.applyToText(txt);
```

---

## What This Toolkit Does (Universal)

| Direction | Description |
|---|---|
| **AXAML → Penpot** | Convert ANY Avalonia `.axaml` screen to editable Penpot shapes (board+rects+texts+paths+SVGs+gradients) |
| **Penpot → AXAML** | Read Penpot shapes back → generate Avalonia `.axaml` code |
| **Intelligence** | Auto-detects lists, playlists, sliders, tabs, inputs → adds realistic dummy data |
| **Discovery** | Auto-finds ALL `.axaml` files in ANY directory structure |
| **Resources** | Resolves `{StaticResource}`, `{DynamicResource}`, gradients, CSS classes, all resource types |
| **Any-Element Fallback** | Unknown/custom elements get rendered via generic handler (text + rect + children) |

---

## Package Installation

```bash
cd axaml-to-penpot
npm install
```

**Dependencies (in package.json):**
| Package | Version | Purpose |
|---|---|---|
| `fast-xml-parser` | `^5.9.3` | Parse AXAML XML to JavaScript objects |
| `fdir` | `^6.5.0` | Fast recursive directory traversal for discovery |

**To add to ANY project:**
```bash
# Option A: Copy the entire axaml-to-penpot folder into your project
cp -r axaml-to-penpot /path/to/your/project/designs/

# Option B: Run from anywhere with --project-root pointing to your project
node /path/to/axaml-to-penpot/convert.mjs MyScreen 1280 800 --project-root /path/to/your/project

# Option C: Install globally (symlink)
cd axaml-to-penpot && npm link
# Then run: axaml-to-penpot MyScreen 1280 800 --project-root /path/to/your/project
```

---

## MASTER PROMPT — Universal AXAML → Penpot

**Use this as the base prompt for ANY conversion task. Customize the bracketed parts.**

```
You are converting an Avalonia AXAML UI file to Penpot shapes via MCP.

CONTEXT:
- Project root: [PROJECT_PATH]
- Screen/component to convert: [SCREEN_NAME]
- Canvas size: [WIDTH]×[HEIGHT] (default: 1280×800 for desktop, 360×640 for mobile)

STEPS:
1. DISCOVER the project:
   Run: node convert.mjs --list --project-root "[PROJECT_PATH]"
   This finds ALL .axaml files in the project automatically.

2. CONVERT the screen (one file at a time — no batch mode):
   Run: node convert.mjs [SCREEN_NAME] [WIDTH] [HEIGHT] --project-root "[PROJECT_PATH]"
   (OR use --axaml-path "[FULL_PATH]" for a specific file)

   Each conversion creates:
   - One Penpot page: `Avalonia - [ScreenName]`
   - One board at W×H
   - All shapes rendered inside the board

3. REGISTER helpers (once per Penpot session):
   Copy the ENTIRE content of helpers.js
   Execute via MCP: execute_code (type: script)

4. EXECUTE the generated JS:
   Take the stdout from step 2. Execute via MCP: execute_code (type: script)

5. VERIFY:
   Execute via MCP: penpotUtils.shapeStructure(penpot.currentPage, 4)
   Check: shapes exist? positioned correctly? fonts applied?

6. DEBUG if needed:
   - Are shapes at y > canvas height? → Layout engine bug, check stacking
   - Font sizes all default? → Typography class resolution check needed
   - Missing shapes? → Check if elements are in SKIP_TAGS
   - Gradients missing? → Check resolver output for gradient count
```

---

## ELEMENT COVERAGE (All Avalonia Elements)

The converter handles EVERY element type through classification + generic fallback:

### Container Elements (Layout)
| AXAML Tag | Penpot Output | Notes |
|---|---|---|
| `Grid` | Background rect + children | Parses RowDefinitions (Auto/\*/pixel); tracks Grid.Row |
| `StackPanel` | Background rect + children | Vertical/Horizontal stacking with Spacing |
| `Border` | Rectangle with fill/stroke/radius | Margin-aware sizing (1256×776 with Margin="12") |
| `DockPanel` | Rect + children | Last child fills remaining space |
| `WrapPanel` | Rect + children | Horizontal wrapping layout |
| `ScrollViewer` | Rect with content | Scrollable content area |
| `Viewbox` | Rect + children | Scaled content |
| `Canvas` | Rect + children | Absolute positioning |
| `UniformGrid` | Rect + children | Equal-sized grid cells |
| `Panel` | Background rect + children | Generic panel |

### Text Elements
| AXAML Tag | Penpot Output | Notes |
|---|---|---|
| `TextBlock` | `createText()` + `font.applyToText()` | Editable text, correct font weight |
| `Label` | `createText()` | Same as TextBlock |
| `Run` | Text span | Inline text inside TextBlock |
| `Span` | Text span | Inline text inside TextBlock |
| `SelectableTextBlock` | `createText()` | Selectable variant |

### Shape Elements
| AXAML Tag | Penpot Output | Notes |
|---|---|---|
| `Rectangle` | `createRect()` | Fill, stroke, RadiusX, CornerRadius |
| `Ellipse` | `createEllipse()` | Fill only |
| `Path` | `createFromSvg()` | Path.Data → SVG import |
| `PathIcon` | `createFromSvg()` | Icon as SVG path |
| `Image` | `createShapeFromSvgWithImages()` | Raster image placeholder |
| `Line` | `createRect()` (thin) | Horizontal/vertical line |

### Interactive Elements
| AXAML Tag | Penpot Output | Intelligence |
|---|---|---|
| `Button` | Rect (bg) + Text (label) | Default 320×40, radius 20, semi-transparent fill |
| `ToggleButton` | Same as Button | Checked state visual |
| `RepeatButton` | Same as Button | Repeat variant |
| `CheckBox` | Rect (box) + Text (label) + check mark | **NEW**: Checkbox pattern with ✓ |
| `RadioButton` | Ellipse (circle) + Text (label) + dot | **NEW**: Radio pattern with ● |
| `TextBox` | Rect (input bg) + Text (placeholder) | **NEW**: Input field with watermark |
| `PasswordBox` | Same as TextBox | Password placeholder |
| `ComboBox` | Rect + Text + dropdown arrow | **NEW**: Dropdown with arrow ▼ |
| `Slider` | Rect (track bg) + Rect (track fill) + Ellipse (thumb) | **NEW**: Filled slider bar |
| `ProgressBar` | Rect (track bg) + Rect (fill) | **NEW**: Progress at 65% |

### Collection Elements
| AXAML Tag | Penpot Output | Intelligence |
|---|---|---|
| `ListBox` | 5 dummy items (bg rects + text) | **NEW**: Context-aware mock data |
| `ListView` | Same as ListBox | With column support |
| `ItemsControl` | Same as ListBox | Generic item repeater |
| `DataGrid` | Table rows with headers | **NEW**: 3 sample data rows |
| `ComboBox` (dropdown) | 5 items below input | Dropdown list |

### Navigation Elements
| AXAML Tag | Penpot Output | Intelligence |
|---|---|---|
| `TabControl` | Tab headers + content area | **NEW**: Rendered tab strip |
| `TabItem` | Tab button + content | Individual tab with header |
| `Menu` | Menu bar rect + items | Horizontal menu |
| `MenuItem` | Menu text item | Individual menu entry |
| `ContextMenu` | Floating menu | Right-click context |

### If Your Element Is NOT Listed

The **GENERIC FALLBACK** handler catches everything else:
- If the element has `Text`/`Content`/`Header`/`Title` attribute → renders as Text
- If the element has `Fill`/`Background` attribute → renders as Rectangle + children
- Always recurses into children so nothing is lost

To add explicit support for a new element type:
1. Add its tag to the appropriate classification list in `convert.mjs`
2. Optionally add a `handleXxx()` function in the dispatch section
3. Add it to `isArray()` in the parser config (if siblings possible)

---

## INTELLIGENCE LAYER — Smart Dummy Data

The converter auto-detects context and generates realistic mock content:

### List Context Detection

The `detectListContext()` function parses element names/headers to guess context:

| Keywords Detected | Context | Generated Dummy Data |
|---|---|---|
| `playlist`, `track`, `song`, `music` | Playlist | Song titles + artist names |
| `subtitle`, `language`, `audio` | Subtitles | Language names + "[CC]" |
| `file`, `document`, `recent` | Files | Project document names |
| `setting`, `preference`, `config` | Settings | Setting categories |
| `table`, `grid`, `data` | Table | Column headers + sample rows |
| (anything else) | Generic | Varied item names |

### Playlist Pattern (example generated code)

```javascript
// Playlist item 0 — "Summer Vibes 2024"
var s0_bg = storage.createRect('Playlist_item0_bg', 12, 300, 400, 56, '#FFFFFF', 0.05, 8);
board.appendChild(s0_bg);

// Album art placeholder (colored square)
var s0_art = storage.createRect('Playlist_art0', 20, 308, 40, 40, '#0078D4', 0.6, 4);
board.appendChild(s0_art);

// Track name
var s0_title = storage.createText('Track_0', 'Summer Vibes 2024', 14, 600, '#FFFFFF', 0.9, 'left');
s0_title.x = 72; s0_title.y = 306;
board.appendChild(s0_title);

// Artist name (subtitle)
var s0_artist = storage.createText('Artist_0', 'DJ Cool Breeze', 11, 400, '#FFFFFF', 0.5, 'left');
s0_artist.x = 72; s0_artist.y = 324;
board.appendChild(s0_artist);

// Duration
var s0_duration = storage.createText('Dur_0', '3:42', 11, 400, '#FFFFFF', 0.4, 'right');
s0_duration.x = 370; s0_duration.y = 316;
board.appendChild(s0_duration);
```

### Subtitle Selector Pattern

```javascript
// Subtitle language list with checkmarks
var subtitles = ['English [CC] ✓', 'Spanish', 'French', 'German', 'Japanese'];
// First item has ✓ checkmark, others don't
```

### Volume Slider Pattern

```javascript
// Volume label
var vol_label = storage.createText('Vol_Label', '🔊 Volume', 11, 400, '#FFFFFF', 0.5, 'left');
vol_label.x = 40; vol_label.y = 450;
board.appendChild(vol_label);

// Slider track (background)
var vol_track_bg = storage.createRect('Vol_TrackBg', 100, 456, 160, 4, '#FFFFFF', 0.15, 2);
board.appendChild(vol_track_bg);

// Slider track (filled — 65% volume)
var vol_track_fill = storage.createRect('Vol_TrackFill', 100, 456, 104, 4, '#0078D4', 1, 2);
board.appendChild(vol_track_fill);

// Slider thumb (circle)
var vol_thumb = storage.createEllipse('Vol_Thumb', 196, 448, 20, 20, '#FFFFFF', 1);
board.appendChild(vol_thumb);

// Percentage label
var vol_pct = storage.createText('Vol_Pct', '65%', 11, 400, '#FFFFFF', 0.5, 'left');
vol_pct.x = 268; vol_pct.y = 448;
board.appendChild(vol_pct);
```

### Checkbox Pattern

```javascript
// Checkbox with label
var cb_box = storage.createRect('CB_Box', 40, 100, 20, 20, '#FFFFFF', 0.1, 4);
cb_box.strokes = [{ strokeColor: '#FFFFFF', strokeOpacity: 0.4, strokeWidth: 1.5 }];
board.appendChild(cb_box);

// Check mark (using SVG path)
var check_svg = '<svg viewBox="0 0 20 20"><path d="M5 10l4 4 6-8" stroke="#FFFFFF" stroke-width="2" fill="none"/></svg>';
var check = storage.createFromSvg('CB_Check', check_svg);
check.x = 44; check.y = 104; check.resize(12, 12);
board.appendChild(check);

var cb_label = storage.createText('CB_Label', 'Enable notifications', 14, 400, '#FFFFFF', 0.9, 'left');
cb_label.x = 72; cb_label.y = 102;
board.appendChild(cb_label);
```

### Radio Button Pattern

```javascript
// Radio outer circle
var rb_outer = storage.createEllipse('RB_Outer', 40, 150, 20, 20, null, 0);
rb_outer.strokes = [{ strokeColor: '#FFFFFF', strokeOpacity: 0.4, strokeWidth: 1.5 }];
board.appendChild(rb_outer);

// Radio inner dot (for selected)
var rb_inner = storage.createEllipse('RB_Inner', 46, 156, 8, 8, '#0078D4', 1);
board.appendChild(rb_inner);

var rb_label = storage.createText('RB_Label', 'Light theme', 14, 400, '#FFFFFF', 0.9, 'left');
rb_label.x = 72; rb_label.y = 152;
board.appendChild(rb_label);
```

### Tab Strip Pattern

```javascript
// Tab background bar
var tab_bar = storage.createRect('TabBar', 0, 0, 1280, 48, '#FFFFFF', 0.05);
board.appendChild(tab_bar);

// Active tab
var tab0_bg = storage.createRect('Tab_Active', 0, 0, 100, 48, '#FFFFFF', 0.12);
board.appendChild(tab0_bg);
var tab0_text = storage.createText('Tab0', 'General', 13, 600, '#FFFFFF', 1, 'center');
tab0_text.x = 0; tab0_text.y = 14; tab0_text.w = 100;
board.appendChild(tab0_text);

// Inactive tabs
var inactive_tabs = ['Display', 'Audio', 'Network', 'About'];
for (var i = 0; i < inactive_tabs.length; i++) {
  var tx = 100 + i * 100;
  var t = storage.createText('Tab' + (i+1), inactive_tabs[i], 13, 400, '#FFFFFF', 0.5, 'center');
  t.x = tx; t.y = 14; t.w = 100;
  board.appendChild(t);
}

// Active tab underline
var tab_line = storage.createRect('TabLine', 0, 44, 100, 3, '#0078D4', 1);
board.appendChild(tab_line);
```

### DataGrid / Table Pattern

```javascript
// Table headers
var headers = ['Name', 'Status', 'Date', 'Size'];
var colW = [280, 100, 120, 100];
var hx = 12;
for (var i = 0; i < headers.length; i++) {
  var th = storage.createText('TH_' + i, headers[i], 11, 700, '#FFFFFF', 0.5, 'left');
  th.x = hx; th.y = 200;
  board.appendChild(th);
  hx += colW[i];
}

// Header underline
var th_line = storage.createRect('TH_Line', 12, 220, 576, 1, '#FFFFFF', 0.1);
board.appendChild(th_line);

// Data rows
var rows = [
  ['video_001.mp4', 'Ready', '2026-01-15', '2.4 GB'],
  ['audio_podcast.wav', 'Processing', '2026-02-03', '156 MB'],
  ['image_batch.zip', 'Done', '2026-03-22', '890 MB'],
];
for (var r = 0; r < rows.length; r++) {
  var ry = 228 + r * 28;
  if (r % 2 === 0) {
    var row_bg = storage.createRect('RowBg_' + r, 12, ry, 576, 26, '#FFFFFF', 0.03);
    board.appendChild(row_bg);
  }
  var rx = 12;
  for (var c = 0; c < rows[r].length; c++) {
    var td = storage.createText('TD_' + r + '_' + c, rows[r][c], 12, 400, '#FFFFFF', c === 1 ? 0.6 : 0.8, 'left');
    td.x = rx; td.y = ry + 4;
    board.appendChild(td);
    rx += colW[c];
  }
}
```

---

## GRADIENT SUPPORT

The resolver automatically parses `LinearGradientBrush` and `RadialGradientBrush` from resource files. The converter generates Penpot native gradient fills.

### How It Works

1. **Discovery**: `lib/resolver.mjs` scans all `.axaml` files for gradient definitions
2. **Resolution**: `resolveGradient('GradientKey')` returns `{ type, stops, startPoint, endPoint }`
3. **Conversion**: `convert.mjs` generates `storage.createGradientRect()` or `storage.createGradientBoard()` calls

### AXAML Input → Penpot Output

**AXAML Resource:**
```xml
<LinearGradientBrush x:Key="AccentGradient" StartPoint="0%,0%" EndPoint="100%,100%">
  <GradientStop Offset="0" Color="#FF6B2C" />
  <GradientStop Offset="0.5" Color="#E84D8A" />
  <GradientStop Offset="1" Color="#8B2FC9" />
</LinearGradientBrush>
```

**Generated Penpot JS:**
```javascript
var bg = storage.createGradientRect('HeaderBg', 0, 0, 1280, 200, 'linear',
  [
    { offset: 0, color: '#FF6B2C', opacity: 1 },
    { offset: 0.5, color: '#E84D8A', opacity: 1 },
    { offset: 1, color: '#8B2FC9', opacity: 1 }
  ],
  { startX: 0, startY: 0, endX: 1, endY: 1 }
);
board.appendChild(bg);
```

### Gradient Prompt

```
Convert [Screen] to Penpot. This screen has gradient backgrounds.

The resolver automatically parses gradients. In the stdin output, look for:
  [resolver] Loaded: X values, Y gradients, Z style selectors

When Y > 0, gradients are available. The converter will auto-generate
storage.createGradientRect() calls for any element referencing a gradient.

If you want to ADD a gradient that isn't in the AXAML:
  Use storage.createGradientBoard() for the canvas background
  Use storage.createGradientRect() for any rectangle shape
  Gradient format: { type: 'linear'|'radial', stops: [{offset, color, opacity}] }
```

---

## REVERSE DIRECTION: Penpot → AXAML

Read shapes FROM Penpot, map them back to Avalonia AXAML.

### Step 1: Read Penpot Shapes

Execute via MCP `execute_code`:

```javascript
(function() {
  var root = penpot.currentPage.root;
  var all = penpotUtils.findShapes(function(s) {
    return s.type !== 'svg-raw';
  }, root);

  var result = { page: penpot.currentPage.name, board: null, shapes: [] };

  for (var i = 0; i < all.length; i++) {
    var s = all[i];
    var entry = {
      name: s.name,
      type: s.type,
      x: Math.round(s.x), y: Math.round(s.y)
    };

    if (s.type === 'board') {
      result.board = {
        name: s.name,
        width: Math.round(s.width),
        height: Math.round(s.height),
        fills: s.fills
      };
      continue;
    }

    if (s.width) entry.width = Math.round(s.width);
    if (s.height) entry.height = Math.round(s.height);

    // Fill
    if (s.fills && s.fills.length > 0) {
      entry.fill = s.fills[0].fillColor;
      entry.fillOpacity = s.fills[0].fillOpacity;
      if (s.fills[0].fillColorGradient) {
        entry.gradient = s.fills[0].fillColorGradient;
      }
    }

    // Stroke
    if (s.strokes && s.strokes.length > 0) {
      entry.stroke = s.strokes[0].strokeColor;
      entry.strokeWidth = s.strokes[0].strokeWidth;
      entry.strokeOpacity = s.strokes[0].strokeOpacity;
    }

    // Text
    if (s.type === 'text') {
      entry.text = s.characters;
      entry.fontSize = s.fontSize;
      if (s.fontId) entry.fontId = s.fontId;
    }

    // Rectangle specifics
    if (s.type === 'rectangle' && s.borderRadius) {
      entry.borderRadius = s.borderRadius;
    }

    // Opacity
    if (s.opacity !== undefined && s.opacity !== 1) {
      entry.opacity = s.opacity;
    }

    result.shapes.push(entry);
  }

  return JSON.stringify(result, null, 2);
})();
```

### Step 2: Shape → AXAML Mapping Rules

| Penpot Shape | AXAML Element | Heuristic |
|---|---|---|
| board | `<UserControl>` or `<Window>` | Root container |
| board (no children) | `<Grid Background="...">` | Background-only canvas |
| rectangle (with text as sibling) | `<Button>` | Button = bg rect + centered text |
| rectangle (stroke only) | `<Rectangle Stroke="..." StrokeThickness="...">` | Border rectangle |
| rectangle (fill, borderRadius>0) | `<Border CornerRadius="..." Background="...">` | Container border |
| rectangle (fill, no radius) | `<Rectangle Fill="..." Width="..." Height="...">` | Colored rect |
| rectangle (thin, w>>h or h>>w) | `<Line>` or `<Border>` | Divider/seperator |
| ellipse | `<Ellipse Fill="..." Width="..." Height="...">` | Circle/ellipse |
| text | `<TextBlock Text="..." FontSize="...">` | Text label |
| group (paths) | `<Panel>` with `<Path>` children | Vector group |
| path (SVG) | `<Path Data="..." Fill="...">` | Vector path |

### Step 3: Color Reverse-Resolution

When mapping colors back to AXAML:
1. If a color matches a known resource → use `{StaticResource KeyName}`
2. If opacity < 1 and color is from 8-digit hex → use `#AARRGGBB` format
3. Otherwise → use `#RRGGBB` directly

### Reverse Prompt

```
Read the current Penpot page and generate the Avalonia AXAML code.

The page name is "[PAGE_NAME]".

Steps:
1. Read all shapes using the Penpot shape reader script
2. Map each shape to its AXAML equivalent using the mapping rules
3. Group related shapes (e.g., Button bg + Button text → <Button>)
4. Calculate relative positioning (first shape at (0,0) becomes origin)
5. Resolve colors back to {StaticResource} keys where possible
6. Output the complete .axaml file with proper XML namespaces
```

---

## HOW TO ADD A NEW ELEMENT TYPE

Example: Adding a `Calendar` control to the converter.

### Step 1: Classify the tag

```javascript
// In convert.mjs, add to the appropriate list:
const INPUT_TAGS = [
  'TextBox', 'PasswordBox', 'ComboBox', 'Slider',
  'Calendar',  // ← ADD HERE
];
```

### Step 2: Add handler (if needed)

```javascript
// In the dispatch section of walkElement():
} else if (tag === 'Calendar') {
  handleCalendar(attrs, ctx, children, shapes);
}

// New handler:
function handleCalendar(attrs, ctx, children, shapes) {
  // Calendar background
  shapes.push({
    type: 'rect', name: 'Calendar_bg',
    x: ctx.x, y: ctx.y, w: 280, h: 240,
    fillColor: '#FFFFFF', fillOpacity: 0.08, borderRadius: 8,
  });
  // Month/year header
  shapes.push({
    type: 'text', name: 'Calendar_header',
    text: 'July 2026', x: ctx.x + 12, y: ctx.y + 8,
    w: 256, h: 20, fontSize: 14, fontWeight: 600,
    fillColor: '#FFFFFF', fillOpacity: 0.9, align: 'center',
  });
  // Day headers
  var days = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];
  for (var i = 0; i < days.length; i++) {
    shapes.push({
      type: 'text', name: 'CalDay_' + i,
      text: days[i], x: ctx.x + 8 + i * 38, y: ctx.y + 36,
      w: 32, h: 14, fontSize: 10, fontWeight: 500,
      fillColor: '#FFFFFF', fillOpacity: 0.5, align: 'center',
    });
  }
  // Today highlight
  shapes.push({
    type: 'rect', name: 'CalToday',
    x: ctx.x + 8 + 2 * 38, y: ctx.y + 54,  // Wed = index 2
    w: 32, h: 28, fillColor: '#0078D4', fillOpacity: 0.8, borderRadius: 14,
  });
}
```

### Step 3: Add to isArray (if siblings possible)

```javascript
// In convert.mjs parser config:
isArray: (name) => [
  // ... existing tags ...
  'Calendar',  // ← ADD if multiple Calendar siblings possible
].includes(name),
```

### Step 4: Add to intelligence config (if display patterns needed)

```javascript
// In CONFIG.intelligence:
calendarMonths: ['January', 'February', 'March', ...],
calendarDayHeaders: ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'],
```

---

## MCP TOOLS REFERENCE (for Penpot)

| MCP Tool | What It Does |
|---|---|
| `execute_code` | Execute JavaScript in Penpot's plugin context (script or expression) |
| `high_level_overview` | Visual text-tree overview of current Penpot page |
| `export_shape` | Export a specific shape or page as PNG/SVG |
| `penpot_api_info` | Get Penpot JavaScript API documentation by type name |

**Common MCP execute_code patterns:**

```javascript
// Inspect page structure
penpotUtils.shapeStructure(penpot.currentPage, 5)

// Find specific shapes
penpotUtils.findShapes(function(s) { return s.type === 'text'; }, penpot.currentPage.root)

// Get page by name
penpotUtils.getPageByName('Avalonia - StartPage')

// List all pages
penpot.pages.map(function(p) { return p.name; }).join(', ')
```

---

## CODE SNIPPET REFERENCE (for manual Penpot JS)

When you need to write Penpot JS manually (not through the converter), use these patterns:

### Always Start With

```javascript
(function() {
  // Find/create page
  var root = storage.preparePage('Avalonia - MyScreen');

  // Create board (canvas)
  var board = storage.createBoard('MyScreen', 1280, 800, '#0C0C0E', 1);
  root.appendChild(board);

  // ... create shapes, append to board ...

  return 'Done: X shapes';
})();
```

### Common Shape Creation

```javascript
// Colored rectangle
var r = storage.createRect('MyRect', 50, 100, 200, 40, '#FFFFFF', 0.12, 8);
board.appendChild(r);

// Text (with correct font weight)
var t = storage.createText('MyText', 'Hello World', 16, 700, '#FFFFFF', 1, 'center');
t.x = 50; t.y = 110;
board.appendChild(t);

// SVG icon
var icon = storage.createFromSvg('PlayIcon',
  '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z" fill="#FFFFFF"/></svg>');
icon.x = 100; icon.y = 200;
board.appendChild(icon);

// Gradient rectangle
var gr = storage.createGradientRect('GradBg', 0, 0, 1280, 100, 'linear',
  [{ offset: 0, color: '#FF6B2C', opacity: 1 }, { offset: 1, color: '#8B2FC9', opacity: 1 }],
  { startX: 0, startY: 0, endX: 1, endY: 0 });
board.appendChild(gr);

// Stroke-only rectangle (border)
var border = storage.createRect('Border', 10, 10, 200, 60, null, 0);
border.strokes = [{ strokeColor: '#FFFFFF', strokeOpacity: 0.3, strokeWidth: 1 }];
board.appendChild(border);

// Circle/ellipse
var dot = storage.createEllipse('Dot', 100, 100, 12, 12, '#0078D4', 1);
board.appendChild(dot);

// Horizontal divider
var line = storage.createLine('Divider', 0, 300, 400, '#FFFFFF', 0.1, 1);
board.appendChild(line);
```

---

## FILE MAP (Complete Toolkit)

```
axaml-to-penpot/
├── convert.mjs                 # MAIN: AXAML → Penpot JS converter
├── helpers.js                  # Penpot runtime helpers (register via MCP)
├── convert-v2.mjs              # (future: enhanced v2 with more intelligence)
├── UNIVERSAL-PROMPT-ENGINE.md  # THIS FILE — master prompt reference
├── prompt-guide.md             # 8 scenario-specific prompts
├── README.md                   # Quick-start guide
├── package.json                # npm: fast-xml-parser + fdir
├── lib/
│   ├── discovery.mjs           # Auto-discover all .axaml files in any project
│   ├── resolver.mjs            # Parse/Resolve resources, gradients, styles
│   ├── penpot-api.mjs          # Penpot JS API reference + code snippets
│   └── reverse-convert.mjs     # Penpot shapes → AXAML converter
├── examples/
│   ├── with-gradients.md       # Gradient conversion pattern
│   ├── smart-lists.md          # ListBox with dummy data pattern
│   ├── penpot-to-axaml.md      # Reverse direction pattern
│   ├── all-elements.md         # COMPLETE element reference with code
│   └── penpot-read-script.js   # Ready-to-run Penpot shape reader
└── temp_output.js              # (runtime: last generated output)
```

---

## TROUBLESHOOTING

### Converter produces no output or errors

```bash
# Run with verbose logging
node convert.mjs MyScreen 1280 800 --project-root "/path/to/project"

# Check: did discovery find files?
node convert.mjs --list --project-root "/path/to/project"

# Check: can parser read the file?
node -e "
const { XMLParser } = require('fast-xml-parser');
const fs = require('fs');
const xml = fs.readFileSync('/path/to/file.axaml', 'utf-8');
console.log(JSON.stringify(new XMLParser({ignoreAttributes:false}).parse(xml), null, 2).slice(0, 500));
"
```

### Shapes appear in wrong positions

```
Check the layout engine:
- Are shapes inside a StackPanel but not advancing? → stackY/stackX accumulation
- Are grid rows overlapping? → RowDefinitions */Auto/pixel calculation
- Are margins being applied? → Margin-aware size reduction in computeContext
- Center-aligned text off-center? → centerTextX() helper call verification
```

### Fonts not applied

```
Check via MCP execute_code:
  storage.font.fontFamily + ':' + Object.keys(storage).filter(k => k.startsWith('fontW'))
```

### Gradients not rendering

```
- Check resolver output stderr for: [resolver] Loaded: X values, Y gradients, Z style selectors
- If Y=0, no gradient definitions found in resource files
- If Y>0 but not rendering, check: does the element reference the gradient key via {StaticResource ...}?
```

---

## QUICK REFERENCE CARD

```bash
# Discover all screens
node convert.mjs --list --project-root "../../"

# Convert one screen (auto-discover path)
node convert.mjs StartPage 1280 800 --project-root "../../"

# Convert with explicit axaml path
node convert.mjs --axaml-path "src/Views/MainWindow.axaml" 1920 1080

# Mobile: narrow canvas
node convert.mjs LoginPage 375 812 --project-root "../../"
```

**Penpot MCP (for AI agents only):**
```
Step 1: execute_code → paste helpers.js → register
Step 2: execute_code → paste converter stdout → create shapes
Step 3: execute_code → penpotUtils.shapeStructure(penpot.currentPage, 4) → verify
```

**Read shapes from Penpot:**
```
execute_code → paste examples/penpot-read-script.js → get JSON
Map JSON shapes → AXAML using mapping rules in reverse-convert.mjs
```

---

> **Last updated: 2026-07-07**  
> **Toolkit version: 2.0.0**  
> **Works with: ANY Avalonia project, ANY directory structure, ANY element types**

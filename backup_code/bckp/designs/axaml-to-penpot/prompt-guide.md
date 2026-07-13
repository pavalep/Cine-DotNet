# AXAML-to-Penpot: Prompt Engineering Guide

> For AI models (Claude, GPT, etc.) — use these prompts to convert Avalonia UI screens to Penpot designs, and vice versa.
> 
> **NEW: See [UNIVERSAL-PROMPT-ENGINE.md](UNIVERSAL-PROMPT-ENGINE.md) for the COMPLETE master reference** covering ALL elements, gradients, intelligence patterns, reverse direction, MCP tools, and code snippets.
>
> **NEW: See [examples/all-elements.md](examples/all-elements.md) for COMPLETE code patterns** for every Avalonia element type.

---

## Quick Start Prompt

The simplest prompt to get started:

```
Convert the AXAML screen "[SCREEN_NAME]" from this Avalonia project to Penpot.
The project root is at [PROJECT_PATH].

Steps:
1. Run: node convert.mjs [SCREEN_NAME] [WIDTH] [HEIGHT] --project-root "[PROJECT_PATH]"
2. Take the output JS code
3. First, register the helpers: copy helpers.js content → MCP execute_code (type: script)
4. Then execute the generated JS code via MCP execute_code (type: script)
5. Verify with: penpotUtils.shapeStructure(penpot.currentPage, 3) via MCP execute_code

Replace [SCREEN_NAME] with the actual screen name.
Replace [PROJECT_PATH] with the actual project path.
```

---

## Scenario 1: Convert a Single Screen

**Prompt:**
```
I need to convert a specific AXAML screen to Penpot.

Project root: ./src/App/UI/Components/
Screen: LoginPage
Canvas size: 360×640 (mobile)

Please:
1. Run `node convert.mjs LoginPage 360 640 --project-root "."` from the axaml-to-penpot folder
2. If the screen has gradients, ensure gradient support is used
3. If the screen has lists/playlists, add 3-5 dummy items with realistic content
4. Execute the generated code in Penpot
5. Verify the output with export_shape
```

---

## Scenario 2: Convert Multiple Screens

**Prompt:**
```
Convert each AXAML screen in this project to Penpot, one at a time.

Project root: [PROJECT_PATH]
Canvas defaults: 1280×800 for desktop screens, 360×640 for mobile

Steps:
1. Run `node convert.mjs --list --project-root "[PROJECT_PATH]"` to see all discovered screens
2. For each screen, run: `node convert.mjs [NAME] 1280 800 --project-root "[PROJECT_PATH]"`
3. Execute the generated JS in Penpot via MCP execute_code
4. Each screen goes on its own Penpot page named "Avalonia - [Name]"
```

---

## Scenario 3: Penpot → AXAML (Reverse)

**Prompt:**
```
Read the current Penpot page and generate AXAML code from the shapes.

Page name: "Cine — HomeScreen"

Steps:
1. Run MCP execute_code to read all shapes:
   - penpotUtils.findShapes(s => true, penpot.currentPage.root)
   - Extract: type, name, x, y, width, height, fills, fontSize, characters
2. Map Penpot shapes → AXAML elements:
   - board → UserControl (root)
   - rectangle with borderRadius=20 → Button
   - rectangle without borderRadius → Rectangle/Border
   - text → TextBlock
   - group with paths → Panel with Path children
   - ellipse → Ellipse
3. Resolve colors back to {StaticResource} keys where possible
4. Generate the .axaml file
```

---

## Scenario 4: Add Intelligence (Lists, Dummy Data)

**Prompt:**
```
When converting AXAML to Penpot, add intelligent enhancements:

1. If the screen has a ListBox/ItemsControl → add 5 dummy items with varied text
2. If the screen has a Slider ("Volume", "SeekBar") → draw a realistic slider bar
3. If the screen has placeholder text ("No items", "Empty state") → show it clearly
4. If the screen has a TabControl → render all tabs as separate groups
5. If the screen has a DataGrid → render with 3 sample rows
6. If the screen has a TextBox/SearchBox → show it with visible placeholder text

For lists, generate dummy data like:
- Playlist: ["Summer Vibes 2024", "Late Night Jazz", "Workout Mix", "Chill Lo-Fi", "Top Hits"]
- Subtitles: "English [CC]", "Spanish", "French", "German", "Japanese"
- Volume: Show a slider at 65% with label "Volume"
```

---

## Scenario 5: Gradient Support

**Prompt:**
```
Convert this AXAML screen with gradient backgrounds.

The resolver has already parsed gradient definitions. Check stdin output for
[resolver] lines showing "... gradients" count.

When a gradient is detected:
1. Use storage.createGradientRect() for gradient-filled rectangles
2. Use storage.createGradientBoard() for the main canvas background
3. Fall back to solid color (last stop) for shapes that can't use gradients

The converter will automatically detect {StaticResource GradientName} references
and generate the appropriate Penpot gradient fill code.
```

---

## Scenario 6: Batch Update Multiple Screens

**Prompt:**
```
I have 5 AXAML screens that need minor updates. Instead of regenerating everything:

1. Read current Penpot page shapes using MCP
2. Identify which shapes changed from the AXAML (compare by name, position, text)
3. Only update changed shapes: modify text, reposition, resize
4. Leave unchanged shapes untouched

This is more efficient than clearing and recreating the entire page.
```

---

## Scenario 7: Verify & Debug

**Prompt:**
```
The converted screen doesn't look right. Please debug:

1. Run MCP execute_code: penpotUtils.shapeStructure(penpot.currentPage, 5)
   → This shows the shape tree. Check if shapes exist and are positioned correctly.
2. Run MCP execute_code: penpotUtils.findShapes(s => s.type === 'text', penpot.currentPage.root)
   → Check all text shapes for correct content, font size, position
3. Compare shape positions against the expected layout:
   - Are any shapes at y > canvas height? → Layout stacking bug
   - Are shapes overlapping? → Missing margin/spacing in computeContext
   - Are font sizes all 14? → Typography class resolution not working
4. Run MCP high_level_overview to see the visual result
5. Re-run the converter with --verbose flag for debug output
```

---

## Scenario 8: Custom Project Structure

**Prompt:**
```
My Avalonia project has an unusual file structure. Components are in:
  /MyApp/Views/Screens/[ModuleName]/[ScreenName].axaml
Resources are in:
  /MyApp/Theming/CoreColors.axaml
  /MyApp/Theming/TextStyles.axaml

Please:
1. The discovery module auto-finds these — no manual path config needed
2. If discovery misses something, use: node convert.mjs [ScreenName] --axaml-path "/MyApp/Views/Screens/..."
3. Or pass --resource-dirs "/MyApp/Theming" to add custom resource directories
```

---

## MCP Tools Reference

When executing in Penpot via MCP, these tools are available:

| Tool | Purpose |
|---|---|
| `execute_code` | Run JavaScript in Penpot's plugin context |
| `high_level_overview` | Get visual overview of current page |
| `export_shape` | Export a shape/page as PNG/SVG |
| `penpot_api_info` | Get API documentation for a Penpot type |

Use `penpot_api_info` with `type: "Penpot"` to see the full Penpot JS API surface.

---

## Package Installation

```bash
cd axaml-to-penpot
npm install
```

Required packages (in `package.json`):
- `fast-xml-parser` — Parse AXAML XML to JS objects
- `fdir` — Fast directory traversal (used by discovery module)

---

## Files in This Toolkit

| File | Purpose |
|---|---|
| `convert.mjs` | Main converter: AXAML → Penpot JS code |
| `lib/discovery.mjs` | Auto-discover AXAML files in any project |
| `lib/resolver.mjs` | Resolve {StaticResource}, gradients, styles |
| `lib/reverse-convert.mjs` | **Penpot shapes → AXAML** reverse converter (NEW) |
| `lib/penpot-api.mjs` | Penpot API reference + code snippets |
| `helpers.js` | Penpot runtime helpers (register via MCP) |
| `UNIVERSAL-PROMPT-ENGINE.md` | **MASTER reference** — all prompts, all elements (NEW) |
| `examples/all-elements.md` | **Complete code patterns** for every element (NEW) |
| `examples/penpot-read-script.js` | Penpot shape reader MCP script (NEW) |
| `examples/` | Additional scenario-specific guides |
| `README.md` | Getting started guide |

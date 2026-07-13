// =============================================================================
// Penpot API Reference (GENERIC)
//
// This module documents ALL known Penpot MCP API methods for:
//   - Reading shapes from Penpot (inspect, export, list)
//   - Writing shapes to Penpot (create, modify, delete)
//   - Page management (create, open, list pages)
//   - Font & text handling
//
// NOT a runtime module — this is a REFERENCE for AI models generating
// Penpot JS code that runs via MCP execute_code.
//
// HOW TO USE (for AI models):
//   - Read this file to understand available Penpot API methods
//   - Generate JS code strings that use these methods
//   - Execute the generated code via MCP execute_code
//
// TYPE ANNOTATIONS are Penpot's actual API types, documented here
// for understanding only (JS doesn't enforce types).
// =============================================================================

// ── Board (Canvas) ────────────────────────────────────────────────────────
// penpot.createBoard(): Board
//   Creates a new board (artboard). Use as root container per screen.
//   Board properties: name, fills, x, y, width, height
//   Board.background: Group — the board's background layer

// ── Shapes (Create) ────────────────────────────────────────────────────────
// penpot.createRectangle(): Rectangle
//   Properties: name, x, y, width, height, fills, strokes, borderRadius, opacity, blendMode
//   fills: [{ fillColor: '#RRGGBB', fillOpacity: 0-1 }]
//   strokes: [{ strokeColor: '#RRGGBB', strokeOpacity: 0-1, strokeWidth: number }]
//   NOTE: width/height are READ-ONLY. Use shape.resize(w, h) instead.

// penpot.createEllipse(): Ellipse
//   Same properties as Rectangle (no borderRadius).

// penpot.createText(text: string): Text
//   Properties: name, x, y, width, height, fills, fontSize, fontId, fontFamily,
//               fontWeight, fontStyle, lineHeight, letterSpacing, textAlign,
//               textDecoration, growType ('auto-width' | 'auto-height' | 'fixed'),
//               characters (the text content)
//   Available align values: 'left' | 'center' | 'right' | 'justify'
//   NOTE: fontWeight may fail with ':fontWeight' errors. Use font.applyToText() instead.

// penpot.createPath(): Path
//   For raw path data. Properties: content (path string), fills, strokes.

// penpot.createShapeFromSvg(svgString: string): Group | null
//   Imports SVG string as Penpot shapes. Returns a Group.
//   Limitations:
//     - <text> → 'svg-raw' (NOT editable Text shape)
//     - <linearGradient>/<radialGradient> → separate 'svg-raw' object
//     - <style> blocks → NOT supported
//     - id attributes → NOT carried to shape names
//     - HTML comments → REJECTED by SES engine
//   Best used for: complex vector paths (<path>), icons, illustrations.

// penpot.createShapeFromSvgWithImages(svgString: string): Promise<Group | null>
//   Same as above but supports <image> tags. Returns a Promise.

// penpot.createBoolean(): BooleanShape
//   Creates boolean (union/subtract/intersect) from multiple shapes.

// penpot.createGroup(shapes): Group
//   Groups multiple shapes together.

// penpot.createMask(): Mask
//   Creates a mask group.

// ── Shapes (Read / Inspect) ────────────────────────────────────────────────
// penpot.currentPage: Page
//   The currently active page. Access: penpot.currentPage.root.children to list all top-level shapes.

// penpotUtils.shapeStructure(shape, maxDepth?: number): string
//   Returns a text tree of the shape's children (for debugging).
//   Example: penpotUtils.shapeStructure(penpot.currentPage, 4)

// penpotUtils.findShapes(predicate: (shape) => boolean, root?: Shape): Shape[]
//   Searches for shapes matching a predicate.
//   Example: penpotUtils.findShapes(s => s.type === 'text', penpot.currentPage.root)

// shape.children: Shape[]
//   Array of child shapes (read-only reference).

// shape.parent: Shape | null
//   Parent shape.

// shape.type: string
//   'board' | 'rectangle' | 'ellipse' | 'text' | 'path' | 'group' | 'svg-raw' | 'bool' | 'mask'

// shape.name: string
//   Human-readable name (set when creating).

// shape.fills: Array<{ fillColor: string, fillOpacity: number }>
// shape.strokes: Array<{ strokeColor: string, strokeOpacity: number, strokeWidth: number }>

// ── Shapes (Modify) ────────────────────────────────────────────────────────
// shape.x = number; shape.y = number;
//   ABSOLUTE positioning on the page/board. Set directly.

// shape.resize(width: number, height: number): void
//   Resize the shape. width/height properties are read-only, use this method.

// shape.opacity = 0-1;
//   Set overall opacity of the shape.

// shape.borderRadius = number; // For rectangles only

// shape.characters = 'new text'; // For Text shapes only

// shape.remove(): void
//   Remove and destroy the shape. DO NOT use parent.removeChild().

// board.appendChild(shape): void
//   Add a shape to a board/group. Use on the container, not the child.

// ── Pages ──────────────────────────────────────────────────────────────────
// penpot.createPage(): Page
//   Creates a new empty page. Returns the new Page object.

// penpot.openPage(page: Page): void
//   Switches to the given page (makes it active).

// penpotUtils.getPageByName(name: string): Page | null
//   Finds a page by name. Returns null if not found.
//   Use this instead of iterating penpot.pages manually.

// penpot.currentPage.name = 'New Name';
//   Rename a page. Then re-fetch: penpotUtils.getPageByName('New Name').

// penpot.root: Shape (of current page)
//   Same as penpot.currentPage.root for brevity.

// ── Fonts ──────────────────────────────────────────────────────────────────
// penpot.fonts.all: Font[]
//   Array of all available fonts.
//   Each Font: { fontFamily, fontId, variants: FontVariant[] }
//   Each FontVariant: { fontId, fontFamily, fontWeight, fontStyle }

// penpot.fonts.findByName(name: string): Font | null
//   Find a font by family name (e.g. 'M PLUS 2', 'Inter', 'Roboto').

// font.applyToText(text: Text, variant?: FontVariant): void
//   Apply this font (and optional variant) to a Text shape.
//   This is the CORRECT way to set font properties on Text shapes.
//   Example:
//     var font = penpot.fonts.findByName('M PLUS 2');
//     var w600 = font.variants.find(v => v.fontWeight === '600');
//     font.applyToText(myText, w600);

// font.variants: FontVariant[]
//   Available weight/style combinations for this font.

// ── Export ──────────────────────────────────────────────────────────────────
// shape.exportSync(options): Uint8Array
//   Export shape as image data.
//   options: { type: 'png' | 'jpeg' | 'svg', scale?: number }

// penpot.selection: Shape[]
//   Currently selected shapes (can set, e.g. penpot.selection = [myShape]).

// ── Storage (Persists across execute_code calls) ────────────────────────────
// storage.*
//   A global object that persists between MCP execute_code calls.
//   Use this to cache helper functions, font references, counters, etc.
//   Example: storage.myHelper = function() { ... };
//            storage.counter = (storage.counter || 0) + 1;

// ═══════════════════════════════════════════════════════════════════════════════
// CODE SNIPPETS (for AI models to use as templates)
// ═══════════════════════════════════════════════════════════════════════════════

// ── SNIPPET 1: Find and use a font ──────────────────────────────────────────
/*
var font = penpot.fonts.findByName('M PLUS 2');
if (!font) {
  // Fallback: any font with a bold variant
  var all = penpot.fonts.all;
  for (var i = 0; i < all.length; i++) {
    var f = all[i];
    var v = f.variants.find(function(v) { return v.fontWeight === '700'; });
    if (v) { font = f; break; }
  }
}
var w400 = font.variants.find(function(v) { return v.fontWeight === '400' && v.fontStyle === 'normal'; });
var w700 = font.variants.find(function(v) { return v.fontWeight === '700'; });
*/

// ── SNIPPET 2: Create a page and board (standard setup) ─────────────────────
/*
var pageName = 'My Screen';
var page = penpotUtils.getPageByName(pageName);
if (!page) { page = penpot.createPage(); page.name = pageName; }
penpot.openPage(page);
// Clear existing content on this page
var children = page.root.children.slice();
for (var i = 0; i < children.length; i++) { children[i].remove(); }

var board = penpot.createBoard();
board.name = 'Canvas';
board.resize(1280, 800);
board.fills = [{ fillColor: '#0C0C0E', fillOpacity: 1 }];
board.x = 0; board.y = 0;
page.root.appendChild(board);
*/

// ── SNIPPET 3: Create a stylized rectangle (button, card, bar) ──────────────
/*
var rect = penpot.createRectangle();
rect.name = 'ButtonBg';
rect.resize(200, 40);
rect.x = 100; rect.y = 50;
rect.fills = [{ fillColor: '#FFFFFF', fillOpacity: 0.12 }];
rect.borderRadius = 20;
board.appendChild(rect);
*/

// ── SNIPPET 4: Create editable text with correct font ───────────────────────
/*
var text = penpot.createText('Hello World');
text.name = 'Title';
text.fontSize = 34;
text.textAlign = 'center';
text.growType = 'auto-width';
text.fills = [{ fillColor: '#FFFFFF', fillOpacity: 0.9 }];
font.applyToText(text, w700); // Apply font + weight variant
text.x = 100; text.y = 200;
board.appendChild(text);
*/

// ── SNIPPET 5: Import SVG icon (for complex paths) ──────────────────────────
/*
var svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M8 5v14l11-7z" fill="#FFFFFF" fill-opacity="1"/></svg>';
var icon = penpot.createShapeFromSvg(svg);
icon.name = 'PlayIcon';
// Remove auto-created background rectangles
var kids = icon.children;
for (var i = kids.length - 1; i >= 0; i--) {
  if (kids[i].type === 'rectangle') { kids[i].remove(); }
}
icon.x = 100; icon.y = 300;
board.appendChild(icon);
*/

// ── SNIPPET 6: Read shapes from Penpot (for Penpot → AXAML direction) ──────
/*
// Get all shapes on current page
var root = penpot.currentPage.root;
var allShapes = penpotUtils.findShapes(function(s) { return true; }, root);

// Filter by type
var texts = penpotUtils.findShapes(function(s) { return s.type === 'text'; }, root);
var rects = penpotUtils.findShapes(function(s) { return s.type === 'rectangle'; }, root);
var boards = penpotUtils.findShapes(function(s) { return s.type === 'board'; }, root);

// Read shape properties
for (var i = 0; i < texts.length; i++) {
  var t = texts[i];
  console.log('Text:', t.characters, 'at', t.x, t.y, 'size', t.fontSize);
}

// Export shapes as PNG
var pngData = board.exportSync({ type: 'png', scale: 2 });
*/

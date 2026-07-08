// ═══════════════════════════════════════════════════════════════════════════════
// Penpot Shape Reader — run THIS in Penpot MCP execute_code
//
// Purpose: Extract ALL shapes from the current Penpot page as structured JSON
//          to be consumed by lib/reverse-convert.mjs for AXAML generation.
//
// HOW TO USE:
//   1. Copy this ENTIRE file content
//   2. Execute via MCP: execute_code (type: script)
//   3. The return value is JSON — save it to a .json file
//   4. Run: node lib/reverse-convert.mjs shapes.json --output MyScreen.axaml
//
// MODIFY the OPTIONS object below to customize what gets extracted.
// ═══════════════════════════════════════════════════════════════════════════════

(function() {
  // ── OPTIONS (customize these) ────────────────────────────────────────────
  var OPTIONS = {
    pageName: null,           // null = current page, or "My Page Name"
    maxDepth: 5,              // How deep to traverse shape hierarchy
    includeSvgRaw: false,      // Include raw SVG imports (usually skip)
    includeGroups: true,       // Include group shapes with their children
    normalizePosition: false,  // If true, subtract board x/y from shapes
  };

  // ── MAIN ─────────────────────────────────────────────────────────────────
  var page;
  if (OPTIONS.pageName) {
    page = penpotUtils.getPageByName(OPTIONS.pageName);
  }
  if (!page) {
    page = penpot.currentPage;
  }

  var root = page.root;
  var result = {
    page: page.name,
    exportedAt: new Date().toISOString(),
    board: null,
    shapes: []
  };

  // Find the board first
  var allTop = root.children || [];
  for (var i = 0; i < allTop.length; i++) {
    var s = allTop[i];
    if (s.type === 'board') {
      result.board = extractBoardInfo(s);
      // Extract children of the board
      extractChildShapes(s, result.shapes, 0, OPTIONS.maxDepth);
      break;
    }
  }

  // If no board found, extract all top-level shapes
  if (!result.board) {
    for (var i = 0; i < allTop.length; i++) {
      extractShape(allTop[i], result.shapes, 0, OPTIONS.maxDepth);
    }
  }

  return JSON.stringify(result, null, 2);

  // ── HELPERS ──────────────────────────────────────────────────────────────

  function extractBoardInfo(board) {
    var info = {
      name: board.name || 'Canvas',
      width: Math.round(board.width || 1280),
      height: Math.round(board.height || 800),
      x: Math.round(board.x || 0),
      y: Math.round(board.y || 0),
      fills: extractFills(board),
    };
    return info;
  }

  function extractChildShapes(parent, targetArray, depth, maxDepth) {
    if (depth >= maxDepth) return;
    var children = parent.children || [];
    for (var i = 0; i < children.length; i++) {
      extractShape(children[i], targetArray, depth, maxDepth);
    }
  }

  function extractShape(shape, targetArray, depth, maxDepth) {
    if (!shape) return;

    // Skip SVG raw unless explicitly requested
    if (!OPTIONS.includeSvgRaw && shape.type === 'svg-raw') return;

    var entry = {
      name: shape.name || '',
      type: shape.type,
      x: Math.round(shape.x || 0),
      y: Math.round(shape.y || 0),
    };

    // Dimensions
    if (shape.width) entry.width = Math.round(shape.width);
    if (shape.height) entry.height = Math.round(shape.height);

    // Fill
    var fills = extractFills(shape);
    if (fills) entry = Object.assign(entry, fills);

    // Stroke
    if (shape.strokes && shape.strokes.length > 0) {
      entry.stroke = shape.strokes[0].strokeColor;
      entry.strokeWidth = shape.strokes[0].strokeWidth;
      if (shape.strokes[0].strokeOpacity !== undefined) {
        entry.strokeOpacity = shape.strokes[0].strokeOpacity;
      }
    }

    // Text-specific
    if (shape.type === 'text') {
      entry.text = shape.characters || '';
      entry.fontSize = shape.fontSize;
      entry.textAlign = shape.textAlign || 'left';
      if (shape.fontId) entry.fontId = shape.fontId;
      if (shape.fontFamily) entry.fontFamily = shape.fontFamily;
      if (shape.fontWeight) entry.fontWeight = shape.fontWeight;
    }

    // Rectangle-specific
    if (shape.type === 'rectangle' && shape.borderRadius) {
      entry.borderRadius = shape.borderRadius;
    }

    // Opacity
    if (shape.opacity !== undefined && shape.opacity !== 1) {
      entry.opacity = shape.opacity;
    }

    // Blend mode
    if (shape.blendMode && shape.blendMode !== 'normal') {
      entry.blendMode = shape.blendMode;
    }

    // Recurse into groups
    if (OPTIONS.includeGroups && shape.type === 'group' && depth < maxDepth) {
      entry.children = [];
      extractChildShapes(shape, entry.children, depth + 1, maxDepth);
    }

    targetArray.push(entry);
  }

  function extractFills(shape) {
    if (!shape.fills || shape.fills.length === 0) return null;

    var fill = shape.fills[0];
    var result = {};

    if (fill.fillColor) {
      result.fill = fill.fillColor;
      result.fillOpacity = fill.fillOpacity !== undefined ? fill.fillOpacity : 1;
    }

    // Gradient
    if (fill.fillColorGradient) {
      result.gradient = {
        type: fill.fillColorGradient.type,
        startX: fill.fillColorGradient.startX,
        startY: fill.fillColorGradient.startY,
        endX: fill.fillColorGradient.endX,
        endY: fill.fillColorGradient.endY,
        stops: (fill.fillColorGradient.stops || []).map(function(stop) {
          return {
            offset: stop.offset,
            color: stop.color,
            opacity: stop.opacity,
          };
        }),
      };
    }

    return Object.keys(result).length > 0 ? result : null;
  }
})();

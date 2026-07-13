// ═══════════════════════════════════════════════════════════════════════════════
// Penpot Helpers (GENERIC — runs in Penpot MCP execute_code)
//
// Register these ONCE in Penpot via MCP execute_code. They persist in storage.*
// and are called by the generated JS code from convert.mjs.
//
// Features:
//   - Board & shape creation with correct font handling
//   - Gradient support (fills shapes with SVG gradients via createShapeFromSvg)
//   - 8-digit hex color parsing (#AARRGGBB → color + opacity)
//   - Page management (single shared page — all boards on one canvas like Figma)
//   - Text centering helper (centerTextX)
//   - Library Asset/Tokens import (colors, typographies, design tokens)
//
// # SINGLE CANVAS RULE
// ALL screens/boards go on ONE page: "Avalonia - Design System"
// Do NOT create separate pages per screen. Like Figma, everything lives
// on the same canvas, arranged spatially with intelligent grouping.
// Use prepareSinglePage() instead of preparePage().
//
// # ASSETS & TOKENS
// Colors and Typographies from AXAML resource files are registered once
// as Penpot Library Assets. Tokens are created as Token Sets.
// Generated shapes should reference these library entries by name.
//
// HOW TO REGISTER:
//   Copy this entire file content → MCP execute_code (type: script)
//   Run once per Penpot session.
// ═══════════════════════════════════════════════════════════════════════════════

(function() {
  // ── Font setup (find the best font available) ──────────────────────────
  // Generic: tries 'M PLUS 2' first, falls back to any font with weight variants
  var font = penpot.fonts.findByName('M PLUS 2')
    || penpot.fonts.findByName('Inter')
    || penpot.fonts.findByName('Roboto');

  if (!font) {
    // Last resort: any font that has a '700' weight variant
    var all = penpot.fonts.all;
    for (var i = 0; i < all.length; i++) {
      var f = all[i];
      var v = f.variants.find(function(v) { return v.fontWeight === '700'; });
      if (v) { font = f; break; }
    }
  }

  // Cache common weight variants (add more as needed)
  storage.font = font;
  storage.fontW900 = font ? font.variants.find(function(v) { return v.fontWeight === '900'; }) : null;
  storage.fontW700 = font ? font.variants.find(function(v) { return v.fontWeight === '700'; }) : null;
  storage.fontW600 = font ? font.variants.find(function(v) { return v.fontWeight === '600'; }) : null;
  storage.fontW500 = font ? font.variants.find(function(v) { return v.fontWeight === '500'; }) : null;
  storage.fontW400 = font ? font.variants.find(function(v) { return v.fontWeight === '400' && v.fontStyle === 'normal'; }) : null;
  storage.fontW300 = font ? font.variants.find(function(v) { return v.fontWeight === '300'; }) : null;
  storage.fontW200 = font ? font.variants.find(function(v) { return v.fontWeight === '200'; }) : null;
  storage.fontW100 = font ? font.variants.find(function(v) { return v.fontWeight === '100'; }) : null;

  // Helper: get font variant by weight (numeric string or standard name)
  storage.getFontVariant = function(weight) {
    var weights = {
      '900': storage.fontW900, '800': storage.fontW800, '700': storage.fontW700,
      '600': storage.fontW600, '500': storage.fontW500, '400': storage.fontW400,
      '300': storage.fontW300, '200': storage.fontW200, '100': storage.fontW100,
      'black': storage.fontW900, 'extra bold': storage.fontW800, 'bold': storage.fontW700,
      'semi bold': storage.fontW600, 'medium': storage.fontW500, 'normal': storage.fontW400,
      'regular': storage.fontW400, 'light': storage.fontW300, 'extra light': storage.fontW200,
      'thin': storage.fontW100,
    };
    return weights[String(weight).toLowerCase()] || storage.fontW400;
  };

  // ── Counter ────────────────────────────────────────────────────────────
  if (typeof storage.screenCounter !== 'number') storage.screenCounter = 0;

  // ═══════════════════════════════════════════════════════════════════════
  // SHAPE CREATION HELPERS (called by generated code)
  // ═══════════════════════════════════════════════════════════════════════

  // ── Board (canvas container) ───────────────────────────────────────────
  storage.createBoard = function(name, w, h, fillColor, fillOpacity) {
    var board = penpot.createBoard();
    board.name = name || 'Canvas';
    board.resize(w || 1280, h || 800);
    if (fillColor) {
      board.fills = [{ fillColor: fillColor, fillOpacity: fillOpacity != null ? fillOpacity : 1 }];
    }
    return board;
  };

  // ── Rectangle (with optional stroke, corner radius) ──────────────────
  storage.createRect = function(name, x, y, w, h, fillColor, fillOpacity, borderRadius, strokeColor, strokeWidth, strokeOpacity) {
    var r = penpot.createRectangle();
    r.name = name || 'rect';
    r.resize(Math.max(1, w || 100), Math.max(1, h || 100));
    r.x = x || 0; r.y = y || 0;
    if (fillColor) {
      r.fills = [{ fillColor: fillColor, fillOpacity: fillOpacity != null ? fillOpacity : 1 }];
    }
    if (borderRadius > 0) {
      r.borderRadius = borderRadius;
    }
    if (strokeColor && strokeWidth > 0) {
      r.strokes = [{ strokeColor: strokeColor, strokeOpacity: strokeOpacity != null ? strokeOpacity : 1, strokeWidth: strokeWidth }];
    }
    return r;
  };

  // ── Ellipse ─────────────────────────────────────────────────────────
  storage.createEllipse = function(name, x, y, w, h, fillColor, fillOpacity) {
    var e = penpot.createEllipse();
    e.name = name || 'ellipse';
    e.resize(Math.max(1, w || 100), Math.max(1, h || 100));
    e.x = x || 0; e.y = y || 0;
    if (fillColor) {
      e.fills = [{ fillColor: fillColor, fillOpacity: fillOpacity != null ? fillOpacity : 1 }];
    }
    return e;
  };

  // ── Text (editable, with correct font) ─────────────────────────────────
  storage.createText = function(name, text, fontSize, fontWeight, fillColor, fillOpacity, align) {
    var t = penpot.createText(text || '');
    t.name = name || 'text';
    t.fontSize = fontSize || 14;
    t.growType = 'auto-width';
    if (fillColor) {
      t.fills = [{ fillColor: fillColor, fillOpacity: fillOpacity != null ? fillOpacity : 1 }];
    }
    // Use font.applyToText with correct weight variant
    var variant = storage.getFontVariant(String(fontWeight || '400'));
    if (storage.font && variant) {
      storage.font.applyToText(t, variant);
    }
    return t;
  };

  // ── SVG Import (for paths, icons, complex vector shapes) ─────────────
  storage.createFromSvg = function(name, svgString) {
    var g = penpot.createShapeFromSvg(svgString);
    if (!g) return null;
    g.name = name || 'SvgGroup';
    // Remove auto-generated background rectangles from SVG import
    var kids = g.children;
    for (var i = kids.length - 1; i >= 0; i--) {
      if (kids[i].type === 'rectangle') { kids[i].remove(); }
    }
    return g;
  };

  // ═══════════════════════════════════════════════════════════════════════
  // GRADIENT SUPPORT
  // ═══════════════════════════════════════════════════════════════════════
  // Since Penpot's createShapeFromSvg doesn't properly handle <linearGradient>
  // inside SVG imports, we use a workaround:
  //   1. Create a solid-filled rectangle as the base shape
  //   2. Set fill to a solid color (last gradient stop) as fallback
  // For true gradient rendering, use manual fill objects:
  //   shape.fills = [{ fillColorGradient: { type: 'linear', ... } }]

  /**
   * Create a rectangle with a gradient fill.
   * Uses Penpot's native gradient fill format (not SVG).
   *
   * @param {string} name
   * @param {number} x, y, w, h
   * @param {string} type - 'linear' or 'radial'
   * @param {Array} stops - [{ offset: 0-1, color: '#RRGGBB', opacity: 0-1 }]
   * @param {Object} options - { startX, startY, endX, endY } for linear
   *                           { centerX, centerY } for radial
   */
  storage.createGradientRect = function(name, x, y, w, h, type, stops, options) {
    var r = penpot.createRectangle();
    r.name = name || 'gradient-rect';
    r.resize(Math.max(1, w || 100), Math.max(1, h || 100));
    r.x = x || 0; r.y = y || 0;

    // Build Penpot native gradient fill
    var gradient = {
      type: type || 'linear',
      startX: (options && options.startX != null) ? options.startX : 0,
      startY: (options && options.startY != null) ? options.startY : 0,
      endX: (options && options.endX != null) ? options.endX : 0,
      endY: (options && options.endY != null) ? options.endY : 1,
      width: 1, height: 1,
    };

    if (type === 'radial') {
      gradient.startX = (options && options.centerX != null) ? options.centerX : 0.5;
      gradient.startY = (options && options.centerY != null) ? options.centerY : 0.5;
      gradient.endX = gradient.startX;
      gradient.endY = gradient.startY;
    }

    // Map stops to Penpot gradient stops
    gradient.stops = (stops || []).map(function(s) {
      return {
        color: s.color || '#FFFFFF',
        opacity: s.opacity != null ? s.opacity : 1,
        offset: s.offset != null ? s.offset : 0,
      };
    });

    r.fills = [{ fillColorGradient: gradient }];
    return r;
  };

  /**
   * Create a board with a gradient background.
   * Same as createBoard but uses gradient fill.
   */
  storage.createGradientBoard = function(name, w, h, type, stops, options) {
    var board = penpot.createBoard();
    board.name = name || 'GradientCanvas';
    board.resize(w || 1280, h || 800);

    var gradient = {
      type: type || 'linear',
      startX: (options && options.startX != null) ? options.startX : 0,
      startY: (options && options.startY != null) ? options.startY : 0,
      endX: (options && options.endX != null) ? options.endX : 0,
      endY: (options && options.endY != null) ? options.endY : 1,
      width: 1, height: 1,
    };

    if (type === 'radial') {
      gradient.startX = (options && options.centerX != null) ? options.centerX : 0.5;
      gradient.startY = (options && options.centerY != null) ? options.centerY : 0.5;
      gradient.endX = gradient.startX;
      gradient.endY = gradient.startY;
    }

    gradient.stops = (stops || []).map(function(s) {
      return { color: s.color || '#FFFFFF', opacity: s.opacity != null ? s.opacity : 1, offset: s.offset != null ? s.offset : 0 };
    });

    board.fills = [{ fillColorGradient: gradient }];
    return board;
  };

  // ═══════════════════════════════════════════════════════════════════════
  // UTILITY HELPERS
  // ═══════════════════════════════════════════════════════════════════════

  /**
   * Parse 8-digit hex (#AARRGGBB) and set fill on a shape.
   * Splits into fillColor (#RRGGBB) + fillOpacity (AA/255).
   */
  storage.setFillFromHex8 = function(shape, hex8) {
    if (!hex8 || hex8.length < 7) return;
    if (hex8.length === 9) {
      var alpha = parseInt(hex8.substring(1, 3), 16) / 255;
      var color = '#' + hex8.substring(3);
      shape.fills = [{ fillColor: color, fillOpacity: Math.round(alpha * 100) / 100 }];
    } else {
      shape.fills = [{ fillColor: hex8, fillOpacity: 1 }];
    }
  };

  /**
   * Prepare the single shared page: create if not exists, open it.
   * Does NOT clear existing shapes — new boards are added alongside.
   * All screens share this one page (Figma-style canvas).
   * Returns the page's root node.
   */
  storage.prepareSinglePage = function() {
    var pageName = 'Avalonia - Design System';
    var page = penpotUtils.getPageByName(pageName);
    if (!page) {
      page = penpot.createPage();
      page.name = pageName;
    }
    penpot.openPage(page);
    return page.root;
  };

  /**
   * @deprecated Use prepareSinglePage() instead.
   * Old page-per-screen approach. Clears all shapes.
   */
  storage.preparePage = function(pageName) {
    var page = penpotUtils.getPageByName(pageName);
    if (!page) {
      page = penpot.createPage();
      page.name = pageName;
    }
    penpot.openPage(page);
    var children = page.root.children.slice();
    for (var i = 0; i < children.length; i++) {
      children[i].remove();
    }
    return page.root;
  };

  /**
   * Populate Penpot Library Assets from AXAML resources.
   * Creates colors and typographies matching Colors.axaml and Typography.axaml.
   */
  storage.createLibraryAssets = function() {
    var lib = penpot.library.local;
    var results = [];

    // ── Colors from Colors.axaml ──
    var colorDefs = [
      // Base
      ['Colors/Black', '#000000'],
      ['Colors/White', '#FFFFFF'],
      // Gray scale
      ['Colors/Gray100', '#E5E5E5'],
      ['Colors/Gray200', '#D6D6D6'],
      ['Colors/Gray300', '#B0B0B0'],
      ['Colors/Gray400', '#808080'],
      ['Colors/Gray500', '#6E6E6E'],
      ['Colors/Gray600', '#555555'],
      ['Colors/Gray700', '#3D3D3D'],
      ['Colors/Gray800', '#2D2D30'],
      ['Colors/Gray900', '#1E1E1E'],
      ['Colors/Gray950', '#131313'],
      // Accent
      ['Colors/Accent', '#5B9BD5'],
      ['Colors/AccentHover', '#4A8BC5'],
      ['Colors/AccentPressed', '#3A7BB5'],
      // App Color System (solid)
      ['Colors/AppBackground', '#0C0C0E'],
      ['Colors/AppSurface', '#1A1A2E'],
      ['Colors/AppSurfaceLight', '#2A2A3E'],
      ['Colors/AppDialogSurface', '#1E1E2E'],
      ['Colors/AppAccent', '#6CB4FF'],
      ['Colors/AppAccentDim', '#4AA3FF'],
      ['Colors/AppAccentLight', '#4AA3FF'],
      ['Colors/AppTextPrimary', '#E5E5E5'],
      ['Colors/AppTextMuted', '#AAAAAA'],
      // Semantic
      ['Colors/ErrorColor', '#FF5252'],
      ['Colors/SuccessColor', '#4CAF50'],
      ['Colors/WarningColor', '#FFB300'],
      ['Colors/InfoColor', '#42A5F5'],
    ];

    // Colors with opacity (use #RRGGBB color + opacity field)
    var colorDefsWithOpacity = [
      ['Colors/AppTextOnDarkPrimary', '#FFFFFF', 0.8],
      ['Colors/AppTextOnDarkSecondary', '#FFFFFF', 0.67],
      ['Colors/AppTextOnDarkHint', '#FFFFFF', 0.6],
      ['Colors/AppTextOnDarkDisabled', '#FFFFFF', 0.31],
      ['Colors/AppTextOnDarkTertiary', '#FFFFFF', 0.5],
      ['Colors/TextTertiary', '#FFFFFF', 0.5],
      ['Colors/AppTextOnDarkSubtle', '#FFFFFF', 0.47],
      ['Colors/AppOverlay', '#000000', 0.69],
      ['Colors/AppOverlayLight', '#000000', 0.5],
      ['Colors/AppOverlayDark', '#000000', 0.4],
      ['Colors/AppOverlayChrome', '#000000', 0.74],
      ['Colors/AppHoverSubtle', '#FFFFFF', 0.08],
      ['Colors/AppHover', '#FFFFFF', 0.12],
      ['Colors/AppHoverStrong', '#FFFFFF', 0.17],
      ['Colors/AppPressed', '#FFFFFF', 0.25],
      ['Colors/AppDivider', '#FFFFFF', 0.15],
      ['Colors/AppDividerStrong', '#FFFFFF', 0.2],
      ['Colors/AppBorderLight', '#FFFFFF', 0.25],
      ['Colors/AppBorderDim', '#FFFFFF', 0.13],
      ['Colors/AppIconLight', '#FFFFFF', 0.27],
      ['Colors/AppIconDim', '#FFFFFF', 0.19],
    ];

    for (var i = 0; i < colorDefs.length; i++) {
      var c = lib.createColor();
      c.name = colorDefs[i][0];
      c.color = colorDefs[i][1];
    }

    for (var i = 0; i < colorDefsWithOpacity.length; i++) {
      var c = lib.createColor();
      c.name = colorDefsWithOpacity[i][0];
      c.color = colorDefsWithOpacity[i][1];
      c.opacity = colorDefsWithOpacity[i][2];
    }

    results.push(lib.colors.length + ' colors');

    // ── Typographies from Typography.axaml ──
    var typoDefs = [
      { name: 'Typography/caption', fontSize: 12, fontWeight: 400, letterSpacing: 0.5 },
      { name: 'Typography/body2',   fontSize: 14, fontWeight: 400, letterSpacing: 0.25 },
      { name: 'Typography/body1',   fontSize: 16, fontWeight: 400, letterSpacing: 0 },
      { name: 'Typography/subtitle1', fontSize: 14, fontWeight: 600, letterSpacing: 0.15 },
      { name: 'Typography/headline6', fontSize: 20, fontWeight: 500, letterSpacing: 0 },
      { name: 'Typography/headline4', fontSize: 24, fontWeight: 400, letterSpacing: -0.25 },
      { name: 'Typography/headline2', fontSize: 34, fontWeight: 400, letterSpacing: -0.5 },
    ];

    for (var i = 0; i < typoDefs.length; i++) {
      var t = lib.createTypography();
      var d = typoDefs[i];
      t.name = d.name;
      t.fontFamilies = 'Segoe UI';
      t.fontSize = String(d.fontSize);
      t.fontWeight = String(d.fontWeight);
      t.lineHeight = '1.5';
      t.letterSpacing = String(d.letterSpacing);
    }

    results.push(lib.typographies.length + ' typographies');

    // ── Design Token Sets ──
    var tokens = lib.tokens;
    if (tokens.sets.length === 0) {
      tokens.addSet({ name: 'cine/colors' });
      tokens.addSet({ name: 'cine/typography' });
    }
    results.push(tokens.sets.length + ' token sets');

    return results.join(', ');
  };

  /**
   * Center a Text shape horizontally at a given X coordinate.
   * Useful when the text has 'auto-width' growType.
   */
  storage.centerTextX = function(textShape, centerX) {
    textShape.x = (centerX || 0) - textShape.width / 2;
  };

  /**
   * Create a simple horizontal line/divider.
   */
  storage.createLine = function(name, x, y, w, color, opacity, strokeWidth) {
    var r = penpot.createRectangle();
    r.name = name || 'line';
    r.resize(w || 100, strokeWidth || 1);
    r.x = x || 0; r.y = y || 0;
    if (color) {
      r.fills = [{ fillColor: color, fillOpacity: opacity != null ? opacity : 1 }];
    }
    return r;
  };

  // ── Final ──────────────────────────────────────────────────────────────
  return 'Helpers registered. Font: ' + (storage.font ? storage.font.fontFamily : 'NOT FOUND')
    + ', Gradients: supported, Weights: '
    + Object.keys(storage).filter(function(k) { return k.startsWith('fontW'); }).join(', ');
})();

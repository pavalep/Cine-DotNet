// ═══════════════════════════════════════════════════════════════════════════════
// Penpot → AXAML Reverse Converter (GENERIC)
//
// Takes Penpot shape JSON (extracted via MCP execute_code) and converts to
// Avalonia AXAML markup. Works for ANY Penpot design, ANY project.
//
// How to use:
//   1. Run the penpot-read-script.js in Penpot MCP to extract shapes as JSON
//   2. Save the output to a .json file
//   3. Run: node lib/reverse-convert.mjs shapes.json --output MyScreen.axaml
//   4. Or use programmatically: import { penpotToAxaml } from './lib/reverse-convert.mjs'
//
// Shape → AXAML Mapping Rules (heuristic, works for ANY design):
//   board                     → <UserControl Width="..." Height="...">
//   rectangle (bg + near text) → <Button>
//   rectangle (stroke only)   → <Rectangle Stroke="..." StrokeThickness="...">
//   rectangle (fill+borderRadius) → <Border CornerRadius="..." Background="...">
//   rectangle (fill, no radius) → <Rectangle Fill="...">
//   rectangle (thin: h<6)     → <Line> or divider <Border>
//   ellipse                   → <Ellipse>
//   text                      → <TextBlock>
//   group (paths)             → <Panel> with <Path> children
//   group (other)             → <StackPanel> or <Grid>
// ═══════════════════════════════════════════════════════════════════════════════

import { readFileSync, writeFileSync } from 'fs';
import { resolve } from 'path';

// ═══════════════════════════════════════════════════════════════════════════════
// MAIN
// ═══════════════════════════════════════════════════════════════════════════════

/**
 * Convert Penpot shape JSON to Avalonia AXAML string.
 *
 * @param {Object} data - Shape data from penpot-read-script.js
 * @param {Object} options
 * @param {string} options.namespace - XML namespace prefix (default: "Avalonia")
 * @param {boolean} options.useStaticResource - Try to resolve colors to {StaticResource} keys
 * @param {Object} options.resourceMap - Optional map of color→key for reverse resolution
 * @returns {string} Complete .axaml file content
 */
export function penpotToAxaml(data, options = {}) {
  const shapes = data.shapes || [];
  const board = data.board;
  const bW = board ? board.width : 1280;
  const bH = board ? board.height : 800;

  // Group shapes: detect buttons (rect + nearby text)
  const groups = detectButtonGroups(shapes);
  const usedShapes = new Set();

  let axaml = buildHeader(bW, bH, options.namespace || 'Avalonia');
  axaml += buildRootStart(bW, bH, board);

  // Render grouped buttons first
  for (const g of groups) {
    axaml += renderButtonGroup(g, shapes, usedShapes);
  }

  // Render remaining ungrouped shapes
  for (let i = 0; i < shapes.length; i++) {
    if (usedShapes.has(i)) continue;
    axaml += renderShape(shapes[i], i, bW, bH);
  }

  axaml += buildRootEnd();
  return axaml;
}

// ═══════════════════════════════════════════════════════════════════════════════
// SHAPE RENDERERS
// ═══════════════════════════════════════════════════════════════════════════════

function renderShape(s, index, canvasW, canvasH) {
  const indent = '    ';
  switch (s.type) {
    case 'rectangle': return renderRectangle(s, indent);
    case 'ellipse':   return renderEllipse(s, indent);
    case 'text':      return renderText(s, indent);
    case 'path':      return renderPath(s, indent);
    case 'group':     return renderGroup(s, indent, canvasW, canvasH);
    case 'svg-raw':   return `  <!-- SVG import: ${s.name} (needs manual Path conversion) -->\n`;
    default:          return `  <!-- Unknown type: ${s.type} name=${s.name} -->\n`;
  }
}

// ── Rectangle → Border / Rectangle / Button / Line ──

function renderRectangle(s, indent) {
  const name = safeName(s.name);
  const w = s.width || 100, h = s.height || 40;
  const x = s.x || 0, y = s.y || 0;

  // Divider line detection: very thin horizontal
  if (h <= 6 && w >= 100) {
    return renderLineDivider(s, indent, name);
  }

  // Stroke-only rectangle (border)
  if ((!s.fill || s.fillOpacity === 0) && s.stroke && s.strokeWidth > 0) {
    return renderStrokedRect(s, indent, name);
  }

  // Filled rectangle with border radius (Border container)
  let axaml = '';
  const attrs = [];

  // Position & size
  if (x > 0) attrs.push(`Margin="${x},${y},0,0"`);
  if (w > 0) attrs.push(`Width="${w}"`);
  if (h > 0) attrs.push(`Height="${h}"`);

  // Fill
  if (s.fill && s.fillOpacity > 0) {
    const color = s.fillOpacity < 1 ? toArgb(s.fill, s.fillOpacity) : s.fill;
    const resKey = reverseResolveColor(color);
    attrs.push(`Background="${resKey}"`);
  }

  // Stroke (border)
  if (s.stroke && s.strokeWidth > 0) {
    attrs.push(`BorderBrush="${s.stroke}"`);
    attrs.push(`BorderThickness="${s.strokeWidth}"`);
  }

  // Corner radius
  if (s.borderRadius > 0) {
    attrs.push(`CornerRadius="${s.borderRadius}"`);
  }

  // Element name
  attrs.push(`x:Name="${name}"`);

  // Border vs Rectangle: use Border if has stroke or radius, Rectangle otherwise
  if (s.borderRadius > 0 || (s.stroke && s.strokeWidth > 0)) {
    axaml += `${indent}<Border ${attrs.join(' ')} />\n`;
  } else {
    if (s.fill && s.fillOpacity > 0) {
      axaml += `${indent}<Rectangle ${attrs.join(' ')} />\n`;
    } else {
      axaml += `${indent}<Border ${attrs.join(' ')} />\n`;
    }
  }

  return axaml;
}

function renderStrokedRect(s, indent, name) {
  const attrs = [
    `x:Name="${name}"`,
    `Width="${s.width || 100}"`,
    `Height="${s.height || 40}"`,
    `Fill="Transparent"`,
    `Stroke="${s.stroke}"`,
    `StrokeThickness="${s.strokeWidth}"`,
  ];
  if (s.x > 0 || s.y > 0) attrs.push(`Margin="${s.x},${s.y},0,0"`);
  if (s.borderRadius > 0) attrs.push(`RadiusX="${s.borderRadius}"`);
  return `${indent}<Rectangle ${attrs.join(' ')} />\n`;
}

function renderLineDivider(s, indent, name) {
  const attrs = [
    `x:Name="${name}Divider"`,
    `Height="${s.height || 1}"`,
    `Width="${s.width || 100}"`,
  ];
  if (s.fill && s.fillOpacity > 0) attrs.push(`Background="${s.fill}"`);
  else attrs.push(`Background="#20FFFFFF"`);
  if (s.x !== undefined) attrs.push(`Margin="${s.x},${s.y},0,0"`);
  return `${indent}<Border ${attrs.join(' ')} />\n`;
}

// ── Ellipse ──

function renderEllipse(s, indent) {
  const name = safeName(s.name);
  const w = s.width || 20, h = s.height || 20;
  const attrs = [
    `x:Name="${name}"`,
    `Width="${w}"`,
    `Height="${h}"`,
  ];
  if (s.fill && s.fillOpacity > 0) {
    const color = s.fillOpacity < 1 ? toArgb(s.fill, s.fillOpacity) : s.fill;
    attrs.push(`Fill="${reverseResolveColor(color)}"`);
  }
  if (s.x > 0 || s.y > 0) attrs.push(`Margin="${s.x},${s.y},0,0"`);

  // Small circle with fill → might be a radio button dot or thumb
  if (w <= 20 && h <= 20 && s.fill) {
    attrs.push('<!-- dot/thumb/radio -->');
  }

  return `${indent}<Ellipse ${attrs.join(' ')} />\n`;
}

// ── Text → TextBlock ──

function renderText(s, indent) {
  const name = safeName(s.name);
  const text = escapeXml(s.text || '');
  const fontSize = s.fontSize || 14;
  const attrs = [
    `x:Name="${name}"`,
    `Text="${text}"`,
    `FontSize="${fontSize}"`,
  ];

  // Foreground
  if (s.fill && s.fillOpacity > 0) {
    const color = s.fillOpacity < 1 ? toArgb(s.fill, s.fillOpacity) : s.fill;
    attrs.push(`Foreground="${reverseResolveColor(color)}"`);
  }

  // Alignment
  const align = (s.textAlign || 'left').toLowerCase();
  if (align === 'center') attrs.push('HorizontalAlignment="Center"');
  if (align === 'right') attrs.push('HorizontalAlignment="Right"');

  // Font weight
  if (s.fontWeight) {
    const fwMap = { '100':'Thin','200':'ExtraLight','300':'Light','400':'Regular',
                    '500':'Medium','600':'SemiBold','700':'Bold','800':'ExtraBold','900':'Black' };
    attrs.push(`FontWeight="${fwMap[s.fontWeight] || 'Regular'}"`);
  }

  // Position
  if (s.x > 0 || s.y > 0) attrs.push(`Margin="${s.x},${s.y},0,0"`);

  return `${indent}<TextBlock ${attrs.join(' ')} />\n`;
}

// ── Path → Path (via SVG Data) ──

function renderPath(s, indent) {
  const name = safeName(s.name);
  const attrs = [
    `x:Name="${name}"`,
  ];
  if (s.x > 0 || s.y > 0) attrs.push(`Margin="${s.x},${s.y},0,0"`);
  if (s.width) attrs.push(`Width="${s.width}"`);
  if (s.height) attrs.push(`Height="${s.height}"`);
  if (s.fill && s.fillOpacity > 0) attrs.push(`Fill="${s.fill}"`);
  if (s.stroke) attrs.push(`Stroke="${s.stroke}"`);

  // Path data would need to be extracted from SVG children.
  // For groups/Panels, this requires the SVG → path-data extraction.
  attrs.push(`Data="<!-- extract from SVG children -->"`);

  return `${indent}<Path ${attrs.join(' ')} />\n`;
}

// ── Group → Panel / StackPanel / Grid ──

function renderGroup(s, indent, canvasW, canvasH) {
  const name = safeName(s.name);
  const children = s.children || [];

  // Determine layout based on children positions
  const layout = detectGroupLayout(children);

  let tag, attrs = [`x:Name="${name}"`];
  if (layout === 'vertical')   { tag = 'StackPanel'; attrs.push('Orientation="Vertical"'); }
  else if (layout === 'horizontal') { tag = 'StackPanel'; attrs.push('Orientation="Horizontal"'); }
  else                         { tag = 'Panel'; }

  if (s.x > 0 || s.y > 0) attrs.push(`Margin="${s.x},${s.y},0,0"`);
  if (s.width) attrs.push(`Width="${s.width}"`);
  if (s.height) attrs.push(`Height="${s.height}"`);

  let axaml = `${indent}<${tag} ${attrs.join(' ')}>\n`;
  const childIndent = indent + '  ';

  // If group contains paths, render as Panel with Path children
  for (const child of children) {
    const c = child;
    if (c.type === 'path') {
      axaml += renderPath({ ...c, x: c.x - (s.x || 0), y: c.y - (s.y || 0) }, childIndent);
    } else {
      axaml += renderShape({ ...c, x: c.x - (s.x || 0), y: c.y - (s.y || 0) }, 0, canvasW, canvasH).replace(/    /g, childIndent);
    }
  }

  axaml += `${indent}</${tag}>\n`;
  return axaml;
}

// ═══════════════════════════════════════════════════════════════════════════════
// BUTTON DETECTION — Groups bg rectangle + nearby text into <Button>
// ═══════════════════════════════════════════════════════════════════════════════

function detectButtonGroups(shapes) {
  const groups = [];
  const used = new Set();

  for (let i = 0; i < shapes.length; i++) {
    if (used.has(i)) continue;
    const s = shapes[i];

    // A potential button: filled rectangle with borderRadius
    if (s.type !== 'rectangle' || !s.fill || s.fillOpacity === 0 || !s.borderRadius) continue;

    // Find nearby text shapes that could be the button label
    const nearbyText = [];
    for (let j = 0; j < shapes.length; j++) {
      if (i === j || used.has(j)) continue;
      const t = shapes[j];
      if (t.type !== 'text') continue;

      // Text must be roughly centered within the rectangle
      const rectCenterX = (s.x || 0) + (s.width || 0) / 2;
      const rectCenterY = (s.y || 0) + (s.height || 0) / 2;
      const textCenterX = (t.x || 0) + (t.width || 0) / 2;
      const textCenterY = (t.y || 0) + (t.height || 0) / 2;

      const dx = Math.abs(rectCenterX - textCenterX);
      const dy = Math.abs(rectCenterY - textCenterY);

      if (dx < 60 && dy < (s.height || 40) / 2) {
        nearbyText.push({ index: j, shape: t });
      }
    }

    if (nearbyText.length >= 1) {
      const bestText = nearbyText.sort((a, b) => {
        const da = Math.abs((a.shape.y || 0) - (s.y || 0));
        const db = Math.abs((b.shape.y || 0) - (s.y || 0));
        return da - db;
      })[0];

      groups.push({ rectIndex: i, textIndex: bestText.index, rect: s, text: bestText.shape });
      used.add(i);
      used.add(bestText.index);
    }
  }

  return groups;
}

function renderButtonGroup(g, shapes, usedShapes) {
  usedShapes.add(g.rectIndex);
  usedShapes.add(g.textIndex);

  const r = g.rect;
  const t = g.text;
  const name = safeName(t.name || r.name).replace(/_text$/, '').replace(/_bg$/, '');
  const indent = '    ';

  const attrs = [
    `x:Name="${name}Button"`,
    `Width="${r.width || 200}"`,
    `Height="${r.height || 40}"`,
    `CornerRadius="${r.borderRadius || 20}"`,
  ];

  // Background
  if (r.fill && r.fillOpacity > 0) {
    const color = r.fillOpacity < 1 ? toArgb(r.fill, r.fillOpacity) : r.fill;
    attrs.push(`Background="${reverseResolveColor(color)}"`);
  }

  // Position
  if (r.x > 0 || r.y > 0) attrs.push(`Margin="${r.x},${r.y},0,0"`);

  // Foreground (from text)
  let fgColor = '#FFFFFF';
  if (t.fill && t.fillOpacity > 0) {
    fgColor = t.fillOpacity < 1 ? toArgb(t.fill, t.fillOpacity) : t.fill;
  }

  let axaml = `${indent}<Button ${attrs.join(' ')}>\n`;
  axaml += `${indent}  <Button.Content>\n`;
  axaml += `${indent}    <TextBlock Text="${escapeXml(t.text || '')}"\n`;
  axaml += `${indent}                FontSize="${t.fontSize || 14}"\n`;
  axaml += `${indent}                Foreground="${reverseResolveColor(fgColor)}"\n`;
  axaml += `${indent}                HorizontalAlignment="Center"\n`;
  axaml += `${indent}                VerticalAlignment="Center" />\n`;
  axaml += `${indent}  </Button.Content>\n`;
  axaml += `${indent}</Button>\n`;
  return axaml;
}

// ═══════════════════════════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════════════════════════

function detectGroupLayout(children) {
  if (!children || children.length < 2) return 'none';
  // Check if children are stacked vertically or horizontally
  let allXSame = true, allYSame = true;
  let prevX = children[0].x, prevY = children[0].y;
  for (let i = 1; i < children.length; i++) {
    if (Math.abs(children[i].x - prevX) > 5) allXSame = false;
    if (Math.abs(children[i].y - prevY) > 5) allYSame = false;
    prevX = children[i].x;
    prevY = children[i].y;
  }
  if (allXSame) return 'vertical';
  if (allYSame) return 'horizontal';
  return 'none';
}

function buildHeader(w, h, ns) {
  return `<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="${w}"
             Height="${h}">\n`;
}

function buildRootStart(w, h, board) {
  const bg = board && board.fills && board.fills[0]
    ? board.fills[0].fillColor || '#0C0C0E'
    : '#0C0C0E';
  return `  <Grid Background="${bg}">\n`;
}

function buildRootEnd() {
  return `  </Grid>\n</UserControl>\n`;
}

function safeName(name) {
  return (name || 'Element').replace(/[^a-zA-Z0-9_-]/g, '');
}

function escapeXml(str) {
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

/**
 * Convert RGB + opacity to 8-digit hex: #AARRGGBB
 */
function toArgb(hex, opacity) {
  if (opacity >= 1) return hex;
  const alpha = Math.round(opacity * 255).toString(16).padStart(2, '0');
  return '#' + alpha.toUpperCase() + hex.replace('#', '');
}

/**
 * Attempt to reverse-resolve a color to {StaticResource key}.
 * Without a resource map, falls back to raw hex.
 */
const COMMON_COLORS = {
  '#FFFFFF': 'White',
  '#000000': 'Black',
  '#FF0000': 'Red',
  '#00FF00': 'Green',
  '#0000FF': 'Blue',
  '#0C0C0E': 'SurfaceDark',
  '#E5E5E5': 'Gray100',
  '#0078D4': 'AccentBlue',
};

function reverseResolveColor(hex) {
  const upper = hex.toUpperCase();
  if (COMMON_COLORS[upper]) {
    return `{StaticResource ${COMMON_COLORS[upper]}}`;
  }
  return hex;
}

// ═══════════════════════════════════════════════════════════════════════════════
// CLI
// ═══════════════════════════════════════════════════════════════════════════════

async function main() {
  const args = process.argv.slice(2);
  let inputFile = null, outputFile = null;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--output' || args[i] === '-o') outputFile = args[++i];
    else if (!inputFile) inputFile = args[i];
  }

  if (!inputFile) {
    console.error('Usage: node lib/reverse-convert.mjs <shapes.json> [--output output.axaml]');
    console.error('  Reads Penpot shape JSON (from examples/penpot-read-script.js)');
    console.error('  Generates corresponding Avalonia AXAML markup.');
    process.exit(1);
  }

  const data = JSON.parse(readFileSync(resolve(inputFile), 'utf-8'));
  const axaml = penpotToAxaml(data);

  if (outputFile) {
    writeFileSync(resolve(outputFile), axaml, 'utf-8');
    console.error(`Written to: ${outputFile}`);
  }
  console.log(axaml);
}

// Only run CLI if called directly
const isMain = process.argv[1] && (
  process.argv[1].endsWith('reverse-convert.mjs') ||
  process.argv[1].includes('reverse-convert')
);

if (isMain) {
  main().catch(err => { console.error('FATAL:', err.message); process.exit(1); });
}

// =============================================================================
// AXAML → Penpot JS Code Converter (GENERIC — works in ANY project)
//
// Converts ANY Avalonia AXAML file → executable Penpot JavaScript code.
//
// Usage:
//   # Auto-discover component by name
//   node convert.mjs StartPage 1280 800 --project-root "../.."
//
//   # Specific AXAML file path
//   node convert.mjs --axaml-path "path/to/MyScreen.axaml" 1280 800
//
//   # List all discovered screens
//   node convert.mjs --list --project-root "../.."
//
// Architecture:
//   AXAML file → parsed XML → element tree walk → shape descriptors
//   → Penpot JS code (uses storage.* helpers that must be registered in Penpot)
//
// To add support for a NEW element type:
//   1. Add tag to the appropriate TAG list (CONTAINER, TEXT, SHAPE, BUTTON, etc.)
//   2. If it has unique behavior, add a handleXxx() function
//   3. If it's unknown, the GENERIC FALLBACK handler will still try to render it
//   4. Add to isArray() list in parser config if it can appear as siblings
// =============================================================================

import { XMLParser } from 'fast-xml-parser';
import { discoverProject, findComponent, listComponents } from './lib/discovery.mjs';
import { init, resolve, resolveGradient, resolveStyle } from './lib/resolver.mjs';
import { readFile } from 'fs/promises';
import { existsSync } from 'fs';
import { resolve as pathResolve, dirname, basename } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// ═══════════════════════════════════════════════════════════════════════════════
// CONFIGURATION (change these for different projects)
// ═══════════════════════════════════════════════════════════════════════════════

const CONFIG = {
  // Default canvas size (fallback if not specified)
  defaultCanvasW: 1280,
  defaultCanvasH: 800,

  // Default board background color
  defaultBgColor: '#0C0C0E',

  // Ignored directories for discovery
  discoveryIgnore: ['node_modules', '.git', 'bin', 'obj', 'dist', 'build', '.vs', '.idea', 'packages'],

  // Intelligence layer: dummy data templates for common patterns
  intelligence: {
    // For lists (ListBox, ItemsControl, etc.)
    dummyItems: [
      'Summer Vibes 2024', 'Late Night Jazz', 'Workout Mix',
      'Chill Lo-Fi Beats', 'Top Hits Collection',
    ],
    dummySubtitles: ['English [CC]', 'Spanish', 'French', 'German', 'Japanese'],
    dummyFiles: [
      'project_proposal_v3.pdf', 'meeting_notes_july.docx',
      'budget_2026.xlsx', 'design_system.fig', 'sprint_retro.md',
    ],
    dummySettings: ['General', 'Display', 'Audio', 'Network', 'About'],
    dummyTableHeaders: ['Name', 'Status', 'Date', 'Size'],
    dummyTableRows: [
      ['video_001.mp4', 'Ready', '2026-01-15', '2.4 GB'],
      ['audio_podcast.wav', 'Processing', '2026-02-03', '156 MB'],
      ['image_batch.zip', 'Done', '2026-03-22', '890 MB'],
    ],
  },
};

// ── XML Parser ──────────────────────────────────────────────────────────────
// isArray() controls which element types get wrapped in arrays when multiple
// siblings exist. CRITICAL: any element that can appear more than once as
// a sibling MUST be listed here, otherwise fast-xml-parser keeps only the last.
const parser = new XMLParser({
  ignoreAttributes: false,
  attributeNamePrefix: '@_',
  textNodeName: '#text',
  preserveOrder: false,
  // GENERIC: include the most common repeatable Avalonia elements.
  // If your project uses custom controls, add them here.
  isArray: (name) => [
    // Layout
    'Grid', 'StackPanel', 'Border', 'Panel', 'DockPanel', 'WrapPanel',
    'ScrollViewer', 'Viewbox', 'Canvas', 'UniformGrid',
    // Grid definitions
    'Grid.RowDefinitions', 'RowDefinition',
    'Grid.ColumnDefinitions', 'ColumnDefinition',
    // Styles (from resources — parsed but not rendered at runtime)
    'Style', 'Setter', 'Trigger', 'DataTrigger', 'MultiTrigger',
    'Transition', 'VisualStateGroup', 'VisualState', 'VisualTransition',
    // Interactive
    'Button', 'TextBlock', 'Label', 'CheckBox', 'RadioButton', 'ToggleButton',
    'TextBox', 'ComboBox', 'Slider', 'ProgressBar',
    'ListBox', 'ListBoxItem', 'ItemsControl', 'MenuItem', 'TabItem',
    // Shapes
    'Path', 'Rectangle', 'Ellipse', 'Image', 'PathIcon',
    // Data templates (nested inside ListBox etc.)
    'DataTemplate', 'ItemTemplate',
    // Safe: include StackPanel twice for dedup
    'StackPanel',
  ].includes(name),
});

// ═══════════════════════════════════════════════════════════════════════════════
// TAG CLASSIFICATION (GENERIC — extend for any project)
// ═══════════════════════════════════════════════════════════════════════════════
// Each category determines how the element is converted to Penpot shapes.
// Add your project's custom controls to these lists.

const CONTAINER_TAGS = [
  'Grid', 'StackPanel', 'Border', 'Panel', 'DockPanel', 'WrapPanel',
  'ScrollViewer', 'Viewbox', 'Canvas', 'UniformGrid',
];

const TEXT_TAGS = [
  'TextBlock', 'Label', 'Run', 'Span',
];

const SHAPE_TAGS = [
  'Rectangle', 'Ellipse', 'Path',
];

const BUTTON_TAGS = [
  'Button', 'ToggleButton', 'RepeatButton',
];

const INPUT_TAGS = [
  'TextBox', 'PasswordBox', 'ComboBox', 'Slider',
];

const LIST_TAGS = [
  'ListBox', 'ItemsControl', 'ListView', 'DataGrid',
];

const TAB_TAGS = [
  'TabControl', 'TabItem',
];

// Elements to skip entirely (non-visual or handled differently)
const SKIP_TAGS = [
  'UserControl', 'Window', 'Style', 'Setter', 'Trigger',
  'DataTrigger', 'MultiTrigger', 'Transition',
  'VisualStateGroup', 'VisualState', 'VisualTransition',
  'Grid.RowDefinitions', 'Grid.ColumnDefinitions',
  'RowDefinition', 'ColumnDefinition', 'DataTemplate',
];

// ═══════════════════════════════════════════════════════════════════════════════
// MAIN
// ═══════════════════════════════════════════════════════════════════════════════

async function main() {
  const args = process.argv.slice(2);
  let compName = null, canvasW = CONFIG.defaultCanvasW, canvasH = CONFIG.defaultCanvasH;
  let projectRoot = null, axamlPath = null, listOnly = false;
  let widthSet = false; // Track whether width has been explicitly set

  // Parse args (order-independent)
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--project-root' || args[i] === '-p') {
      projectRoot = pathResolve(args[++i] || '.');
    } else if (args[i] === '--axaml-path' || args[i] === '-a') {
      axamlPath = pathResolve(args[++i] || '');
    } else if (args[i] === '--list' || args[i] === '-l') {
      listOnly = true;
    } else if (args[i] === '--verbose' || args[i] === '-v') {
      // Enable verbose logging (handled inline)
    } else if (!compName && !args[i].startsWith('-')) {
      compName = args[i];
    } else if (compName && !isNaN(parseInt(args[i]))) {
      if (!widthSet) {
        canvasW = parseInt(args[i], 10);
        widthSet = true;
      } else {
        canvasH = parseInt(args[i], 10);
      }
    }
  }

  // Determine project root
  if (!projectRoot) {
    // Default: try ../../ (standard tool placement relative to project)
    projectRoot = pathResolve(__dirname, '..', '..');
    if (!existsSync(projectRoot)) {
      projectRoot = pathResolve('.');
    }
  }

  // ── Init discovery + resolver ──
  await discoverProject(projectRoot, { ignore: CONFIG.discoveryIgnore });
  await init(projectRoot);

  // ── --list mode ──
  if (listOnly) {
    const names = listComponents();
    console.error('Discovered screens:');
    for (const n of names) console.error(`  - ${n}`);
    console.error(`\nTotal: ${names.length} screens`);
    console.log(`// Run: node convert.mjs <name> ${CONFIG.defaultCanvasW} ${CONFIG.defaultCanvasH}`);
    return;
  }

  // ── Find the AXAML file ──
  if (!axamlPath) {
    if (!compName) {
      console.error('ERROR: No component name or --axaml-path specified.');
      console.error('Usage: node convert.mjs <ComponentName> [width] [height]');
      console.error('   or: node convert.mjs --axaml-path <file.axaml> [width] [height]');
      console.error('   or: node convert.mjs --list');
      process.exit(1);
    }
    // Auto-discover by name
    const found = findComponent(compName);
    if (!found) {
      console.error(`ERROR: Could not find component "${compName}".`);
      console.error('Available components:');
      for (const n of listComponents()) console.error(`  - ${n}`);
      process.exit(1);
    }
    axamlPath = found.path;
    console.error(`  Found: ${found.relativePath}`);
  }

  if (!existsSync(axamlPath)) {
    console.error(`ERROR: AXAML file not found: ${axamlPath}`);
    process.exit(1);
  }

  // ── Parse AXAML ──
  const axamlContent = await readFile(axamlPath, 'utf-8');
  const parsed = parser.parse(axamlContent);

  // ── Find root element ──
  const rootInfo = findRootElement(parsed);
  if (!rootInfo) {
    console.error('ERROR: No root element found in AXAML');
    process.exit(1);
  }

  // ── Walk tree → shape list ──
  const shapes = [];
  walkElement(rootInfo.el, {
    x: 0, y: 0, w: canvasW, h: canvasH,
    marginL: 0, marginT: 0, marginR: 0, marginB: 0,
    stackY: 0, stackSpacing: 0, stackOrientation: null,
    // Grid row tracking (for proper row-based layout)
    gridRows: null, currentRow: 0, rowHeights: null,
  }, shapes, rootInfo.tag);

  // ── Generate code ──
  const screenName = compName || basename(axamlPath, '.axaml');
  const jsCode = generateCode(screenName, canvasW, canvasH, shapes);
  console.log(jsCode);

  console.error(`  Generated ${shapes.length} shapes for "${screenName}"`);
}

// ═══════════════════════════════════════════════════════════════════════════════
// ELEMENT WALKER
// ═══════════════════════════════════════════════════════════════════════════════
// Recursively walks parsed AXAML tree. Each element:
//   1. Extract attributes + children (with tag names)
//   2. Compute layout context (position, size, stack/grid offsets)
//   3. Dispatch to appropriate handler based on tag classification
//   4. Handlers push shape descriptors to shapes[] array

function walkElement(el, parentCtx, shapes, tagName) {
  if (!el || typeof el !== 'object') return;

  const tag = tagName || guessTag(el);
  const attrs = extractAttrs(el);
  const children = extractChildren(el);

  // Skip non-visual elements (but recurse into their children)
  if (SKIP_TAGS.includes(tag)) {
    for (const [childTag, childEl] of children) {
      walkElement(childEl, parentCtx, shapes, childTag);
    }
    return;
  }

  // Remember shape count before dispatch (to compute actual height after)
  const shapesBefore = shapes.length;

  // Compute position and size
  const ctx = computeContext(tag, attrs, parentCtx);

  // ── Dispatch ──
  if (CONTAINER_TAGS.includes(tag)) {
    handleContainer(tag, attrs, ctx, children, shapes);
  } else if (TEXT_TAGS.includes(tag)) {
    handleTextBlock(attrs, ctx, shapes, tag);
  } else if (SHAPE_TAGS.includes(tag)) {
    if (tag === 'Rectangle') handleRectangle(attrs, ctx, shapes);
    else if (tag === 'Ellipse') handleEllipse(attrs, ctx, shapes);
    else if (tag === 'Path') handlePath(attrs, ctx, shapes);
  } else if (BUTTON_TAGS.includes(tag)) {
    handleButton(attrs, ctx, children, shapes);
  } else if (LIST_TAGS.includes(tag)) {
    handleListBox(tag, attrs, ctx, children, shapes);
  } else if (INPUT_TAGS.includes(tag)) {
    handleInput(tag, attrs, ctx, shapes);
  } else if (TAB_TAGS.includes(tag)) {
    handleTabs(tag, attrs, ctx, children, shapes);
  } else {
    // ── GENERIC FALLBACK: try to render any unknown element ──
    handleFallback(tag, attrs, ctx, children, shapes);
  }

  // After handler runs, advance parent's stack by the element's actual height.
  // We compute actual height by looking at the shapes produced (bottom edge of
  // last shape minus top edge of first shape, or ctx.h if shapes span wider).
  advanceStack(parentCtx, ctx, shapes, shapesBefore);
}

// ── GENERIC FALLBACK HANDLER ────────────────────────────────────────────────
// Tries to render ANY unknown element by checking common properties.
// This means the converter works for custom controls, 3rd-party libraries,
// or future Avalonia elements without any code changes.

function handleFallback(tag, attrs, ctx, children, shapes) {
  const hasText = attrs.Text || attrs.Content || attrs.Header || attrs.Title;
  const hasFill = attrs.Fill || attrs.Background;
  const hasChildren = children.length > 0;

  // If it has text content → try to render as text
  if (hasText && !hasFill) {
    handleTextBlock({ ...attrs, Text: attrs.Text || attrs.Content || attrs.Header || attrs.Title }, ctx, shapes, tag);
    // Still recurse children if any
    if (hasChildren) {
      for (const [childTag, childEl] of children) {
        walkElement(childEl, ctx, shapes, childTag);
      }
    }
    return;
  }

  // If it has fill/background → try to render as rectangle + recurse
  if (hasFill) {
    const bgColor = resolveColorValue(attrs.Fill || attrs.Background);
    if (bgColor) {
      const { color, alpha } = parseHex8(bgColor);
      shapes.push({
        type: 'rect',
        name: attrs.x_Name || `${tag}_${shapes.length}`,
        x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
        fillColor: color, fillOpacity: alpha,
        borderRadius: parseMeasurement(attrs.CornerRadius) || null,
      });
    }
  }

  // Always recurse into children (don't lose subtree)
  for (const [childTag, childEl] of children) {
    walkElement(childEl, ctx, shapes, childTag);
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CONTEXT COMPUTATION (layout engine)
// ═══════════════════════════════════════════════════════════════════════════════
// Computes x, y, w, h for an element based on parent context and attributes.
//
// Layout modes:
//   - Normal: uses alignment (HorizontalAlignment / VerticalAlignment)
//   - StackPanel: uses accumulated stackY/stackX with Spacing
//   - Grid: uses RowDefinitions to position children in specific rows
//
// Grid behavior:
//   When parentCtx.rowHeights exists, children with Grid.Row are positioned
//   at the correct row offset. Auto rows use their content height.
//   Star (*) rows split the remaining space equally.
//
// StackPanel behavior:
//   When parentCtx.stackOrientation is set, the element uses accumulated
//   offset. After computing, the parent's offset advances by this element's
//   size + spacing.

function computeContext(tag, attrs, parentCtx) {
  let x = parentCtx.x, y = parentCtx.y;
  let w = parentCtx.w, h = parentCtx.h;
  let marginL = 0, marginT = 0, marginR = 0, marginB = 0;

  // ── Resolve margin ──
  if (attrs.Margin) {
    const parts = attrs.Margin.split(',').map(s => parseMeasurement(s) || 0);
    if (parts.length === 1) marginL = marginT = marginR = marginB = parts[0];
    else if (parts.length === 2) { marginL = marginR = parts[0]; marginT = marginB = parts[1]; }
    else if (parts.length === 4) { marginL = parts[0]; marginT = parts[1]; marginR = parts[2]; marginB = parts[3]; }
  }

  // ── Resolve dimensions ──
  const hasExplicitH = !!(attrs.Height || attrs.MinHeight || attrs.MaxHeight);
  const hasExplicitW = !!(attrs.Width || attrs.MinWidth || attrs.MaxWidth);

  // When inside a StackPanel, elements without explicit dimensions should NOT
  // fill the parent. They size to their content (handled by advanceStack()).
  if (parentCtx.stackOrientation === 'Vertical' && !hasExplicitH) {
    h = 0; // Content-sized; actual height computed after handler runs
  }
  if (parentCtx.stackOrientation === 'Horizontal' && !hasExplicitW) {
    w = 0; // Content-sized
  }

  // Text elements (TextBlock, Label): content-sized unless explicit height is set
  if (!hasExplicitH && (tag === 'TextBlock' || tag === 'Label')) {
    h = 0; // Content-sized; actual height determined by font size in handler
  }

  if (attrs.Width) { const pw = parseMeasurement(attrs.Width); if (pw !== null) w = pw; }
  if (attrs.Height) { const ph = parseMeasurement(attrs.Height); if (ph !== null) h = ph; }
  if (attrs.MinWidth) { const pmw = parseMeasurement(attrs.MinWidth); if (pmw !== null && pmw > w) w = pmw; }
  if (attrs.MinHeight) { const pmh = parseMeasurement(attrs.MinHeight); if (pmh !== null && pmh > h) h = pmh; }
  if (attrs.MaxWidth) { const pmx = parseMeasurement(attrs.MaxWidth); if (pmx !== null && pmx < w) w = pmx; }
  if (attrs.MaxHeight) { const pmh2 = parseMeasurement(attrs.MaxHeight); if (pmh2 !== null && pmh2 < h) h = pmh2; }

  // In non-stack containers, margin reduces the available size for Stretch-aligned
  // elements. For Center/Bottom-aligned elements, the margin-box shifts the position
  // but the element still fills its original size (unless explicit width/height is set).
  // E.g., Border Margin="12" with Stretch → w=1256, h=776.
  if (!parentCtx.stackOrientation && !hasExplicitW) {
    const halign = attrs.HorizontalAlignment || 'Stretch';
    if (halign === 'Stretch') w = Math.max(0, w - marginL - marginR);
  }
  if (!parentCtx.stackOrientation && !hasExplicitH) {
    const valign = attrs.VerticalAlignment || 'Stretch';
    if (valign === 'Stretch') h = Math.max(0, h - marginT - marginB);
  }

  // ── Grid row positioning ──
  // When parent has rowHeights, use Grid.Row to compute Y position.
  if (parentCtx.rowHeights && parentCtx.rowHeights.length > 0) {
    const gridRow = parseInt(attrs['Grid.Row'] || parentCtx.currentRow || '0', 10);
    // Sum heights of all previous rows
    let rowY = parentCtx.y;
    for (let i = 0; i < gridRow; i++) {
      rowY += (parentCtx.rowHeights[i] || 0);
    }
    y = rowY + marginT;
    // For 'Auto' rows, set height to 0 to be content-sized; * rows use proportional height
    const rowHeight = parentCtx.rowHeights[gridRow] || 0;
    if (rowHeight > 0) {
      h = rowHeight - marginT - marginB;
    }
  }

  // ── Horizontal alignment ──
  // In Avalonia, margin offsets from the aligned position.
  // E.g., Center + marginL=10 → positioned 10px right of the true center.
  const halign = attrs.HorizontalAlignment || 'Stretch';
  if (parentCtx.stackOrientation === 'Horizontal') {
    // StackPanel horizontal: use accumulated X (advancement happens in advanceStack)
    x = parentCtx.x + (parentCtx.stackX || 0) + marginL;
  } else if (halign === 'Center') {
    x = parentCtx.x + (parentCtx.w - w) / 2 + marginL;
  } else if (halign === 'Right') {
    x = parentCtx.x + parentCtx.w - w - marginR;
  } else {
    // Stretch / Left
    x = parentCtx.x + marginL;
  }

  // ── Vertical alignment ──
  const valign = attrs.VerticalAlignment || 'Stretch';
  if (parentCtx.stackOrientation === 'Vertical') {
    // StackPanel vertical: use accumulated Y (advancement happens in advanceStack)
    y = parentCtx.y + parentCtx.stackY + marginT;
  } else if (!parentCtx.rowHeights && valign === 'Center') {
    y = parentCtx.y + (parentCtx.h - h) / 2 + marginT;
  } else if (!parentCtx.rowHeights && valign === 'Bottom') {
    y = parentCtx.y + parentCtx.h - h - marginB;
  } else if (!parentCtx.rowHeights) {
    // Top / Stretch
    y = parentCtx.y + marginT;
  }

  return { x, y, w, h, marginL, marginT, marginR, marginB };
}

// AFTER a handler produces shapes, advance the parent's stack so the next
// sibling is positioned correctly. We compute the actual element height from
// the shapes that were just created, not from ctx.h (which may be 0).
//
// For vertical stacks: advances stackY by actual_height + margin + spacing.
// For horizontal stacks: advances stackX by actual_width + margin + spacing.

function advanceStack(parentCtx, ctx, shapes, shapesBefore) {
  if (!parentCtx.stackOrientation) return;

  // Compute actual element dimensions from the shapes just produced
  let actualW = ctx.w, actualH = ctx.h;
  const newShapes = shapes.slice(shapesBefore);
  if (newShapes.length > 0) {
    // Find the bounding box of all new shapes
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const s of newShapes) {
      const sx = s.x || 0, sy = s.y || 0;
      const sw = s.w || 0, sh = s.h || 0;
      if (sx < minX) minX = sx;
      if (sy < minY) minY = sy;
      if (sx + sw > maxX) maxX = sx + sw;
      if (sy + sh > maxY) maxY = sy + sh;
    }
    actualW = Math.max(actualW, maxX - minX);
    actualH = Math.max(actualH, maxY - minY);
  }

  // Use a minimum height/width so zero-sized elements still advance
  if (actualH <= 0) actualH = 20;
  if (actualW <= 0) actualW = 20;

  if (parentCtx.stackOrientation === 'Vertical') {
    parentCtx.stackY += ctx.marginT + actualH + ctx.marginB + (parentCtx.stackSpacing || 0);
  } else if (parentCtx.stackOrientation === 'Horizontal') {
    parentCtx.stackX += ctx.marginL + actualW + ctx.marginR + (parentCtx.stackSpacing || 0);
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ELEMENT HANDLERS
// ═══════════════════════════════════════════════════════════════════════════════

// ── Container (Grid, StackPanel, Border, etc.) ─────────────────────────
function handleContainer(tag, attrs, ctx, children, shapes) {
  // Skip hidden elements (IsVisible=False)
  if (attrs.IsVisible === 'False') return;

  // Background rect: only create if the container has visual properties
  const hasVisual = attrs.Background || attrs.BorderBrush || attrs.CornerRadius ||
                    attrs.BorderThickness || tag === 'Border';
  const bg = resolveColorValue(attrs.Background);
  const isGradient = attrs.Background && resolveGradient(attrs.Background);

  if (hasVisual && (bg || isGradient)) {
    if (isGradient) {
      // Gradient background → use gradient fill
      const g = isGradient;
      const stops = g.stops.map(s => ({
        offset: s.offset, color: s.color.length === 9 ? '#' + s.color.substring(3) : s.color,
        opacity: s.color.length === 9 ? parseInt(s.color.substring(1, 3), 16) / 255 : 1,
      }));
      const opts = g.type === 'linear'
        ? { startX: g.startPoint.x / 100, startY: g.startPoint.y / 100, endX: g.endPoint.x / 100, endY: g.endPoint.y / 100 }
        : { centerX: g.center.x / 100, centerY: g.center.y / 100 };
      shapes.push({
        type: 'gradientRect',
        name: `${attrs.x_Name || tag}_bg`, x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
        gradientType: g.type, stops, options: opts,
        borderRadius: parseMeasurement(attrs.CornerRadius) || null,
      });
    } else {
      const { color, alpha } = parseHex8(bg);
      shapes.push({
        type: 'rect',
        name: `${attrs.x_Name || tag}_bg`, x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
        fillColor: color, fillOpacity: alpha,
        borderRadius: parseMeasurement(attrs.CornerRadius) || null,
      });
    }
  }

  // Grid: parse RowDefinitions
  if (tag === 'Grid') {
    ctx.rowHeights = parseGridRows(attrs, ctx.h);
    ctx.currentRow = 0;
  }

  // StackPanel: propagate orientation + spacing
  if (tag === 'StackPanel') {
    ctx.stackOrientation = attrs.Orientation || 'Vertical';
    ctx.stackSpacing = parseMeasurement(attrs.Spacing) || 0;
    ctx.stackY = 0;
    ctx.stackX = 0;
  }

  // Recurse children
  for (let i = 0; i < children.length; i++) {
    const [childTag, childEl] = children[i];
    // Track Grid.Row for proper positioning
    const childAttrs = extractAttrs(childEl);
    if (ctx.rowHeights) {
      const gridRow = parseInt(childAttrs['Grid.Row'] || ctx.currentRow, 10);
      ctx.currentRow = gridRow;
    }
    walkElement(childEl, ctx, shapes, childTag);
    // Increment row after each child (if not explicitly set)
    if (ctx.rowHeights) {
      const childRow = parseInt(childAttrs['Grid.Row'] || '0', 10);
      const nextChildRow = i + 1 < children.length
        ? parseInt(extractAttrs(children[i + 1][1])['Grid.Row'] || '0', 10)
        : childRow + 1;
      if (nextChildRow === childRow) ctx.currentRow = childRow + 1;
      else ctx.currentRow = nextChildRow;
    }
  }

  // ── Intelligence: detect "recent" / "files" containers and generate dummy list items ──
  if (tag === 'StackPanel' || tag === 'ScrollViewer') {
    const name = (attrs.x_Name || '').toLowerCase();
    if (name.includes('recent') || name.includes('filelist')) {
      generateDummyListItems(ctx, shapes, tag);
    }
  }
}

// ── TextBlock / Label ─────────────────────────────────────────────────
function handleTextBlock(attrs, ctx, shapes, tag) {
  const text = attrs.Text || el_text(attrs) || '';
  if (!text || text.trim() === '') return;

  // Resolve font properties from Typography CSS classes
  const classList = attrs.Classes ? attrs.Classes.split(/\s+/).filter(Boolean) : [];
  const styleProps = resolveStyle(tag || 'TextBlock', classList);

  // FontSize: explicit attr > CSS class > default 14
  const fontSize = parseMeasurement(attrs.FontSize) ||
                   parseMeasurement(styleProps.FontSize) || 14;

  // FontWeight: explicit attr > CSS class > "400"
  let fontWeight = resolveFontWeight(attrs.FontWeight || styleProps.FontWeight || 'Normal');

  // Foreground color
  const fgColor = resolveColorValue(attrs.Foreground);
  const { color, alpha } = fgColor ? parseHex8(fgColor) : { color: '#FFFFFF', alpha: 0.9 };

  // Text alignment
  const halign = (attrs.HorizontalContentAlignment || attrs.HorizontalAlignment || 'Left').toLowerCase();
  const align = halign === 'center' ? 'center' : (halign === 'right' ? 'right' : 'left');

  shapes.push({
    type: 'text',
    name: attrs.x_Name || `Text_${shapes.length}`,
    text, x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
    fontSize, fontWeight: parseInt(fontWeight, 10),
    fillColor: color, fillOpacity: alpha, align,
  });
}

// ── Rectangle ─────────────────────────────────────────────────────────
function handleRectangle(attrs, ctx, shapes) {
  const fillColor = resolveColorValue(attrs.Fill);
  const strokeColor = resolveColorValue(attrs.Stroke);
  const strokeThickness = parseMeasurement(attrs.StrokeThickness) || 0;
  const cornerRadius = parseMeasurement(attrs.RadiusX) || parseMeasurement(attrs.CornerRadius) || null;

  if (fillColor) {
    const { color, alpha } = parseHex8(fillColor);
    shapes.push({
      type: 'rect', name: attrs.x_Name || `Rect_${shapes.length}`,
      x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
      fillColor: color, fillOpacity: alpha, borderRadius: cornerRadius,
    });
  }
  if (strokeColor && strokeThickness > 0) {
    const { color, alpha } = parseHex8(strokeColor);
    shapes.push({
      type: 'rect', name: (attrs.x_Name || 'Rect') + '_stroke',
      x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
      fillColor: null, fillOpacity: 0,
      strokeColor: color, strokeOpacity: alpha, strokeWidth: strokeThickness,
      borderRadius: cornerRadius,
    });
  }
}

// ── Ellipse ────────────────────────────────────────────────────────────
function handleEllipse(attrs, ctx, shapes) {
  const fillColor = resolveColorValue(attrs.Fill);
  if (fillColor) {
    const { color, alpha } = parseHex8(fillColor);
    shapes.push({
      type: 'ellipse', name: attrs.x_Name || `Ellipse_${shapes.length}`,
      x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
      fillColor: color, fillOpacity: alpha,
    });
  }
}

// ── Path (vector shapes via SVG import) ────────────────────────────────
function handlePath(attrs, ctx, shapes) {
  const data = attrs.Data;
  if (!data) return;

  const fillColor = resolveColorValue(attrs.Fill);
  const strokeColor = resolveColorValue(attrs.Stroke);
  const strokeThickness = parseMeasurement(attrs.StrokeThickness) || 0;

  let svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${ctx.w} ${ctx.h}">`;
  svg += `<path d="${data}"`;
  if (fillColor) {
    const { color, alpha } = parseHex8(fillColor);
    svg += ` fill="${color}" fill-opacity="${alpha}"`;
  } else {
    svg += ' fill="none"';
  }
  if (strokeColor && strokeThickness > 0) {
    const { color, alpha } = parseHex8(strokeColor);
    svg += ` stroke="${color}" stroke-opacity="${alpha}" stroke-width="${strokeThickness}"`;
  }
  svg += '/></svg>';

  shapes.push({
    type: 'svg', name: attrs.x_Name || `Path_${shapes.length}`,
    x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h, svg,
  });
}

// ── Button ─────────────────────────────────────────────────────────────
function handleButton(attrs, ctx, children, shapes) {
  const bgColor = resolveColorValue(attrs.Background);
  const fgColor = resolveColorValue(attrs.Foreground);
  const { color: bg, alpha: bgA } = bgColor ? parseHex8(bgColor) : { color: '#FFFFFF', alpha: 0.12 };
  const { color: fg, alpha: fgA } = fgColor ? parseHex8(fgColor) : { color: '#E5E5E5', alpha: 1 };
  const cornerRadius = parseMeasurement(attrs.CornerRadius) || 20;
  // Button width: use full context width (already constrained by parent MaxWidth)
  const btnW = ctx.w > 0 ? ctx.w : 200;
  const btnH = ctx.h > 40 ? 40 : (ctx.h || 40);

  // Button background
  shapes.push({
    type: 'rect', name: (attrs.x_Name || 'Button') + '_bg',
    x: ctx.x, y: ctx.y, w: btnW, h: btnH,
    fillColor: bg, fillOpacity: bgA, borderRadius: cornerRadius,
  });

  // Button text from child TextBlock or Content attribute
  let btnText = '', btnClasses = '';
  for (const [childTag, childEl] of children) {
    const ca = extractAttrs(childEl);
    if (childTag === 'TextBlock' || TEXT_TAGS.includes(childTag)) {
      btnText = ca.Text || '';
      btnClasses = ca.Classes || '';
    }
  }
  if (!btnText && attrs.Content) btnText = attrs.Content;

  if (btnText) {
    const classList = btnClasses ? btnClasses.split(/\s+/).filter(Boolean) : [];
    const styleProps = resolveStyle('TextBlock', classList);
    const labelSize = parseMeasurement(styleProps.FontSize) || 14;
    shapes.push({
      type: 'text', name: (attrs.x_Name || 'Button') + '_text', text: btnText,
      x: ctx.x, y: ctx.y + (btnH - labelSize) / 2,
      w: btnW, h: labelSize + 4,
      fontSize: labelSize, fontWeight: 600,
      fillColor: fg, fillOpacity: fgA, align: 'center',
    });
  }
}

// ── ListBox / ItemsControl (INTELLIGENCE LAYER) ────────────────────────
function handleListBox(tag, attrs, ctx, children, shapes) {
  // 1. Render any header text above the list
  const hasHeader = attrs.Header;
  if (hasHeader) {
    shapes.push({
      type: 'text', name: `${tag}_header`,
      text: hasHeader, x: ctx.x, y: ctx.y, w: ctx.w, h: 20,
      fontSize: 14, fontWeight: 600,
      fillColor: '#FFFFFF', fillOpacity: 0.7, align: 'left',
    });
  }

  // 2. Generate dummy items based on context clues
  const context = detectListContext(tag, attrs, children);
  const dummyData = getDummyData(context, 5);
  const itemH = 28;
  const spacing = 4;
  let itemY = ctx.y + (hasHeader ? 28 : 0);

  for (let i = 0; i < dummyData.length; i++) {
    const item = dummyData[i];
    // Item background (subtle)
    shapes.push({
      type: 'rect', name: `${tag}_item${i}_bg`,
      x: ctx.x, y: itemY, w: ctx.w, h: itemH,
      fillColor: '#FFFFFF', fillOpacity: 0.05, borderRadius: 4,
    });
    // Item text
    shapes.push({
      type: 'text', name: `${tag}_item${i}`,
      text: item,
      x: ctx.x + 12, y: itemY + (itemH - 12) / 2,
      w: ctx.w - 24, h: 14,
      fontSize: 12, fontWeight: 400,
      fillColor: '#FFFFFF', fillOpacity: 0.8, align: 'left',
    });
    itemY += itemH + spacing;
  }

  // Note: parent's stackY is already advanced via ctx.stackY updates
  // in computeContext(), so no additional update needed here.
}

// ── Input (TextBox, Slider, ComboBox) ─────────────────────────────────
function handleInput(tag, attrs, ctx, shapes) {
  if (tag === 'Slider') {
    // Draw a realistic slider
    const trackH = 4, thumbR = 10;
    const trackY = ctx.y + (ctx.h - trackH) / 2;
    const progress = 0.65; // Default to 65%
    const label = attrs.x_Name || attrs.Header || '';

    if (label) {
      shapes.push({
        type: 'text', name: `${tag}_label`, text: label,
        x: ctx.x, y: ctx.y - 18, w: ctx.w, h: 14,
        fontSize: 12, fontWeight: 400,
        fillColor: '#FFFFFF', fillOpacity: 0.5, align: 'left',
      });
    }

    // Slider track (background)
    shapes.push({
      type: 'rect', name: `${tag}_track_bg`,
      x: ctx.x, y: trackY, w: ctx.w, h: trackH,
      fillColor: '#FFFFFF', fillOpacity: 0.15, borderRadius: 2,
    });
    // Slider track (filled portion)
    shapes.push({
      type: 'rect', name: `${tag}_track_fill`,
      x: ctx.x, y: trackY, w: ctx.w * progress, h: trackH,
      fillColor: '#0078D4', fillOpacity: 1, borderRadius: 2,
    });
    // Slider thumb
    shapes.push({
      type: 'ellipse', name: `${tag}_thumb`,
      x: ctx.x + ctx.w * progress - thumbR, y: trackY + trackH / 2 - thumbR,
      w: thumbR * 2, h: thumbR * 2,
      fillColor: '#FFFFFF', fillOpacity: 1,
    });
  } else {
    // TextBox / ComboBox — draw input field with placeholder
    const placeholder = attrs.Watermark || attrs.PlaceholderText || `Enter ${tag.replace('Box', '')}...`;
    // Input field background
    shapes.push({
      type: 'rect', name: `${tag}_bg`,
      x: ctx.x, y: ctx.y, w: ctx.w, h: ctx.h,
      fillColor: '#FFFFFF', fillOpacity: 0.08, borderRadius: 4,
    });
    // Placeholder text
    shapes.push({
      type: 'text', name: `${tag}_placeholder`, text: placeholder,
      x: ctx.x + 8, y: ctx.y + (ctx.h - 14) / 2,
      w: ctx.w - 16, h: 14,
      fontSize: 12, fontWeight: 400,
      fillColor: '#FFFFFF', fillOpacity: 0.4, align: 'left',
    });
  }
}

// ── Tabs (TabControl / TabItem) ───────────────────────────────────────
function handleTabs(tag, attrs, ctx, children, shapes) {
  if (tag === 'TabControl') {
    // For TabControl, just recurse into TabItems
    for (const [childTag, childEl] of children) {
      walkElement(childEl, ctx, shapes, childTag);
    }
  } else if (tag === 'TabItem') {
    // Render a tab with header
    const header = attrs.Header || attrs.x_Name || 'Tab';
    const tabW = 80, tabH = 32;
    shapes.push({
      type: 'rect', name: `${header}_tab`,
      x: ctx.x, y: ctx.y, w: tabW, h: tabH,
      fillColor: '#FFFFFF', fillOpacity: 0.8, borderRadius: 4,
    });
    shapes.push({
      type: 'text', name: `${header}_label`, text: header,
      x: ctx.x, y: ctx.y + (tabH - 12) / 2,
      w: tabW, h: 14,
      fontSize: 12, fontWeight: 500,
      fillColor: '#000000', fillOpacity: 0.9, align: 'center',
    });
    // Recurse TabItem content if any
    const tCtx = { ...ctx, x: ctx.x, y: ctx.y + tabH + 4, stackY: 0, stackX: 0, stackOrientation: null };
    for (const [childTag, childEl] of children) {
      if (!TAB_TAGS.includes(childTag)) {
        walkElement(childEl, tCtx, shapes, childTag);
      }
    }
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
// INTELLIGENCE LAYER
// ═══════════════════════════════════════════════════════════════════════════════
// These functions detect the CONTEXT of lists, inputs, etc. and generate
// appropriate dummy data. Extend these for your project's use cases.

function detectListContext(tag, attrs, children) {
  const name = (attrs.x_Name || attrs.Header || tag).toLowerCase();
  if (name.includes('playlist') || name.includes('track') || name.includes('song')) return 'playlist';
  if (name.includes('subtitle') || name.includes('language') || name.includes('audio')) return 'subtitle';
  if (name.includes('file') || name.includes('document') || name.includes('recent')) return 'files';
  if (name.includes('setting') || name.includes('preference') || name.includes('config')) return 'settings';
  if (name.includes('table') || name.includes('grid') || name.includes('data')) return 'table';
  return 'generic';
}

function getDummyData(context, count) {
  const data = CONFIG.intelligence;
  switch (context) {
    case 'playlist': return data.dummyItems.slice(0, count);
    case 'subtitle': return data.dummySubtitles.slice(0, count);
    case 'files': return data.dummyFiles.slice(0, count);
    case 'settings': return data.dummySettings.slice(0, count);
    case 'table': return data.dummyTableHeaders.slice(0, count);
    default: return data.dummyItems.slice(0, count);
  }
}

// Generate dummy list items inside containers detected as "recent files"
function generateDummyListItems(ctx, shapes, tag) {
  const items = CONFIG.intelligence.dummyFiles;
  if (!items || items.length === 0) return;
  const itemH = 28;
  const spacing = 4;
  let itemY = ctx.y;
  for (let i = 0; i < items.length; i++) {
    shapes.push({
      type: 'rect', name: `${tag}_recent_item${i}_bg`,
      x: ctx.x + 8, y: itemY, w: Math.max(0, ctx.w - 16), h: itemH,
      fillColor: '#FFFFFF', fillOpacity: 0.05, borderRadius: 4,
    });
    shapes.push({
      type: 'text', name: `${tag}_recent_item${i}`,
      text: items[i],
      x: ctx.x + 20, y: itemY + (itemH - 12) / 2,
      w: Math.max(0, ctx.w - 40), h: 14,
      fontSize: 12, fontWeight: 400,
      fillColor: '#FFFFFF', fillOpacity: 0.8, align: 'left',
    });
    itemY += itemH + spacing;
  }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CODE GENERATION
// ═══════════════════════════════════════════════════════════════════════════════

function generateCode(screenName, canvasW, canvasH, shapes) {
  let code = `// ============================================================================\n`;
  code    += `// ${screenName} — Auto-generated AXAML → Penpot\n`;
  code    += `// Canvas: ${canvasW}×${canvasH} | Shapes: ${shapes.length}\n`;
  code    += `// Execute via MCP execute_code (type: script)\n`;
  code    += `// ============================================================================\n\n`;
  code    += `(function() {\n`;
  code    += `  var root = storage.prepareSinglePage();\n`;
  code    += `  var board = storage.createBoard('${screenName}', ${canvasW}, ${canvasH}, '${CONFIG.defaultBgColor}', 1);\n`;
  code    += `  // Position board on shared canvas, offsetting for existing boards\n`;
  code    += `  board.x = 40 + (storage.screenCounter || 0) * ${canvasW + 40};\n`;
  code    += `  board.y = 40;\n`;
  code    += `  storage.screenCounter = (storage.screenCounter || 0) + 1;\n`;
  code    += `  root.appendChild(board);\n\n`;

  let vc = 0;
  for (const s of shapes) {
    const varName = `s${vc++}`;

    if (s.type === 'rect') {
      const fill = s.fillColor ? `'${s.fillColor}'` : 'null';
      const opacity = s.fillOpacity != null ? s.fillOpacity : 1;
      code += `  var ${varName} = storage.createRect('${s.name}', ${fmt(s.x)}, ${fmt(s.y)}, ${fmt(s.w)}, ${fmt(s.h)}, ${fill}, ${opacity}`;
      if (s.borderRadius) code += `, ${s.borderRadius}`;
      code += `);\n`;
      if (s.strokeColor) {
        code += `  ${varName}.strokes = [{ strokeColor: '${s.strokeColor}', strokeOpacity: ${s.strokeOpacity}, strokeWidth: ${s.strokeWidth} }];\n`;
      }
      code += `  board.appendChild(${varName});\n\n`;
    } else if (s.type === 'ellipse') {
      const fill = s.fillColor ? `'${s.fillColor}'` : 'null';
      code += `  var ${varName} = storage.createEllipse('${s.name}', ${fmt(s.x)}, ${fmt(s.y)}, ${fmt(s.w)}, ${fmt(s.h)}, ${fill}, ${s.fillOpacity});\n`;
      code += `  board.appendChild(${varName});\n\n`;
    } else if (s.type === 'text') {
      const text = escapeJs(s.text);
      code += `  var ${varName} = storage.createText('${s.name}', '${text}', ${s.fontSize}, ${s.fontWeight}, '${s.fillColor}', ${s.fillOpacity}, '${s.align}');\n`;
      code += `  ${varName}.x = ${fmt(s.x)}; ${varName}.y = ${fmt(s.y)};\n`;
      if (s.align === 'center') {
        code += `  storage.centerTextX(${varName}, ${fmt(s.x + s.w / 2)});\n`;
      }
      code += `  board.appendChild(${varName});\n\n`;
    } else if (s.type === 'svg') {
      code += `  var ${varName} = storage.createFromSvg('${s.name}', '${escapeJs(s.svg)}');\n`;
      code += `  ${varName}.x = ${fmt(s.x)}; ${varName}.y = ${fmt(s.y)};\n`;
      code += `  board.appendChild(${varName});\n\n`;
    } else if (s.type === 'gradientRect') {
      code += `  var ${varName} = storage.createGradientRect('${s.name}', ${fmt(s.x)}, ${fmt(s.y)}, ${fmt(s.w)}, ${fmt(s.h)}, '${s.gradientType}',\n`;
      code += `    ${JSON.stringify(s.stops)},\n`;
      code += `    ${JSON.stringify(s.options)});\n`;
      code += `  board.appendChild(${varName});\n\n`;
    }
  }

  code += `  return '${screenName}: ${shapes.length} shapes created';\n`;
  code += `})();\n`;
  return code;
}

// ═══════════════════════════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════════════════════════

function fmt(v) { return Math.round(v); }
function escapeJs(s) { return s.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/\n/g, '\\n').replace(/\r/g, '').replace(/\u2026/g, '...'); }

function el_text(attrs) {
  if (attrs['#text']) return attrs['#text'];
  for (const k of Object.keys(attrs)) {
    if (k === 'Inlines' && Array.isArray(attrs[k])) return attrs[k].map(i => i['#text'] || '').join('');
    if (k === 'Span') return attrs[k]?.['#text'] || '';
  }
  return null;
}

/** Guess the tag name from the first non-attribute, non-text key in the element object */
function guessTag(el) {
  if (!el || typeof el !== 'object') return 'unknown';
  // fast-xml-parser stores XML child elements as object keys (not @_ prefixed).
  // The first such key whose value is an object/array is typically the element's tag name.
  const keys = Object.keys(el);
  // Filter out attribute keys (start with @_), text nodes, and metadata
  const tagKeys = keys.filter(k => !k.startsWith('@_') && k !== '#text' && k !== 'x_Col');
  for (const k of tagKeys) {
    if (typeof el[k] === 'object') return k;
  }
  return 'unknown';
}

/** Extract attributes (keys starting with @_) */
function extractAttrs(el) {
  const attrs = {};
  for (const k of Object.keys(el)) {
    if (k.startsWith('@_')) {
      attrs[k.substring(2)] = parseResourceRef(el[k]);
    } else if (typeof el[k] === 'string' || typeof el[k] === 'number') {
      if (!['#text'].includes(k) && !k.startsWith('xmlns')) {
        attrs[k] = parseResourceRef(el[k]);
      }
    }
  }
  // Also check for x:Name (fast-xml-parser stores as x_Col?.['@_Name'])
  if (el['x_Col']) attrs.x_Name = el['x_Col']['@_Name'];
  return attrs;
}

/** Resolve {StaticResource} / {DynamicResource} references inline */
function parseResourceRef(val) {
  if (typeof val !== 'string') return val;
  const res = resolve(val); // resolver handles both {StaticResource key} and {DynamicResource key}
  return res || val;
}

/** Extract children as [tagName, element] tuples */
function extractChildren(el) {
  const result = [];
  const skipKeys = new Set(SKIP_TAGS);
  for (const k of Object.keys(el)) {
    if (k.startsWith('@_') || k === '#text' || k === 'x_Col' || k === 'xmlns' || k === 'xmlns_x') continue;
    if (skipKeys.has(k)) continue; // Skip style/metadata elements
    const val = el[k];
    if (Array.isArray(val)) {
      for (const item of val) {
        if (item && typeof item === 'object') result.push([k, item]);
      }
    } else if (val && typeof val === 'object') {
      result.push([k, val]);
    }
  }
  return result;
}

/** Find the root visual element, skipping wrappers like UserControl/Window */
function findRootElement(parsed) {
  const topKey = Object.keys(parsed).find(k => !k.startsWith('?xml') && !k.startsWith('Styles'));
  if (!topKey) return null;
  let el = parsed[topKey];
  let tag = topKey;

  // Unwrap UserControl/Window: they wrap the actual UI in a child element
  while (el && typeof el === 'object') {
    const children = extractChildren(el);
    if (children.length === 1 && (tag === 'UserControl' || tag === 'Window')) {
      tag = children[0][0];
      el = children[0][1];
    } else {
      break;
    }
  }

  return { tag, el };
}

/** Parse Grid RowDefinitions into heights array */
function parseGridRows(attrs, totalH) {
  const rowDefs = attrs['RowDefinitions'] || attrs['Grid.RowDefinitions'];
  if (!rowDefs) return null;

  const rows = [];
  const defs = rowDefs.RowDefinition || rowDefs;
  const list = Array.isArray(defs) ? defs : [defs];

  let starCount = 0;
  for (const d of list) {
    const h = (d['@_Height'] || d.Height || '').toLowerCase();
    if (h.includes('*')) {
      const mult = parseFloat(h.replace('*', '')) || 1;
      rows.push({ type: 'star', mult });
      starCount += mult;
    } else if (h === 'auto') {
      rows.push({ type: 'auto', val: 0 });
    } else {
      const val = parseMeasurement(h) || 0;
      rows.push({ type: 'pixel', val });
    }
  }

  // Compute actual heights
  let autoUsed = 0, pixelUsed = 0;
  for (const r of rows) {
    if (r.type === 'pixel') pixelUsed += r.val;
    if (r.type === 'auto') autoUsed += 40; // Default auto height
  }
  const remaining = Math.max(0, totalH - pixelUsed - autoUsed);

  return rows.map(r => {
    if (r.type === 'star') return (remaining * r.mult) / starCount;
    if (r.type === 'auto') return 40;
    return r.val;
  });
}

/** Parse a measurement value: "14", "24px", "{StaticResource space-5}" → number */
function parseMeasurement(val) {
  if (!val && val !== 0) return null;
  if (typeof val === 'number') return val;
  if (typeof val !== 'string') return null;
  if (val.startsWith('{StaticResource') || val.startsWith('{DynamicResource')) {
    const key = val.match(/\{(?:Static|Dynamic)Resource\s+(\S+)\}/)?.[1];
    if (key) {
      const res = resolve(key);
      if (res) return parseFloat(res) || null;
    }
    return null;
  }
  return parseFloat(val.replace('px', '').trim()) || null;
}

/** Parse 8-digit hex (#AARRGGBB) → { color: '#RRGGBB', alpha: 0-1 } */
function parseHex8(hex) {
  if (!hex || !hex.startsWith('#')) return { color: hex || '#000000', alpha: 1 };
  if (hex.length === 9) {
    return {
      color: '#' + hex.substring(3),
      alpha: Math.round((parseInt(hex.substring(1, 3), 16) / 255) * 100) / 100,
    };
  }
  return { color: hex, alpha: 1 };
}

/** Resolve font weight: "Bold" → "700", "Regular" → "400", etc. */
function resolveFontWeight(raw) {
  const fwMap = {
    'thin': '100', 'extralight': '200', 'light': '300',
    'normal': '400', 'regular': '400', 'medium': '500',
    'semibold': '600', 'bold': '700', 'extrabold': '800', 'black': '900',
  };
  const lc = (raw || 'normal').toLowerCase();
  if (fwMap[lc]) return fwMap[lc];
  const n = parseInt(raw, 10);
  if (n >= 100) return String(n);
  return '400';
}

/** Resolve a color value including {StaticResource/DynamicResource} */
function resolveColorValue(val) {
  if (!val) return null;
  if (typeof val === 'string' && val.startsWith('#')) return val;
  const res = resolve(val);
  return res || null;
}

export { CONFIG, generateCode, walkElement, findRootElement,
  parseGridRows, extractAttrs, extractChildren, parseHex8, parseMeasurement, resolveColorValue };

// ── Run only when executed directly ──
const isMain = process.argv[1] &&
  pathResolve(process.argv[1]) === pathResolve(fileURLToPath(import.meta.url));
if (isMain) {
  main().catch(err => {
    console.error('FATAL:', err.message);
    process.exit(1);
  });
}

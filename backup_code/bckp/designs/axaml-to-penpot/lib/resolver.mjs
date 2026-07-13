// ═══════════════════════════════════════════════════════════════════════════════
// AXAML Resource Resolver (GENERIC — works in ANY project)
//
// What it does:
//   1. Auto-discovers ALL .axaml resource files in a project (no hardcoded paths)
//   2. Parses: SolidColorBrush, Color, LinearGradientBrush, RadialGradientBrush,
//      x:Double, CornerRadius, Thickness, and Typography Style selectors
//   3. Provides key→value lookups for {StaticResource} and {DynamicResource}
//   4. Resolves CSS-style classes from Typography/Styles.axaml
//   5. Handles gradients fully (multiple stops, not just last color)
//
// How to use:
//   import { init, resolve, resolveGradient, resolveStyle } from './lib/resolver.mjs';
//   await init(projectRoot);  // auto-discovers all resources
//   resolve('space-5');       // → "5"
//   resolveGradient('AccentGradient'); // → { type: 'linear', stops: [...] }
// ═══════════════════════════════════════════════════════════════════════════════

import { readFileSync, existsSync } from 'fs';
import { resolve as pathResolve } from 'path';
import { discoverProject, getResourceDir } from './discovery.mjs';

// ── Module-level caches ─────────────────────────────────────────────────
let _resMap = null;        // key → value (for simple resources)
let _gradients = null;     // key → gradient descriptor
let _styleClasses = null;  // selector → {property: value} (from Typography/Styles)

// ── Initialization (call once) ──────────────────────────────────────────
/**
 * Initialize the resolver by discovering and parsing ALL resource files.
 * Call this ONCE before using any resolve functions.
 *
 * @param {string} projectRoot - Root directory of the Avalonia project
 */
export async function init(projectRoot) {
  if (_resMap) return; // Already initialized

  // 1. Discover the project structure
  const discovery = await discoverProject(projectRoot);

  // 2. Parse every resource file found
  _resMap = {};
  _gradients = {};
  _styleClasses = {};

  for (const res of discovery.resources) {
    parseResourceFile(res.path, res.name);
  }

  // 3. If no explicit resource files found, scan the entire project for any .axaml
  //    files that might contain resource dictionaries (nested <Styles.Resources>)
  if (Object.keys(_resMap).length === 0) {
    for (const comp of discovery.components) {
      parseResourceFile(comp.path, comp.name, /* strict: */ false);
    }
  }

  const keyCount = Object.keys(_resMap).length;
  const gradCount = Object.keys(_gradients).length;
  const styleCount = Object.keys(_styleClasses).length;

  if (keyCount + gradCount + styleCount > 0) {
    console.error(`  [resolver] Loaded: ${keyCount} values, ${gradCount} gradients, ${styleCount} style selectors`);
  }
}

// ── Generic Resource File Parser ────────────────────────────────────────
/**
 * Parse a single .axaml file for resources.
 * Extracts: SolidColorBrush, Color, gradients, doubles, corner radii,
 * thicknesses, and Typography style selectors.
 *
 * @param {string} filePath - Absolute path to the .axaml file
 * @param {string} label - Human-readable label for logging
 * @param {boolean} strict - If false, also scans for nested resource sections
 */
function parseResourceFile(filePath, label, strict = true) {
  if (!existsSync(filePath)) return;
  const content = readFileSync(filePath, 'utf-8');
  let count = 0;

  // ── Pattern A: SolidColorBrush — <SolidColorBrush x:Key="name" Color="#HEX" />
  for (const m of content.matchAll(/<(SolidColorBrush)\s+x:Key="([^"]+)"\s+Color="([^"]+)"\s*\/?>/g)) {
    _resMap[m[2]] = m[3].toUpperCase(); count++;
  }

  // ── Pattern B: Color element — <Color x:Key="name">#HEX</Color>
  for (const m of content.matchAll(/<Color\s+x:Key="([^"]+)"(?:\s+Color="([^"]+)")?\s*(?:\/>|>([^<]*)<\/Color>)/g)) {
    _resMap[m[1]] = (m[2] || (m[3] || '').trim()).toUpperCase(); count++;
  }

  // ── Pattern C: LinearGradientBrush (FULL gradient support) ──
  for (const m of content.matchAll(/<LinearGradientBrush\s+x:Key="([^"]+)"[^>]*StartPoint="([^"]*)"[^>]*EndPoint="([^"]*)"[^>]*>/g)) {
    const key = m[1];
    const startPoint = m[2] || '0%,0%';
    const endPoint = m[3] || '0%,100%';
    const blockEnd = content.indexOf('</LinearGradientBrush>', m.index);
    const block = blockEnd > -1 ? content.slice(m.index, blockEnd) : '';
    const stops = [];
    for (const sm of block.matchAll(/<GradientStop[^>]*Offset="([^"]+)"[^>]*Color="([^"]+)"[^>]*\/?>/g)) {
      stops.push({ offset: parseFloat(sm[1]) || 0, color: sm[2].toUpperCase() });
    }
    if (stops.length > 0) {
      _gradients[key] = {
        type: 'linear',
        startPoint: parsePoint(startPoint),
        endPoint: parsePoint(endPoint),
        stops,
      };
      // Also store last stop color as solid fallback
      _resMap[key] = stops[stops.length - 1].color;
      count++;
    }
  }

  // ── Pattern D: RadialGradientBrush ──
  for (const m of content.matchAll(/<RadialGradientBrush\s+x:Key="([^"]+)"[^>]*Center="([^"]*)"[^>]*GradientOrigin="([^"]*)"[^>]*>/g)) {
    const key = m[1];
    const center = m[2] || '50%,50%';
    const origin = m[3] || '50%,50%';
    const blockEnd = content.indexOf('</RadialGradientBrush>', m.index);
    const block = blockEnd > -1 ? content.slice(m.index, blockEnd) : '';
    const stops = [];
    for (const sm of block.matchAll(/<GradientStop[^>]*Offset="([^"]+)"[^>]*Color="([^"]+)"[^>]*\/?>/g)) {
      stops.push({ offset: parseFloat(sm[1]) || 0, color: sm[2].toUpperCase() });
    }
    if (stops.length > 0) {
      _gradients[key] = { type: 'radial', center: parsePoint(center), origin: parsePoint(origin), stops };
      _resMap[key] = stops[stops.length - 1].color;
      count++;
    }
  }

  // ── Pattern E: Numeric resources — <(x|sys):Double x:Key="name">value</...>
  for (const m of content.matchAll(/<(?:x|sys):Double\s+x:Key="([^"]+)"[^>]*>([^<]*)<\/(?:x|sys):Double>/g)) {
    if (m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern F: Integer — <x:Int32 x:Key="name">value</x:Int32>
  for (const m of content.matchAll(/<(?:x|sys):Int(?:16|32|64)\s+x:Key="([^"]+)"[^>]*>([^<]*)<\/(?:x|sys):Int(?:16|32|64)>/g)) {
    if (m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern G: Boolean — <x:Boolean x:Key="name">value</x:Boolean>
  for (const m of content.matchAll(/<(?:x|sys):Boolean\s+x:Key="([^"]+)"[^>]*>([^<]*)<\/(?:x|sys):Boolean>/g)) {
    if (m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern H: String — <x:String x:Key="name">value</x:String>
  for (const m of content.matchAll(/<(?:x|sys):String\s+x:Key="([^"]+)"[^>]*>([^<]*)<\/(?:x|sys):String>/g)) {
    if (m[2] !== undefined) { _resMap[m[1]] = m[2]; count++; }
  }

  // ── Pattern I: CornerRadius — <CornerRadius x:Key="name">value</CornerRadius>
  for (const m of content.matchAll(/<CornerRadius\s+x:Key="([^"]+)"(?:\s+[^>]*)?(?:\/>|>([^<]*)<\/CornerRadius>)/g)) {
    if (m[2] && m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern J: Thickness — <Thickness x:Key="name">value</Thickness>
  for (const m of content.matchAll(/<Thickness\s+x:Key="([^"]+)"(?:\s+[^>]*)?(?:\/>|>([^<]*)<\/Thickness>)/g)) {
    if (m[2] && m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern K: FontFamily — <FontFamily x:Key="name">value</FontFamily>
  for (const m of content.matchAll(/<FontFamily\s+x:Key="([^"]+)"[^>]*>([^<]*)<\/FontFamily>/g)) {
    if (m[2].trim()) { _resMap[m[1]] = m[2].trim(); count++; }
  }

  // ── Pattern L: Typography Style selectors (CSS-style classes) ──
  // Parses: <Style Selector="TextBlock.md3-headline2"><Setter Property="FontSize" Value="34"/></Style>
  for (const block of content.matchAll(/<Style\s+Selector="([^"]+)"[^>]*>([\s\S]*?)<\/Style>/g)) {
    const selector = block[1];
    const body = block[2];
    const props = {};
    for (const s of body.matchAll(/<Setter\s+Property="(\w+)"\s+Value="([^"]*)"\s*\/?>/g)) {
      props[s[1]] = s[2];
    }
    if (Object.keys(props).length > 0) {
      _styleClasses[selector] = props;
      count++;
    }
  }

  // ── Pattern M: Other x:Key patterns (catch-all for unknown resource types) ──
  // Many resource types follow <TypeName x:Key="name" ... /> or <TypeName x:Key="name">value</TypeName>
  for (const m of content.matchAll(/<(?:[a-zA-Z]+\.)?(?:[a-zA-Z]+)\s+x:Key="([^"]+)"(?:[^>]*?)Color="([^"]+)"[^>]*\/?>/g)) {
    const key = m[1], color = m[2];
    // Only add if not already captured by more specific patterns above
    if (!_resMap[key]) { _resMap[key] = color.toUpperCase(); count++; }
  }
}

// ── Public API ──────────────────────────────────────────────────────────
/**
 * Resolve a resource key to its value.
 * Handles both {StaticResource key} and {DynamicResource key} syntax.
 *
 * @param {string} keyOrExpr - Either plain key ("space-5") or "{StaticResource space-5}"
 * @returns {string|null}
 */
export function resolve(keyOrExpr) {
  if (!_resMap || !keyOrExpr) return null;

  // Strip {StaticResource ...} or {DynamicResource ...} wrappers
  let key = keyOrExpr;
  const match = key.match(/\{(?:Static|Dynamic)Resource\s+(\S+)\}/);
  if (match) key = match[1];

  return _resMap[key] || null;
}

/**
 * Resolve a gradient (full descriptor with all stops).
 *
 * @param {string} keyOrExpr
 * @returns {Object|null} { type:'linear'|'radial', stops:[{offset,color}], ... }
 */
export function resolveGradient(keyOrExpr) {
  if (!_gradients) return null;
  let key = keyOrExpr;
  const match = key.match(/\{(?:Static|Dynamic)Resource\s+(\S+)\}/);
  if (match) key = match[1];
  return _gradients[key] || null;
}

/**
 * Resolve CSS-style class properties from Typography/Styles.
 * e.g. ["md3-headline2"] → { FontSize: "34", FontWeight: "Regular" }
 *
 * @param {string} tag - Element type ("TextBlock", "Button", etc.)
 * @param {string[]} classes - List of CSS classes
 * @returns {Object}
 */
export function resolveStyle(tag, classes) {
  if (!_styleClasses || !classes || classes.length === 0) return {};
  const result = {};
  for (const cls of classes) {
    // Try tag-specific: "TextBlock.md3-headline2"
    const tagKey = `${tag}.${cls}`;
    if (_styleClasses[tagKey]) Object.assign(result, _styleClasses[tagKey]);
    // Fallback: generic "md3-headline2"
    else if (_styleClasses[cls]) Object.assign(result, _styleClasses[cls]);
  }
  return result;
}

/**
 * Get ALL loaded resource keys (for debugging/exploration).
 * @returns {string[]}
 */
export function keys() {
  return _resMap ? Object.keys(_resMap) : [];
}

/**
 * Generate an SVG <defs> block with gradient definitions for the given keys.
 * Used when exporting shapes that reference gradients.
 *
 * @param {string[]} gradientKeys
 * @returns {string} SVG <defs> string
 */
export function toSvgDefs(gradientKeys = []) {
  if (!_gradients || gradientKeys.length === 0) return '';
  let defs = '<defs>\n';
  for (const key of gradientKeys) {
    const g = _gradients[key];
    if (!g) continue;
    const id = key.replace(/[^a-zA-Z0-9_-]/g, '_');
    if (g.type === 'linear') {
      defs += `  <linearGradient id="${id}" x1="${g.startPoint.x}%" y1="${g.startPoint.y}%" x2="${g.endPoint.x}%" y2="${g.endPoint.y}%">\n`;
    } else {
      defs += `  <radialGradient id="${id}" cx="${g.center.x}%" cy="${g.center.y}%" fx="${g.origin.x}%" fy="${g.origin.y}%">\n`;
    }
    for (const stop of g.stops) {
      const alpha = stop.color.length === 9 ? parseInt(stop.color.substring(1, 3), 16) / 255 : 1;
      const rgb = stop.color.length === 9 ? '#' + stop.color.substring(3) : stop.color;
      defs += `    <stop offset="${stop.offset * 100}%" stop-color="${rgb}" stop-opacity="${Math.round(alpha * 100) / 100}"/>\n`;
    }
    defs += `  </${g.type === 'linear' ? 'linearGradient' : 'radialGradient'}>\n`;
  }
  defs += '</defs>';
  return defs;
}

// ── Internal helpers ────────────────────────────────────────────────────
function parsePoint(str) {
  const parts = str.split(',').map(s => parseFloat(s.trim().replace('%', '')) || 0);
  return { x: parts[0] || 0, y: parts[1] || 0 };
}

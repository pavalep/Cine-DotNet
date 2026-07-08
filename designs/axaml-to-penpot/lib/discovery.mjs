// ═══════════════════════════════════════════════════════════════════════════════
// AXAML Discovery Module (GENERIC)
//
// Auto-discovers AXAML files, resource files, and project structure
// in ANY Avalonia/C# project — no hardcoded paths.
//
// How it works:
//   1. Given a project root, walks the directory tree
//   2. Finds all .axaml files (UI components, resources, styles)
//   3. Categorizes them: components vs resources vs root files
//   4. Provides lookup functions: findComponent(), listComponents(), etc.
//
// This means ANY project structure works:
//   - src/App/UI/Components/Start/StartPage.axaml
//   - Views/MainWindow.axaml
//   - UI/Screens/Login.axaml
//   - Or literally any path with .axaml files
// ═══════════════════════════════════════════════════════════════════════════════

import { readdir, stat } from 'fs/promises';
import { join, relative, basename, extname } from 'path';

// ── Cache (runs once per session) ────────────────────────────────────────
let _discoveryCache = null;

/**
 * Discover ALL .axaml files in a project.
 *
 * @param {string} projectRoot - Root directory of the project
 * @param {Object} options
 * @param {string[]} options.ignore - Directories to skip (e.g. ['node_modules', 'bin', 'obj'])
 * @returns {Object} {
 *   components: [{ name, path, dirName }],  // UI component screens
 *   resources:  [{ name, path }],            // Resource/style files
 *   controls:   [{ name, path }],            // Custom controls
 *   all:        [{ name, path, category }],  // Everything
 * }
 */
export async function discoverProject(projectRoot, options = {}) {
  if (_discoveryCache) return _discoveryCache;

  const ignore = new Set(options.ignore || [
    'node_modules', '.git', 'bin', 'obj', 'dist', 'build',
    '.vs', '.idea', 'packages', 'TestResults',
  ]);

  const allFiles = [];
  await walkDir(projectRoot, ignore, allFiles);

  // Categorize .axaml files
  const components = [];
  const resources = [];
  const controls = [];

  for (const f of allFiles) {
    const rel = relative(projectRoot, f);
    const name = basename(f, '.axaml');
    const dirName = basename(join(f, '..'));

    const entry = { name, path: f, relativePath: rel };

    // Heuristic categorization (works for ANY project structure)
    if (rel.includes('Resource') || rel.includes('Style') || rel.includes('Theme') ||
        name.includes('Color') || name.includes('Spacing') || name.includes('Radius') ||
        name.includes('Typography') || name.includes('Icon')) {
      resources.push(entry);
    } else if (rel.includes('Control') || rel.includes('Template') ||
               name.endsWith('Control') || name.endsWith('Template')) {
      controls.push(entry);
    } else {
      // Component: any other .axaml that's not a resource or control
      components.push({ ...entry, dirName });
    }
  }

  _discoveryCache = { components, resources, controls, all: allFiles };
  console.error(`  [discovery] Found: ${components.length} components, ${resources.length} resources, ${controls.length} controls`);
  return _discoveryCache;
}

/**
 * Find a specific component by name (fuzzy match).
 * Tries: exact match → case-insensitive → partial match → name in path
 *
 * @param {string} name - Component name (e.g. "StartPage", "Start")
 * @returns {Object|null} { name, path, dirName }
 */
export function findComponent(name) {
  if (!_discoveryCache) return null;
  const { components } = _discoveryCache;

  // 1. Exact match
  let match = components.find(c => c.name === name);
  if (match) return match;

  // 2. Case-insensitive
  match = components.find(c => c.name.toLowerCase() === name.toLowerCase());
  if (match) return match;

  // 3. Contains match
  match = components.find(c =>
    c.name.toLowerCase().includes(name.toLowerCase()) ||
    name.toLowerCase().includes(c.name.toLowerCase())
  );
  if (match) return match;

  // 4. Path includes name
  match = components.find(c =>
    c.relativePath.toLowerCase().includes(name.toLowerCase())
  );
  return match || null;
}

/**
 * List all discovered component names (for CLI help).
 * @returns {string[]}
 */
export function listComponents() {
  if (!_discoveryCache) return [];
  return _discoveryCache.components.map(c => c.name);
}

/**
 * Find resource files by keyword (e.g. "Colors", "Typography").
 * @param {string} keyword
 * @returns {Object[]}
 */
export function findResources(keyword) {
  if (!_discoveryCache) return [];
  const kw = keyword.toLowerCase();
  return _discoveryCache.resources.filter(r =>
    r.name.toLowerCase().includes(kw) || r.relativePath.toLowerCase().includes(kw)
  );
}

/**
 * Get the directory containing resource files.
 * If multiple, picks the directory with the most resource files.
 * @returns {string|null}
 */
export function getResourceDir() {
  if (!_discoveryCache || _discoveryCache.resources.length === 0) return null;

  // Group by directory, pick the one with most files
  const dirCount = {};
  for (const r of _discoveryCache.resources) {
    const dir = join(r.path, '..').replace(/\\/g, '/');
    dirCount[dir] = (dirCount[dir] || 0) + 1;
  }
  const best = Object.entries(dirCount).sort((a, b) => b[1] - a[1])[0];
  return best ? best[0] : null;
}

// ── Internal: recursive directory walk ────────────────────────────────
async function walkDir(dir, ignore, results) {
  let entries;
  try {
    entries = await readdir(dir, { withFileTypes: true });
  } catch {
    return; // Skip inaccessible directories
  }

  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (ignore.has(entry.name)) continue;
      await walkDir(join(dir, entry.name), ignore, results);
    } else if (entry.isFile() && entry.name.endsWith('.axaml')) {
      results.push(join(dir, entry.name));
    }
  }
}

/**
 * Clear the discovery cache (for testing or re-scan).
 */
export function clearCache() {
  _discoveryCache = null;
}

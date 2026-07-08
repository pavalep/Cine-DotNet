#!/usr/bin/env node

/**
 * PenPot Generator for Cine Media Player Designs.
 *
 * Converts `converted/*.json` (figmaTree format) into proper `.penpot` ZIP files
 * with editable layers, matching the standard PenPot export format.
 *
 * Usage:
 *   node tools/penpot_generator.mjs
 */

import fs from "fs";
import path from "path";
import os from "os";
import { fileURLToPath } from "url";
import * as archiver from "archiver";
import { v4 as uuidv4 } from "uuid";

// ── Paths ──
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const CONVERTED_DIR = path.join(ROOT, "designs", "Cine-Design-Exact", "converted");
const PENPOT_DIR = path.join(ROOT, "designs", "Cine-Design-Exact", "penpot");
const EXTRACTED_DIR = path.join(PENPOT_DIR, "extracted");
const BACKUP_DIR = path.join(PENPOT_DIR, "backup-old");

function newId() {
  return uuidv4();
}

function makeTransform() {
  return { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 };
}

function makeSelrect(x, y, w, h) {
  return { x, y, width: w, height: h, x1: x, y1: y, x2: x + w, y2: y + h };
}

function makePoints(x, y, w, h) {
  return [
    { x, y },
    { x: x + w, y },
    { x: x + w, y: y + h },
    { x, y: y + h },
  ];
}

function figmaColorToHex(color, opacity = 1.0) {
  const r = Math.round((color.r ?? 1) * 255);
  const g = Math.round((color.g ?? 1) * 255);
  const b = Math.round((color.b ?? 1) * 255);
  return `#${r.toString(16).padStart(2, "0").toUpperCase()}${g.toString(16).padStart(2, "0").toUpperCase()}${b.toString(16).padStart(2, "0").toUpperCase()}`;
}

function figmaFillsToPenpot(fills) {
  if (!fills || !Array.isArray(fills) || fills.length === 0) return [];
  return fills
    .filter((f) => f.type === "SOLID" || f.type === "GRADIENT_LINEAR")
    .map((f) => {
      if (f.type === "SOLID") {
        const color = f.color ?? {};
        const opacity = f.opacity ?? 1.0;
        return { fillColor: figmaColorToHex(color, opacity), fillOpacity: opacity };
      }
      // GRADIENT_LINEAR - approximate as solid using last stop
      const stops = f.gradientStops ?? [];
      if (stops.length > 0) {
        const last = stops[stops.length - 1];
        const color = last.color ?? {};
        const opacity = color.a ?? last.opacity ?? 1.0;
        const c = typeof color === "object" && color.r !== undefined ? color : { r: 1, g: 1, b: 1 };
        return { fillColor: figmaColorToHex(c, 1.0), fillOpacity: parseFloat(opacity) || 1.0 };
      }
      return { fillColor: "#000000", fillOpacity: 1.0 };
    })
    .filter(Boolean);
}

function figmaStrokesToPenpot(strokes) {
  if (!strokes || !Array.isArray(strokes) || strokes.length === 0) return [];
  return strokes.map((s) => ({
    strokeColor: figmaColorToHex(s.color ?? {}, s.opacity ?? 1.0),
    strokeWidth: s.strokeWeight ?? 1,
    strokeOpacity: s.opacity ?? 1.0,
    strokeStyle: "solid",
    strokeAlignment: "center",
  }));
}

function makeTextContent(shapeId, characters, fontFamily = "Segoe UI", fontSize = 14, fills = null) {
  if (!fills) fills = [{ fillColor: "#FFFFFF", fillOpacity: 0.8 }];
  return {
    type: "root",
    key: `${shapeId}-root`,
    children: [
      {
        type: "paragraph-set",
        key: `${shapeId}-ps`,
        children: [
          {
            type: "paragraph",
            key: `${shapeId}-p0`,
            fills,
            fontFamily: "sans-serif",
            fontSize: String(fontSize),
            fontStyle: "normal",
            fontWeight: "400",
            children: [
              {
                type: "text",
                text: characters,
                key: `${shapeId}-t0`,
                fills,
                fontFamily: "sans-serif",
                fontSize: String(fontSize),
              },
            ],
          },
        ],
      },
    ],
  };
}

// ── SVG Parser ──

function parseCSSColor(cssStr) {
  if (!cssStr || cssStr === "none") return null;
  if (cssStr.startsWith("#")) return cssStr.slice(0, 7);
  const rgbMatch = cssStr.match(/rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/);
  if (rgbMatch) {
    const r = parseInt(rgbMatch[1]).toString(16).padStart(2, "0").toUpperCase();
    const g = parseInt(rgbMatch[2]).toString(16).padStart(2, "0").toUpperCase();
    const b = parseInt(rgbMatch[3]).toString(16).padStart(2, "0").toUpperCase();
    return `#${r}${g}${b}`;
  }
  const named = {
    white: "#FFFFFF", black: "#000000", red: "#FF0000", green: "#008000",
    blue: "#0000FF", gray: "#808080", grey: "#808080", silver: "#C0C0C0",
    transparent: null, currentcolor: "#FFFFFF",
  };
  return named[cssStr.toLowerCase()] ?? "#FFFFFF";
}

function parseSvgMarkup(svgMarkup, baseX, baseY) {
  const shapes = [];
  if (!svgMarkup) return shapes;

  // Extract viewBox or use defaults
  const vbMatch = svgMarkup.match(/viewBox\s*=\s*["']([^"']+)["']/);
  let vbX = 0, vbY = 0, vbW = 24, vbH = 24;
  if (vbMatch) {
    const parts = vbMatch[1].split(/[\s,]+/).map(Number);
    if (parts.length >= 4) {
      [vbX, vbY, vbW, vbH] = parts;
    }
  }

  // Get rendered dimensions
  const wMatch = svgMarkup.match(/width\s*=\s*["'](\d+(?:\.\d+)?)["']/);
  const hMatch = svgMarkup.match(/height\s*=\s*["'](\d+(?:\.\d+)?)["']/);
  const svgW = wMatch ? parseFloat(wMatch[1]) : vbW;
  const svgH = hMatch ? parseFloat(hMatch[1]) : vbH;
  const scaleX = vbW > 0 ? svgW / vbW : 1;
  const scaleY = vbH > 0 ? svgH / vbH : 1;

  // Find path elements
  const pathRegex = /<path\b([^>]*?)>/gi;
  let pathMatch;
  while ((pathMatch = pathRegex.exec(svgMarkup)) !== null) {
    const attrs = parseAttributes(pathMatch[1]);
    const d = attrs.d || "";
    if (!d) continue;

    const fillColor = parseCSSColor(attrs.fill);
    const strokeColor = parseCSSColor(attrs.stroke);
    const fOpacity = parseFloat(attrs["fill-opacity"] ?? attrs.opacity ?? "1");
    const sOpacity = parseFloat(attrs["stroke-opacity"] ?? attrs.opacity ?? "1");
    const sw = parseFloat(attrs["stroke-width"] ?? "1");

    const shape = {
      id: newId(),
      name: "path",
      type: "path",
      x: baseX, y: baseY, width: svgW, height: svgH,
      rotation: 0,
      transform: makeTransform(),
      transformInverse: makeTransform(),
      flipX: false, flipY: false,
      opacity: parseFloat(attrs.opacity ?? "1"),
      blocked: false, hiddenBlur: false, hideEmpty: false,
      selrect: makeSelrect(baseX, baseY, svgW, svgH),
      points: makePoints(baseX, baseY, svgW, svgH),
      parentId: null, frameId: null, children: [],
      fills: fillColor ? [{ fillColor, fillOpacity: isNaN(fOpacity) ? 1 : fOpacity }] : [],
      strokes: strokeColor ? [{ strokeColor, strokeWidth: sw, strokeOpacity: isNaN(sOpacity) ? 1 : sOpacity, strokeStyle: "solid", strokeAlignment: "center" }] : [],
      content: d,
    };
    shapes.push(shape);
  }

  // Find rect elements
  const rectRegex = /<rect\b([^>]*?)>/gi;
  let rectMatch;
  while ((rectMatch = rectRegex.exec(svgMarkup)) !== null) {
    const attrs = parseAttributes(rectMatch[1]);
    const rx = parseFloat(attrs.rx ?? "0");
    const ry = parseFloat(attrs.ry ?? "0");
    const x = (parseFloat(attrs.x ?? "0") * scaleX) + baseX;
    const y = (parseFloat(attrs.y ?? "0") * scaleY) + baseY;
    const w = parseFloat(attrs.width ?? "0") * scaleX;
    const h = parseFloat(attrs.height ?? "0") * scaleY;
    const fillColor = parseCSSColor(attrs.fill);
    const strokeColor = parseCSSColor(attrs.stroke);
    const sw = parseFloat(attrs["stroke-width"] ?? "1");

    if (w <= 0 || h <= 0) continue;

    const shape = {
      id: newId(),
      name: "rect",
      type: "rect",
      x, y, width: w, height: h,
      rotation: 0,
      transform: makeTransform(),
      transformInverse: makeTransform(),
      flipX: false, flipY: false,
      opacity: parseFloat(attrs.opacity ?? "1"),
      blocked: false, hiddenBlur: false, hideEmpty: false,
      selrect: makeSelrect(x, y, w, h),
      points: makePoints(x, y, w, h),
      parentId: null, frameId: null, children: [],
      fills: fillColor ? [{ fillColor, fillOpacity: 1.0 }] : [],
      strokes: strokeColor ? [{ strokeColor, strokeWidth: sw || 1, strokeOpacity: 1.0, strokeStyle: "solid", strokeAlignment: "center" }] : [],
      rx, ry,
    };
    shapes.push(shape);
  }

  // Find circle elements
  const circleRegex = /<circle\b([^>]*?)>/gi;
  let circleMatch;
  while ((circleMatch = circleRegex.exec(svgMarkup)) !== null) {
    const attrs = parseAttributes(circleMatch[1]);
    const cx = (parseFloat(attrs.cx ?? "0") * scaleX) + baseX;
    const cy = (parseFloat(attrs.cy ?? "0") * scaleY) + baseY;
    const r = parseFloat(attrs.r ?? "0") * scaleX;
    const fillColor = parseCSSColor(attrs.fill);
    const strokeColor = parseCSSColor(attrs.stroke);
    const sw = parseFloat(attrs["stroke-width"] ?? "1");
    const opacity = parseFloat(attrs.opacity ?? "1");

    if (r <= 0) continue;

    const shape = {
      id: newId(),
      name: "circle",
      type: "circle",
      x: cx - r, y: cy - r, width: r * 2, height: r * 2,
      rotation: 0,
      transform: makeTransform(),
      transformInverse: makeTransform(),
      flipX: false, flipY: false,
      opacity,
      blocked: false, hiddenBlur: false, hideEmpty: false,
      selrect: makeSelrect(cx - r, cy - r, r * 2, r * 2),
      points: makePoints(cx - r, cy - r, r * 2, r * 2),
      parentId: null, frameId: null, children: [],
      fills: fillColor ? [{ fillColor, fillOpacity: 1.0 }] : [],
      strokes: strokeColor ? [{ strokeColor, strokeWidth: sw || 1, strokeOpacity: 1.0, strokeStyle: "solid", strokeAlignment: "center" }] : [],
    };
    shapes.push(shape);
  }

  // Find line elements
  const lineRegex = /<line\b([^>]*?)>/gi;
  let lineMatch;
  while ((lineMatch = lineRegex.exec(svgMarkup)) !== null) {
    const attrs = parseAttributes(lineMatch[1]);
    const x1 = (parseFloat(attrs.x1 ?? "0") * scaleX) + baseX;
    const y1 = (parseFloat(attrs.y1 ?? "0") * scaleY) + baseY;
    const x2 = (parseFloat(attrs.x2 ?? "0") * scaleX) + baseX;
    const y2 = (parseFloat(attrs.y2 ?? "0") * scaleY) + baseY;
    const strokeColor = parseCSSColor(attrs.stroke);
    const sw = parseFloat(attrs["stroke-width"] ?? "1");

    const d = `M ${x1} ${y1} L ${x2} ${y2}`;
    const shape = {
      id: newId(),
      name: "line",
      type: "path",
      x: baseX, y: baseY, width: svgW, height: svgH,
      rotation: 0,
      transform: makeTransform(),
      transformInverse: makeTransform(),
      flipX: false, flipY: false,
      opacity: parseFloat(attrs.opacity ?? "1"),
      blocked: false, hiddenBlur: false, hideEmpty: false,
      selrect: makeSelrect(baseX, baseY, svgW, svgH),
      points: makePoints(baseX, baseY, svgW, svgH),
      parentId: null, frameId: null, children: [],
      fills: [],
      strokes: strokeColor ? [{ strokeColor, strokeWidth: sw || 1, strokeOpacity: 1.0, strokeStyle: "solid", strokeAlignment: "center" }] : [],
      content: d,
    };
    shapes.push(shape);
  }

  return shapes;
}

function parseAttributes(str) {
  const attrs = {};
  const regex = /(\w[-\w]*)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;
  let match;
  while ((match = regex.exec(str)) !== null) {
    attrs[match[1]] = match[2] ?? match[3] ?? "";
  }
  return attrs;
}

// ── Tree Walker ──

class PenpotConverter {
  constructor(figmaTree, pageTitle, viewportWidth = 1920, viewportHeight = 1080) {
    this.figmaTree = figmaTree;
    this.pageTitle = pageTitle;
    this.viewportWidth = viewportWidth;
    this.viewportHeight = viewportHeight;
    this.figmaToPenpot = {};
    this.shapes = {};
    this.rootShapeIds = [];
  }

  convert() {
    const fileId = newId();
    const pageId = newId();

    for (const node of this.figmaTree) {
      this._walkNode(node, null, null, pageId);
    }

    this._assignParents(pageId);
    return { fileId, pageId, shapes: this.shapes };
  }

  _walkNode(node, parentId, frameId, pageId) {
    const nodeId = node.id;
    const nodeType = node.type || "FRAME";
    const nodeName = node.name || "unnamed";
    const x = node.x ?? 0;
    const y = node.y ?? 0;
    const w = node.width ?? 0;
    const h = node.height ?? 0;
    const opacity = node.opacity ?? 1;
    const cornerRadius = node.cornerRadius ?? 0;
    const fills = node.fills ?? [];
    const strokes = node.strokes ?? [];

    if (nodeType === "FRAME" || nodeType === "GROUP") {
      const shapeId = newId();
      this.figmaToPenpot[nodeId] = shapeId;

      const penpotFills = figmaFillsToPenpot(fills);
      const penpotStrokes = figmaStrokesToPenpot(strokes);
      const isRoot = node._pageLayout === true || parentId === null;

      const shape = {
        id: shapeId,
        name: nodeName,
        type: "frame",
        x, y, width: w, height: h,
        rotation: 0,
        transform: makeTransform(),
        transformInverse: makeTransform(),
        flipX: false, flipY: false,
        opacity,
        blocked: false, hiddenBlur: false, hideEmpty: false,
        selrect: makeSelrect(x, y, w, h),
        points: makePoints(x, y, w, h),
        parentId: parentId || shapeId,
        frameId: null,
        children: [],
        fills: penpotFills,
        strokes: penpotStrokes,
        rx: cornerRadius,
        ry: cornerRadius,
        shapes: [],
      };

      this.shapes[shapeId] = shape;

      if (isRoot) {
        this.rootShapeIds.push(shapeId);
      }

      const childIds = [];
      for (const child of (node.children || [])) {
        const cid = this._walkNode(child, shapeId, frameId || shapeId, pageId);
        if (cid) childIds.push(cid);
      }
      shape.children = childIds;
      return shapeId;
    }

    if (nodeType === "TEXT") {
      const shapeId = newId();
      this.figmaToPenpot[nodeId] = shapeId;

      const characters = node.characters || "";
      const fontName = node.fontName || {};
      const fontFamily = fontName.family || "Segoe UI";
      const fontSize = node.fontSize ?? 14;
      const penpotFills = figmaFillsToPenpot(fills);
      const textFills = penpotFills.length > 0 ? penpotFills : [{ fillColor: "#FFFFFF", fillOpacity: 0.8 }];

      const shape = {
        id: shapeId,
        name: nodeName,
        type: "text",
        x, y, width: w, height: h,
        rotation: 0,
        transform: makeTransform(),
        transformInverse: makeTransform(),
        flipX: false, flipY: false,
        opacity,
        blocked: false, hiddenBlur: false, hideEmpty: false,
        selrect: makeSelrect(x, y, w, h),
        points: makePoints(x, y, w, h),
        parentId,
        frameId: null,
        children: [],
        fills: textFills,
        strokes: figmaStrokesToPenpot(strokes),
        content: makeTextContent(shapeId, characters, fontFamily, fontSize, textFills),
      };

      this.shapes[shapeId] = shape;
      return shapeId;
    }

    if (nodeType === "SVG") {
      const svgMarkup = node._svgMarkup || "";
      if (!svgMarkup) return null;

      const shapeId = newId();
      this.figmaToPenpot[nodeId] = shapeId;

      const svgFrame = {
        id: shapeId,
        name: nodeName,
        type: "frame",
        x, y, width: w, height: h,
        rotation: 0,
        transform: makeTransform(),
        transformInverse: makeTransform(),
        flipX: false, flipY: false,
        opacity,
        blocked: false, hiddenBlur: false, hideEmpty: false,
        selrect: makeSelrect(x, y, w, h),
        points: makePoints(x, y, w, h),
        parentId,
        frameId: null,
        children: [],
        fills: [],
        strokes: [],
        rx: 0, ry: 0,
        shapes: [],
      };

      this.shapes[shapeId] = svgFrame;

      const svgShapes = parseSvgMarkup(svgMarkup, 0, 0);
      const childIds = [];
      for (const svgShape of svgShapes) {
        svgShape.parentId = shapeId;
        svgShape.frameId = null;
        this.shapes[svgShape.id] = svgShape;
        childIds.push(svgShape.id);
      }
      svgFrame.children = childIds;
      return shapeId;
    }

    return null;
  }

  _assignParents(pageId) {
    for (const rootId of this.rootShapeIds) {
      const rootShape = this.shapes[rootId];
      if (rootShape) {
        rootShape.frameId = rootId;
        rootShape.parentId = rootId;
        this._assignFrameIdRecursive(rootId, rootId, pageId);
      }
    }
  }

  _assignFrameIdRecursive(shapeId, currentFrameId, pageId) {
    const shape = this.shapes[shapeId];
    if (!shape) return;

    shape.frameId = currentFrameId;
    if (shape.type === "text") {
      shape.pageId = pageId;
    }

    for (const childId of shape.children || []) {
      const child = this.shapes[childId];
      if (child) {
        const childFrameId = child.type === "frame" ? childId : currentFrameId;
        this._assignFrameIdRecursive(childId, childFrameId, pageId);
      }
    }
  }
}

// ── PenPot File Builder ──

async function buildPenpotZip(fileId, pageId, pageTitle, shapes) {
  const now = new Date().toISOString().replace(/\.\d+Z$/, "Z");

  const manifest = {
    version: 3,
    type: "penpot/export-files",
    files: [{
      id: fileId,
      name: pageTitle,
      features: ["components/v2", "fdata/shape-data-type", "fdata/path-data", "styles/v2", "layout/grid"],
    }],
  };

  const fileData = {
    id: fileId,
    name: pageTitle,
    revn: 1,
    vern: 0,
    version: 67,
    features: ["components/v2", "fdata/shape-data-type", "fdata/path-data", "styles/v2", "layout/grid"],
    createdAt: now,
    modifiedAt: now,
    pages: [pageId],
    isShared: false,
  };

  const pageData = {
    id: pageId,
    name: "Page 1",
    index: 0,
    background: "#1E1E2E",
    options: { grids: [] },
    objects: shapes,
  };

  // Write to temp file first (archiver v7 uses classes, needs pipe to stream)
  const tmpPath = path.join(os.tmpdir(), `penpot-${fileId}.zip`);
  const archive = new archiver.ZipArchive();
  const dest = fs.createWriteStream(tmpPath);
  archive.pipe(dest);

  archive.append(JSON.stringify(manifest, null, 2), { name: "manifest.json" });
  archive.append(JSON.stringify(fileData, null, 2), { name: `files/${fileId}.json` });
  archive.append(JSON.stringify(pageData, null, 2), { name: `files/${fileId}/pages/${pageId}.json` });

  await archive.finalize();
  await new Promise((resolve, reject) => {
    dest.on("finish", resolve);
    dest.on("error", reject);
  });

  const data = fs.readFileSync(tmpPath);
  fs.rmSync(tmpPath);
  return data;
}

// ── Main ──

async function main() {
  // 1. Backup old penpot files
  console.log("Backing up old .penpot files...");
  if (!fs.existsSync(BACKUP_DIR)) {
    fs.mkdirSync(BACKUP_DIR, { recursive: true });
  }

  const oldFiles = fs.existsSync(PENPOT_DIR)
    ? fs.readdirSync(PENPOT_DIR).filter((f) => f.endsWith(".penpot"))
    : [];

  for (const f of oldFiles) {
    const src = path.join(PENPOT_DIR, f);
    const dest = path.join(BACKUP_DIR, f);
    fs.renameSync(src, dest);
  }
  console.log(`  Moved ${oldFiles.length} old .penpot files to backup/`);

  // Remove extracted dir from previous investigations
  if (fs.existsSync(EXTRACTED_DIR)) {
    fs.rmSync(EXTRACTED_DIR, { recursive: true, force: true });
  }

  // 2. Read converted files
  if (!fs.existsSync(CONVERTED_DIR)) {
    console.error(`ERROR: Converted directory not found: ${CONVERTED_DIR}`);
    process.exit(1);
  }

  const convertedFiles = fs.readdirSync(CONVERTED_DIR)
    .filter((f) => f.endsWith(".json"))
    .sort();

  if (convertedFiles.length === 0) {
    console.error(`No converted JSON files found in ${CONVERTED_DIR}`);
    process.exit(1);
  }

  console.log(`\nFound ${convertedFiles.length} converted JSON files.`);

  // 3. Generate each .penpot file
  for (const filename of convertedFiles) {
    const inputPath = path.join(CONVERTED_DIR, filename);
    const baseName = filename.replace(/\.json$/, "");
    const outputName = `${baseName}.penpot`;
    const outputPath = path.join(PENPOT_DIR, outputName);

    process.stdout.write(`\n  Generating: ${outputName}... `);

    const data = JSON.parse(fs.readFileSync(inputPath, "utf-8"));
    const figmaTree = data.figmaTree || [];
    const meta = data.meta || {};
    const title = meta.title || baseName;
    const vp = meta.viewport || {};
    const vpW = vp.width || 1920;
    const vpH = vp.height || 1080;

    if (figmaTree.length === 0) {
      console.log(`WARNING: No figmaTree, skipping.`);
      continue;
    }

    const converter = new PenpotConverter(figmaTree, title, vpW, vpH);
    const { fileId, pageId, shapes } = converter.convert();

    process.stdout.write(`${Object.keys(shapes).length} shapes, zipping... `);

    const zipBytes = await buildPenpotZip(fileId, pageId, title, shapes);
    fs.writeFileSync(outputPath, zipBytes);

    const sizeKb = Math.round(zipBytes.length / 1024);
    console.log(`${sizeKb}KB`);
  }

  console.log(`\n✅ Done! Generated ${convertedFiles.length} .penpot files in ${PENPOT_DIR}`);
}

main().catch(console.error);

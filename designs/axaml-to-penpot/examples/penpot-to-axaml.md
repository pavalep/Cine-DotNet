# Example: Reading from Penpot Back to AXAML

**Scenario:** You have a design in Penpot and want to generate AXAML from it.

This demonstrates the BIDIRECTIONAL capability.

## Step 1: Read Shapes from Penpot

Run via MCP `execute_code`:

```javascript
(function() {
  var root = penpot.currentPage.root;
  var all = penpotUtils.findShapes(function(s) { return s.type !== 'svg-raw'; }, root);

  var result = {
    page: penpot.currentPage.name,
    board: null,
    shapes: []
  };

  for (var i = 0; i < all.length; i++) {
    var s = all[i];
    var entry = {
      name: s.name,
      type: s.type,
      x: Math.round(s.x),
      y: Math.round(s.y),
    };

    if (s.type === 'board') {
      result.board = { name: s.name, width: Math.round(s.width), height: Math.round(s.height) };
      continue;
    }

    if (s.width) entry.width = Math.round(s.width);
    if (s.height) entry.height = Math.round(s.height);

    if (s.fills && s.fills.length > 0) {
      entry.fill = s.fills[0].fillColor;
      entry.fillOpacity = s.fills[0].fillOpacity;
    }
    if (s.strokes && s.strokes.length > 0) {
      entry.stroke = s.strokes[0].strokeColor;
      entry.strokeWidth = s.strokes[0].strokeWidth;
    }
    if (s.type === 'text') {
      entry.text = s.characters;
      entry.fontSize = s.fontSize;
    }
    if (s.type === 'rectangle' && s.borderRadius) {
      entry.borderRadius = s.borderRadius;
    }

    result.shapes.push(entry);
  }

  return JSON.stringify(result, null, 2);
})();
```

## Step 2: Map to AXAML

Shape-to-AXAML mapping rules:

| Penpot Shape | AXAML Element | Notes |
|---|---|---|
| board | `<UserControl Width="..." Height="...">` | Root |
| rectangle (bg fill, borderRadius > 0) | `<Button CornerRadius="..." Background="...">` | If contains child text |
| rectangle (bg fill, no borderRadius) | `<Border Background="...">` or `<Rectangle Fill="...">` | |
| rectangle (stroke only, no fill) | `<Rectangle Stroke="..." StrokeThickness="...">` | |
| text | `<TextBlock Text="..." FontSize="..." Foreground="...">` | |
| ellipse | `<Ellipse Fill="..." Width="..." Height="...">` | |
| group (contains paths) | `<Panel>` with `<Path>` children | |

## Prompt to Achieve This

```
Read the current Penpot page "Cine — HomeScreen" and generate the corresponding AXAML.
Map shapes to AXAML elements using the standard mapping rules.
Resolve solid colors back to {StaticResource} keys where possible.
Output the complete .axaml file.
```

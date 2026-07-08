# All Elements Reference — AXAML ↔ Penpot Code Patterns

> **Catalog of code patterns for EVERY Avalonia element type.**  
> NOT actual running code — these are TEMPLATES to guide AI models and developers.  
> For each element: AXAML example → expected Penpot JS → intelligence behavior.

---

## CONTAINERS

### Grid

```xml
<!-- AXAML -->
<Grid Background="{StaticResource SurfaceDark}" Margin="0,0,0,0">
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="48" />
  </Grid.RowDefinitions>

  <!-- Header (row 0) -->
  <Border Grid.Row="0" Height="56" Background="#FF1A1A20">
    <TextBlock Text="Header" />
  </Border>

  <!-- Content (row 1) -->
  <StackPanel Grid.Row="1" Spacing="8" Margin="16,0" />

  <!-- Footer (row 2) -->
  <Border Grid.Row="2" Height="48" Background="#FF222228" />
</Grid>
```

```javascript
// Generated Penpot JS pattern:
var board = storage.createBoard('Screen', 1280, 800, '#0C0C0E', 1);
root.appendChild(board);

// Row 0 (Auto): Header at y=0, height=56
var r0_bg = storage.createRect('Grid_bg', 0, 0, 1280, 56, '#1A1A20', 1);
board.appendChild(r0_bg);

// Row 1 (*): Content at y=56, remaining height = 800-56-48 = 696
// Content area: y=56 to y=752

// Row 2 (pixel): Footer at y=752, height=48
var r2_bg = storage.createRect('Footer_bg', 0, 752, 1280, 48, '#222228', 1);
board.appendChild(r2_bg);
```

### StackPanel (Vertical)

```xml
<!-- AXAML -->
<StackPanel Orientation="Vertical" Spacing="12" Margin="24,16,24,0">
  <TextBlock Text="Section Title" FontSize="20" FontWeight="Bold" />
  <TextBlock Text="Description text goes here" FontSize="14" Foreground="{StaticResource Gray300}" />
  <Button Content="Action" Width="200" />
</StackPanel>
```

```javascript
// Generated Penpot JS pattern:
// Each child stacks vertically with 12px spacing.
// stackY starts at parent.y, advances by each element's height + spacing.

var t0 = storage.createText('Title', 'Section Title', 20, 700, '#FFFFFF', 0.9, 'left');
t0.x = 24; t0.y = 16;  // First child: y = parent.y + stackY (0) + marginT
board.appendChild(t0);
// stackY advances: 0 + 0 + 24 + 0 + 12 = 36

var t1 = storage.createText('Desc', 'Description text goes here', 14, 400, '#E5E5E5', 0.7, 'left');
t1.x = 24; t1.y = 52;  // Second child: y = 16 + 36 = 52
board.appendChild(t1);
// stackY advances: 36 + 0 + 18 + 0 + 12 = 66

// Button bg + text at y = 16 + 66 = 82
```

### StackPanel (Horizontal)

```xml
<!-- AXAML -->
<StackPanel Orientation="Horizontal" Spacing="8">
  <Button Content="Save" Width="100" />
  <Button Content="Cancel" Width="100" />
  <Button Content="Reset" Width="100" />
</StackPanel>
```

```javascript
// Generated Penpot JS pattern:
// Each child stacks horizontally with 8px spacing.
// Three buttons: x at 0, 108, 216
```

### Border

```xml
<!-- AXAML -->
<Border Background="{StaticResource Gray900}" CornerRadius="12"
        BorderBrush="{StaticResource Gray700}" BorderThickness="1"
        Padding="16" Margin="12">
  <StackPanel>
    <TextBlock Text="Card Title" FontWeight="Bold" />
    <TextBlock Text="Card content" Foreground="{StaticResource Gray400}" />
  </StackPanel>
</Border>
```

```javascript
// Generated Penpot JS pattern:
// Border = rect with fill + stroke + cornerRadius
// Margin 12 → w = parent.w - 24, h = parent.h - 24
var border = storage.createRect('Card_bg', 12, 12, 1256, 100, '#1A1A20', 1, 12);
border.strokes = [{ strokeColor: '#3A3A40', strokeOpacity: 1, strokeWidth: 1 }];
board.appendChild(border);

// Padding 16 → children offset by 16px inside the border
// Child x = 12 + 16 = 28, y = 12 + 16 = 28
```

### DockPanel

```xml
<!-- AXAML -->
<DockPanel>
  <Border DockPanel.Dock="Top" Height="48" Background="#FF1A1A20" />
  <Border DockPanel.Dock="Bottom" Height="32" Background="#FF222228" />
  <Border DockPanel.Dock="Left" Width="200" Background="#FF1E1E24" />
  <Grid Background="Transparent" />
</DockPanel>
```

```javascript
// Generated Penpot JS pattern:
// Dock order: Top → Bottom → Left → Right → Fill
// Top bar: y=0, h=48
// Bottom bar: y=752, h=32
// Left sidebar: x=0, y=48, w=200, h=704
// Fill area: x=200, y=48, w=1080, h=704
```

### ScrollViewer

```xml
<!-- AXAML -->
<ScrollViewer>
  <StackPanel Spacing="8">
    <!-- Many items... -->
  </StackPanel>
</ScrollViewer>
```

```javascript
// Penpot JS pattern: ScrollViewer = parent rect + overflow content
// The content extends beyond the visible area.
// No special Penpot shape for scroll — just show the visible area.
var scroller_bg = storage.createRect('ScrollArea', x, y, w, h, '#0C0C0E', 1);
board.appendChild(scroller_bg);
// Children render within the scroller's rect bounds
```

### Viewbox

```xml
<!-- AXAML -->
<Viewbox Stretch="Uniform">
  <Canvas Width="100" Height="100">
    <Ellipse Fill="Red" Width="50" Height="50" Canvas.Left="25" Canvas.Top="25" />
  </Canvas>
</Viewbox>
```

```javascript
// Penpot JS pattern: Viewbox scales content uniformly.
// In Penpot, just render children at their natural size (no scaling needed).
```

---

## TEXT ELEMENTS

### TextBlock (basic)

```xml
<TextBlock Text="Hello World" FontSize="16" FontWeight="Bold" Foreground="#FFFFFF" />
```

```javascript
var t = storage.createText('Hello', 'Hello World', 16, 700, '#FFFFFF', 1, 'left');
t.x = 50; t.y = 100;
board.appendChild(t);
```

### TextBlock with CSS class

```xml
<TextBlock Text="Headline" Classes="md3-headline2" />
```
*Resolved from Typography.axaml: FontSize=34, FontWeight=Regular*

```javascript
var t = storage.createText('Headline', 'Headline', 34, 400, '#FFFFFF', 0.9, 'left');
t.x = 40; t.y = 48;
board.appendChild(t);
```

### TextBlock centered

```xml
<TextBlock Text="Centered" TextAlignment="Center" HorizontalAlignment="Center" Width="300" />
```

```javascript
var t = storage.createText('Centered', 'Centered', 14, 400, '#FFFFFF', 0.9, 'center');
t.x = 490; t.y = 200;  // x = (1280 - 300) / 2
storage.centerTextX(t, 640);  // Re-center based on actual text width
board.appendChild(t);
```

### TextBlock with wrapping

```xml
<TextBlock Text="A very long piece of text that wraps onto multiple lines when it exceeds the available width."
           TextWrapping="Wrap" Width="300" />
```

```javascript
// Penpot text with fixed width will wrap naturally.
// Set growType to 'fixed' for wrapping behavior.
var t = storage.createText('Wrap', 'A very long...', 14, 400, '#FFFFFF', 0.9, 'left');
t.x = 50; t.y = 100; t.w = 300; t.growType = 'fixed';
board.appendChild(t);
```

### Label

```xml
<Label Content="Form Label" Target="inputField" FontSize="14" />
```

```javascript
// Same as TextBlock in Penpot
var l = storage.createText('Label', 'Form Label', 14, 400, '#FFFFFF', 0.9, 'left');
```

### Inline Runs

```xml
<TextBlock>
  <Run Text="Bold" FontWeight="Bold" />
  <Run Text=" and " />
  <Run Text="Normal" />
</TextBlock>
```

```javascript
// Penpot has one font per Text shape — render as single string
var t = storage.createText('Inline', 'Bold and Normal', 14, 400, '#FFFFFF', 0.9, 'left');
// Bold runs use the weight of the containing TextBlock
```

---

## SHAPES

### Rectangle

```xml
<Rectangle Fill="{StaticResource AccentBlue}" Width="100" Height="100"
           RadiusX="8" RadiusY="8" />
```

```javascript
var r = storage.createRect('Rect', 50, 100, 100, 100, '#0078D4', 1, 8);
board.appendChild(r);
```

### Rectangle with stroke

```xml
<Rectangle Stroke="{StaticResource Gray600}" StrokeThickness="2"
           Width="200" Height="60" Fill="Transparent" />
```

```javascript
var r = storage.createRect('StrokeRect', 50, 100, 200, 60, null, 0);
r.strokes = [{ strokeColor: '#505050', strokeOpacity: 1, strokeWidth: 2 }];
board.appendChild(r);
```

### Ellipse

```xml
<Ellipse Fill="{StaticResource AccentBlue}" Width="48" Height="48" />
```

```javascript
var e = storage.createEllipse('Circle', 50, 100, 48, 48, '#0078D4', 1);
board.appendChild(e);
```

### Path (icon)

```xml
<Path Data="{StaticResource PlayIcon}" Fill="{StaticResource White}" />
<!-- PlayIcon = "M8 5v14l11-7z" -->
```

```javascript
// Wrap in SVG for Penpot import
var svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">' +
  '<path d="M8 5v14l11-7z" fill="#FFFFFF" fill-opacity="1"/></svg>';
var icon = storage.createFromSvg('PlayIcon', svg);
icon.x = 100; icon.y = 200;
board.appendChild(icon);
```

### Path with complex data

```xml
<Path Data="M0,0 L100,0 L100,100 L0,100 Z M20,20 L80,20 L80,80 L20,80 Z" Fill="#FF0000" FillRule="EvenOdd" />
```

```javascript
var svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">' +
  '<path d="M0,0 L100,0 L100,100 L0,100 Z M20,20 L80,20 L80,80 L20,80 Z" ' +
  'fill="#FF0000" fill-opacity="1" fill-rule="evenodd"/></svg>';
var p = storage.createFromSvg('ComplexPath', svg);
```

### Image

```xml
<Image Source="{StaticResource Logo}" Width="120" Height="40" Stretch="Uniform" />
```

```javascript
// Images need penpot.createShapeFromSvgWithImages() — Promise-based
// Or: placeholder rectangle with label
var img_place = storage.createRect('Logo_placeholder', 100, 200, 120, 40, '#FFFFFF', 0.08, 4);
board.appendChild(img_place);
var img_label = storage.createText('Logo_label', '<Image: Logo>', 10, 400, '#FFFFFF', 0.3, 'center');
img_label.x = 100; img_label.y = 214;
board.appendChild(img_label);
```

### PathIcon

```xml
<PathIcon Data="{StaticResource SearchIcon}" Width="20" Height="20" />
```

```javascript
// Same as Path — wrap in SVG
var svg = '<svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27..." fill="#FFFFFF"/></svg>';
var icon = storage.createFromSvg('Search', svg);
```

---

## INTERACTIVE ELEMENTS

### Button (standard)

```xml
<Button Content="Submit" Width="200" Height="40"
        Background="{StaticResource AccentBlue}"
        Foreground="{StaticResource White}"
        CornerRadius="20" />
```

```javascript
// Button = background rect + centered text
var btn_bg = storage.createRect('Submit_bg', 100, 300, 200, 40, '#0078D4', 1, 20);
board.appendChild(btn_bg);

var btn_text = storage.createText('Submit_text', 'Submit', 14, 600, '#FFFFFF', 1, 'center');
btn_text.x = 100; btn_text.y = 313;  // Center vertically in 40px button
board.appendChild(btn_text);
```

### Button with icon

```xml
<Button Width="160" Height="40" CornerRadius="8">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <PathIcon Data="{StaticResource PlayIcon}" Width="16" Height="16" />
    <TextBlock Text="Play" VerticalAlignment="Center" />
  </StackPanel>
</Button>
```

```javascript
// Button background
var btn_bg = storage.createRect('PlayBtn_bg', 100, 300, 160, 40, '#FFFFFF', 0.12, 8);
board.appendChild(btn_bg);

// Icon (SVG)
var icon = storage.createFromSvg('PlayIcon', '<svg viewBox="0 0 16 16"><path d="..." fill="#FFFFFF"/></svg>');
icon.x = 118; icon.y = 312;
board.appendChild(icon);

// Label
var label = storage.createText('PlayLbl', 'Play', 14, 600, '#FFFFFF', 1, 'left');
label.x = 142; label.y = 311;
board.appendChild(label);
```

### ToggleButton

```xml
<ToggleButton Content="Toggle Me" Width="120" Height="36" CornerRadius="18"
              IsChecked="{Binding IsEnabled}" />
```

```javascript
// Toggle ON state (filled)
var toggle_on = storage.createRect('Toggle_on_bg', 100, 200, 50, 28, '#0078D4', 1, 14);
board.appendChild(toggle_on);
var thumb_on = storage.createEllipse('Toggle_thumb', 126, 204, 20, 20, '#FFFFFF', 1);
board.appendChild(thumb_on);

// Label
var lbl = storage.createText('ToggleLbl', 'Toggle Me', 14, 400, '#FFFFFF', 0.9, 'left');
lbl.x = 162; lbl.y = 206;
board.appendChild(lbl);
```

### CheckBox

```xml
<CheckBox Content="Enable notifications" IsChecked="True" />
```

```javascript
// Checkbox box (small rect with border)
var cb_box = storage.createRect('CB_Box', 40, 100, 18, 18, '#0078D4', 1, 3);
board.appendChild(cb_box);

// Check mark (SVG tick)
var check_svg = '<svg viewBox="0 0 18 18"><path d="M4 9l4 4 6-8" stroke="#FFFFFF" stroke-width="2" fill="none" stroke-linecap="round"/></svg>';
var check = storage.createFromSvg('CB_Check', check_svg);
check.x = 43; check.y = 103;
board.appendChild(check);

// Label
var lbl = storage.createText('CB_Label', 'Enable notifications', 14, 400, '#FFFFFF', 0.9, 'left');
lbl.x = 68; lbl.y = 102;
board.appendChild(lbl);
```

### RadioButton

```xml
<RadioButton Content="Option A" GroupName="Options" IsChecked="True" />
```

```javascript
// Radio outer circle
var rb_outer = storage.createEllipse('RB_Outer', 40, 140, 18, 18, null, 0);
rb_outer.strokes = [{ strokeColor: '#0078D4', strokeOpacity: 1, strokeWidth: 2 }];
board.appendChild(rb_outer);

// Radio inner dot (selected)
var rb_inner = storage.createEllipse('RB_Inner', 45, 145, 8, 8, '#0078D4', 1);
board.appendChild(rb_inner);

// Label
var lbl = storage.createText('RB_Label', 'Option A', 14, 400, '#FFFFFF', 0.9, 'left');
lbl.x = 68; lbl.y = 142;
board.appendChild(lbl);
```

### TextBox

```xml
<TextBox Watermark="Enter your name" Width="300" Height="36" />
```

```javascript
// Input field background
var input_bg = storage.createRect('TextBox_bg', 50, 100, 300, 36, '#FFFFFF', 0.08, 4);
board.appendChild(input_bg);

// Placeholder text
var placeholder = storage.createText('TB_Placeholder', 'Enter your name', 12, 400, '#FFFFFF', 0.3, 'left');
placeholder.x = 58; placeholder.y = 111;
board.appendChild(placeholder);
```

### PasswordBox

```xml
<PasswordBox PasswordChar="•" Width="300" Height="36" Watermark="Password" />
```

```javascript
// Same as TextBox but with placeholder "Password" and dots "••••••••" as content
var pw_bg = storage.createRect('PW_bg', 50, 100, 300, 36, '#FFFFFF', 0.08, 4);
board.appendChild(pw_bg);

// Show masked content (dots)
var pw_text = storage.createText('PW_Content', '••••••••••', 14, 400, '#FFFFFF', 0.6, 'left');
pw_text.x = 58; pw_text.y = 111;
board.appendChild(pw_text);
```

### ComboBox

```xml
<ComboBox Width="240" Height="36" SelectedIndex="0">
  <ComboBoxItem Content="Option 1" />
  <ComboBoxItem Content="Option 2" />
  <ComboBoxItem Content="Option 3" />
</ComboBox>
```

```javascript
// Dropdown field
var combo_bg = storage.createRect('Combo_bg', 50, 100, 240, 36, '#FFFFFF', 0.08, 4);
board.appendChild(combo_bg);

// Selected text
var selected = storage.createText('Combo_text', 'Option 1', 14, 400, '#FFFFFF', 0.9, 'left');
selected.x = 60; selected.y = 111;
board.appendChild(selected);

// Dropdown arrow (▼)
var arrow = storage.createFromSvg('Arrow', '<svg viewBox="0 0 24 24"><path d="M7 10l5 5 5-5z" fill="#FFFFFF" opacity="0.5"/></svg>');
arrow.x = 264; arrow.y = 106;
board.appendChild(arrow);
```

### Slider

```xml
<Slider Minimum="0" Maximum="100" Value="65" Width="200" />
```

```javascript
// Track background
var track_bg = storage.createRect('Slider_bg', 50, 148, 200, 4, '#FFFFFF', 0.15, 2);
board.appendChild(track_bg);

// Filled track (65% = 130px)
var track_fill = storage.createRect('Slider_fill', 50, 148, 130, 4, '#0078D4', 1, 2);
board.appendChild(track_fill);

// Thumb (circle at 65%)
var thumb = storage.createEllipse('Slider_thumb', 172, 140, 20, 20, '#FFFFFF', 1);
board.appendChild(thumb);
```

### ProgressBar

```xml
<ProgressBar Value="65" Maximum="100" Width="300" Height="20" />
```

```javascript
// Track background
var prog_bg = storage.createRect('Prog_bg', 50, 100, 300, 6, '#FFFFFF', 0.15, 3);
board.appendChild(prog_bg);

// Filled portion (65%)
var prog_fill = storage.createRect('Prog_fill', 50, 100, 195, 6, '#0078D4', 1, 3);
board.appendChild(prog_fill);

// Percentage label (optional)
var pct = storage.createText('Prog_pct', '65%', 12, 400, '#FFFFFF', 0.5, 'right');
pct.x = 310; pct.y = 94;
board.appendChild(pct);
```

---

## COLLECTION ELEMENTS

### ListBox (with intelligence — detects playlist, files, settings)

```xml
<ListBox ItemsSource="{Binding RecentFiles}">
  <ListBox.ItemTemplate>
    <DataTemplate>
      <StackPanel Orientation="Vertical">
        <TextBlock Text="{Binding FileName}" FontWeight="Medium" />
        <TextBlock Text="{Binding Date}" Foreground="{StaticResource Gray400}" FontSize="11" />
      </StackPanel>
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

**Intelligence: The converter detects "files" context → generates 5 document names with dates.**

```javascript
// Dummy file items
var files = [
  { name: 'project_proposal_v3.pdf', date: '2026-01-15', size: '2.4 MB' },
  { name: 'meeting_notes_july.docx',  date: '2026-02-03', size: '156 KB' },
  { name: 'budget_2026.xlsx',         date: '2026-03-22', size: '890 KB' },
  { name: 'design_system.fig',        date: '2026-04-10', size: '4.1 MB' },
  { name: 'sprint_retro.md',          date: '2026-05-18', size: '32 KB' },
];

for (var i = 0; i < files.length; i++) {
  var itemY = 300 + i * 56;
  // Item background
  var bg = storage.createRect('FileItem_' + i, 12, itemY, 400, 50, '#FFFFFF', 0.04, 8);
  board.appendChild(bg);
  // File icon
  var icon = storage.createFromSvg('FileIcon_' + i, fileIconSvg);
  icon.x = 24; icon.y = itemY + 13;
  board.appendChild(icon);
  // File name
  var name = storage.createText('FileName_' + i, files[i].name, 13, 600, '#FFFFFF', 0.9, 'left');
  name.x = 56; name.y = itemY + 8;
  board.appendChild(name);
  // Date
  var date = storage.createText('FileDate_' + i, files[i].date, 11, 400, '#FFFFFF', 0.4, 'left');
  date.x = 56; date.y = itemY + 28;
  board.appendChild(date);
  // Size
  var size = storage.createText('FileSize_' + i, files[i].size, 11, 400, '#FFFFFF', 0.4, 'right');
  size.x = 370; size.y = itemY + 28;
  board.appendChild(size);
}
```

### Playlist ListBox (intelligence variant)

```javascript
// Dummy playlist items with album art, track name, artist, duration
var tracks = [
  { title: 'Summer Vibes 2024',    artist: 'DJ Cool Breeze',   duration: '3:42' },
  { title: 'Late Night Jazz',      artist: 'The Midnight Trio', duration: '5:18' },
  { title: 'Workout Mix Vol. 3',   artist: 'Beat Factory',     duration: '2:55' },
  { title: 'Chill Lo-Fi Beats',    artist: 'Sleepy Cat',       duration: '4:10' },
  { title: 'Top Hits Collection',  artist: 'Various Artists',  duration: '3:28' },
];

for (var i = 0; i < tracks.length; i++) {
  var itemY = 250 + i * 60;
  // Row bg
  var bg = storage.createRect('Track_' + i, 12, itemY, 420, 54, '#FFFFFF', i === 0 ? 0.08 : 0.03, 8);
  board.appendChild(bg);
  // Album art (colored square placeholder)
  var art = storage.createRect('Art_' + i, 24, itemY + 7, 40, 40, pastelColors[i], 0.7, 6);
  board.appendChild(art);
  // Track number (in art)
  var num = storage.createText('Num_' + i, String(i + 1), 16, 700, '#FFFFFF', 0.8, 'center');
  num.x = 24; num.y = itemY + 18; num.w = 40;
  board.appendChild(num);
  // Track title
  var title = storage.createText('Title_' + i, tracks[i].title, 14, 600, '#FFFFFF', 0.9, 'left');
  title.x = 76; title.y = itemY + 8;
  board.appendChild(title);
  // Artist
  var artist = storage.createText('Artist_' + i, tracks[i].artist, 12, 400, '#FFFFFF', 0.5, 'left');
  artist.x = 76; artist.y = itemY + 28;
  board.appendChild(artist);
  // Duration
  var dur = storage.createText('Dur_' + i, tracks[i].duration, 12, 400, '#FFFFFF', 0.4, 'right');
  dur.x = 390; dur.y = itemY + 18;
  board.appendChild(dur);
}
```

### Subtitle Selector (intelligence variant)

```javascript
// Dummy subtitle languages with checkmark on first item
var subtitles = [
  { lang: 'English [CC]', checked: true },
  { lang: 'Spanish',      checked: false },
  { lang: 'French',       checked: false },
  { lang: 'German',       checked: false },
  { lang: 'Japanese',     checked: false },
];

for (var i = 0; i < subtitles.length; i++) {
  var itemY = 520 + i * 36;
  var bg = storage.createRect('Sub_' + i, 40, itemY, 300, 32, '#FFFFFF', 0.04);
  board.appendChild(bg);

  if (subtitles[i].checked) {
    // Checkmark
    var chk = storage.createFromSvg('Chk_' + i,
      '<svg viewBox="0 0 24 24"><path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z" fill="#0078D4"/></svg>');
    chk.x = 48; chk.y = itemY + 6;
    board.appendChild(chk);
  }

  var lang = storage.createText('Lang_' + i, subtitles[i].lang, 13,
    subtitles[i].checked ? 600 : 400,
    '#FFFFFF',
    subtitles[i].checked ? 0.9 : 0.6, 'left');
  lang.x = subtitles[i].checked ? 78 : 52;
  lang.y = itemY + 8;
  board.appendChild(lang);
}
```

### Volume Slider (intelligence variant — standalone control)

```javascript
// Volume icon (speaker with waves)
var vol_icon = storage.createFromSvg('VolIcon',
  '<svg viewBox="0 0 24 24"><path d="M3 9v6h4l5 5V4L7 9H3z" fill="#FFFFFF" opacity="0.5"/>' +
  '<path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z" fill="#FFFFFF" opacity="0.3"/></svg>');
vol_icon.x = 40; vol_icon.y = 456;
board.appendChild(vol_icon);

// Slider track (160px)
var track = storage.createRect('VolTrack', 70, 468, 160, 4, '#FFFFFF', 0.15, 2);
board.appendChild(track);

// Filled portion (65% = 104px)
var fill = storage.createRect('VolFill', 70, 468, 104, 4, '#0078D4', 1, 2);
board.appendChild(fill);

// Thumb
var thumb = storage.createEllipse('VolThumb', 166, 460, 20, 20, '#FFFFFF', 1);
board.appendChild(thumb);

// Percentage
var pct = storage.createText('VolPct', '65%', 12, 400, '#FFFFFF', 0.5, 'left');
pct.x = 236; pct.y = 464;
board.appendChild(pct);
```

### DataGrid / Table (intelligence variant)

```javascript
// Headers
var headers = ['Name', 'Status', 'Date', 'Size'];
var colWidths = [200, 90, 100, 70];
var hx = 20;
for (var h = 0; h < headers.length; h++) {
  var th = storage.createText('TH_' + h, headers[h], 11, 700, '#FFFFFF', 0.5, 'left');
  th.x = hx; th.y = 180;
  board.appendChild(th);
  hx += colWidths[h];
}

// Data rows
var rows = [
  ['video_001.mp4',     'Ready',      '2026-01-15', '2.4 GB'],
  ['audio_podcast.wav', 'Processing', '2026-02-03', '156 MB'],
  ['image_batch.zip',   'Done',       '2026-03-22', '890 MB'],
];

for (var r = 0; r < rows.length; r++) {
  var ry = 204 + r * 26;
  // Zebra striping
  var bg = storage.createRect('RowBg_' + r, 20, ry, 460, 24, '#FFFFFF', r % 2 === 0 ? 0.03 : 0);
  board.appendChild(bg);

  var rx = 20;
  for (var c = 0; c < rows[r].length; c++) {
    var td = storage.createText('TD_' + r + '_' + c, rows[r][c], 12, 400, '#FFFFFF', 0.7, 'left');
    td.x = rx; td.y = ry + 4;
    board.appendChild(td);
    rx += colWidths[c];
  }
}

// Separator line under headers
var sep = storage.createLine('TableSep', 20, 202, 460, '#FFFFFF', 0.1, 1);
board.appendChild(sep);
```

---

## NAVIGATION

### TabControl

```xml
<TabControl>
  <TabItem Header="General">
    <StackPanel>...</StackPanel>
  </TabItem>
  <TabItem Header="Display">
    <StackPanel>...</StackPanel>
  </TabItem>
  <TabItem Header="Audio" IsSelected="True">
    <StackPanel>...</StackPanel>
  </TabItem>
</TabControl>
```

```javascript
// Tab bar background
var tab_bar = storage.createRect('TabBar', 0, 0, 1280, 44, '#FFFFFF', 0.03);
board.appendChild(tab_bar);

// Tabs
var tabs = ['General', 'Display', 'Audio'];
var activeIndex = 2; // "Audio" is selected
var tabW = 100;
for (var i = 0; i < tabs.length; i++) {
  var tx = i * tabW;
  var isActive = i === activeIndex;

  if (isActive) {
    // Active tab bg
    var bg = storage.createRect('TabBg_' + i, tx, 0, tabW, 44, '#FFFFFF', 0.06);
    board.appendChild(bg);
    // Underline
    var line = storage.createRect('TabLine_' + i, tx + 10, 40, tabW - 20, 3, '#0078D4', 1);
    board.appendChild(line);
  }

  var label = storage.createText('Tab_' + i, tabs[i], 13,
    isActive ? 600 : 400,
    '#FFFFFF',
    isActive ? 0.9 : 0.5,
    'center');
  label.x = tx; label.y = 12; label.w = tabW;
  board.appendChild(label);
}
```

### Menu / MenuItem

```xml
<Menu>
  <MenuItem Header="File">
    <MenuItem Header="New" />
    <MenuItem Header="Open" />
    <MenuItem Header="Save" />
    <Separator />
    <MenuItem Header="Exit" />
  </MenuItem>
  <MenuItem Header="Edit" />
  <MenuItem Header="View" />
</Menu>
```

```javascript
// Menu bar
var menuBar = storage.createRect('MenuBar', 0, 0, 1280, 32, '#FFFFFF', 0.03);
board.appendChild(menuBar);

// Menu items
var items = ['File', 'Edit', 'View', 'Help'];
for (var i = 0; i < items.length; i++) {
  var mi = storage.createText('Menu_' + i, items[i], 13, 400, '#FFFFFF', 0.8, 'left');
  mi.x = 8 + i * 60; mi.y = 6;
  board.appendChild(mi);
}
```

---

## GRADIENTS

### LinearGradientBrush background

```xml
<!-- In Colors.axaml -->
<LinearGradientBrush x:Key="HeaderGradient" StartPoint="0%,0%" EndPoint="100%,0%">
  <GradientStop Offset="0" Color="#FF6B2C" />
  <GradientStop Offset="1" Color="#8B2FC9" />
</LinearGradientBrush>

<!-- In component -->
<Border Background="{StaticResource HeaderGradient}" Height="200" />
```

```javascript
// Gradient border background
var bg = storage.createGradientRect('Header', 0, 0, 1280, 200, 'linear',
  [
    { offset: 0, color: '#FF6B2C', opacity: 1 },
    { offset: 1, color: '#8B2FC9', opacity: 1 }
  ],
  { startX: 0, startY: 0, endX: 1, endY: 0 }
);
board.appendChild(bg);
```

### RadialGradientBrush

```xml
<RadialGradientBrush x:Key="Spotlight" Center="50%,50%" GradientOrigin="50%,50%">
  <GradientStop Offset="0" Color="#40FFFFFF" />
  <GradientStop Offset="1" Color="#00FFFFFF" />
</RadialGradientBrush>
```

```javascript
var spotlight = storage.createGradientRect('Spot', 0, 0, 400, 400, 'radial',
  [
    { offset: 0, color: '#FFFFFF', opacity: 0.25 },
    { offset: 1, color: '#FFFFFF', opacity: 0 }
  ],
  { centerX: 0.5, centerY: 0.5 }
);
board.appendChild(spotlight);
```

### Multi-stop gradient

```xml
<LinearGradientBrush x:Key="Rainbow" StartPoint="0%,0%" EndPoint="100%,100%">
  <GradientStop Offset="0" Color="#FF0000" />
  <GradientStop Offset="0.33" Color="#00FF00" />
  <GradientStop Offset="0.66" Color="#0000FF" />
  <GradientStop Offset="1" Color="#FF00FF" />
</LinearGradientBrush>
```

```javascript
var rainbow = storage.createGradientRect('Rainbow', 0, 0, 400, 400, 'linear',
  [
    { offset: 0,    color: '#FF0000', opacity: 1 },
    { offset: 0.33, color: '#00FF00', opacity: 1 },
    { offset: 0.66, color: '#0000FF', opacity: 1 },
    { offset: 1,    color: '#FF00FF', opacity: 1 }
  ],
  { startX: 0, startY: 0, endX: 1, endY: 1 }
);
board.appendChild(rainbow);
```

---

## SPECIAL PATTERNS

### Search Box

```javascript
// Search icon
var searchIcon = storage.createFromSvg('Search',
  '<svg viewBox="0 0 24 24"><path d="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z" fill="#FFFFFF" opacity="0.3"/></svg>');
searchIcon.x = 60; searchIcon.y = 54;
board.appendChild(searchIcon);

// Input background
var input = storage.createRect('SearchInput', 90, 44, 260, 36, '#FFFFFF', 0.08, 18);
board.appendChild(input);

// Placeholder text
var placeholder = storage.createText('SearchPH', 'Search files...', 13, 400, '#FFFFFF', 0.3, 'left');
placeholder.x = 106; placeholder.y = 53;
board.appendChild(placeholder);
```

### Empty State

```javascript
// Empty state = icon + message + action button
var emptyIcon = storage.createFromSvg('EmptyIcon',
  '<svg viewBox="0 0 64 64"><path d="M32 12C20.96 12 12 20.96 12 32s8.96 20 20 20 20-8.96 20-20S43.04 12 32 12zm0 36c-8.82 0-16-7.18-16-16s7.18-16 16-16 16 7.18 16 16-7.18 16-16 16zm-2-22h4v12h-4zm0-8h4v4h-4z" fill="#FFFFFF" opacity="0.15"/></svg>');
emptyIcon.x = 600; emptyIcon.y = 280;
board.appendChild(emptyIcon);

var title = storage.createText('EmptyTitle', 'No items yet', 18, 600, '#FFFFFF', 0.5, 'center');
title.x = 440; title.y = 360; title.w = 400;
board.appendChild(title);

var desc = storage.createText('EmptyDesc', 'Add your first item to get started', 13, 400, '#FFFFFF', 0.3, 'center');
desc.x = 440; desc.y = 388; desc.w = 400;
board.appendChild(desc);

var btn = storage.createRect('EmptyBtn', 560, 420, 160, 36, '#0078D4', 1, 18);
board.appendChild(btn);
var btnText = storage.createText('EmptyBtnTxt', 'Add New', 14, 600, '#FFFFFF', 1, 'center');
btnText.x = 560; btnText.y = 429; btnText.w = 160;
board.appendChild(btnText);
```

### Header Bar

```javascript
// Full-width header bar with title + action buttons
var header = storage.createRect('HeaderBar', 0, 0, 1280, 56, '#FFFFFF', 0.04);
board.appendChild(header);

// Back button
var back = storage.createFromSvg('BackBtn',
  '<svg viewBox="0 0 24 24"><path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z" fill="#FFFFFF" opacity="0.7"/></svg>');
back.x = 16; back.y = 16;
board.appendChild(back);

// Title
var title = storage.createText('HdrTitle', 'Settings', 20, 700, '#FFFFFF', 0.9, 'left');
title.x = 52; title.y = 14;
board.appendChild(title);

// Action button (top right)
var action = storage.createText('HdrAction', 'Done', 14, 600, '#0078D4', 1, 'right');
action.x = 1200; action.y = 18; action.w = 64;
board.appendChild(action);
```

### Card Layout

```javascript
// Card: rounded rect with shadow-like border, title, description, action
var card = storage.createRect('Card', 40, 200, 300, 180, '#FFFFFF', 0.05, 12);
card.strokes = [{ strokeColor: '#FFFFFF', strokeOpacity: 0.06, strokeWidth: 1 }];
board.appendChild(card);

var cardImg = storage.createRect('CardImg', 40, 200, 300, 100, '#0078D4', 0.3, [12, 12, 0, 0]);
board.appendChild(cardImg);

var cardTitle = storage.createText('CardTitle', 'Feature Name', 16, 600, '#FFFFFF', 0.9, 'left');
cardTitle.x = 56; cardTitle.y = 312;
board.appendChild(cardTitle);

var cardDesc = storage.createText('CardDesc', 'Short description of the feature', 12, 400, '#FFFFFF', 0.5, 'left');
cardDesc.x = 56; cardDesc.y = 334;
board.appendChild(cardDesc);

// Card action button
var cardBtn = storage.createText('CardAct', 'Learn more →', 12, 600, '#0078D4', 1, 'left');
cardBtn.x = 56; cardBtn.y = 358;
board.appendChild(cardBtn);
```

---

## COLOR UTILITY PATTERNS

```javascript
// 8-digit hex parsing (#AARRGGBB)
storage.setFillFromHex8(shape, '#99FFFFFF');  // White at 60% opacity
// Result: fillColor = '#FFFFFF', fillOpacity = 0.6

// Semi-transparent fills
storage.createRect('Overlay', 0, 0, 400, 300, '#000000', 0.5);  // 50% black overlay

// Common opacity values in UI:
// 0.03-0.05  → Very subtle (separator lines, zebra striping)
// 0.08-0.12  → Input backgrounds, card surfaces
// 0.15-0.20  → Track backgrounds, disabled states
// 0.40-0.60  → Medium emphasis (secondary text)
// 0.80-0.90  → High emphasis (body text, active elements)
// 1.0        → Full opacity (headings, primary actions)
```

---

> **This is a REFERENCE catalog — not actual running code.**  
> Use these patterns to understand how each AXAML element maps to Penpot JS shapes.  
> The `convert.mjs` generator produces similar code automatically.

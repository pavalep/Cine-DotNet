# Example: Converting a Screen with a List/Playlist

**Scenario:** Your AXAML has a `ListBox` or `ItemsControl` with item templates.
The converter detects this pattern and generates dummy list items.

## Input AXAML (simplified)
```xml
<StackPanel>
  <TextBlock Text="Recent Files" Classes="md3-subtitle1" />
  <ListBox ItemsSource="{Binding RecentFiles}">
    <ListBox.ItemTemplate>
      <DataTemplate>
        <StackPanel Orientation="Horizontal" Spacing="8">
          <PathIcon Data="{StaticResource FileIcon}" />
          <TextBlock Text="{Binding FileName}" />
          <TextBlock Text="{Binding Date}" Foreground="{StaticResource Gray400}" />
        </StackPanel>
      </DataTemplate>
    </ListBox.ItemTemplate>
  </ListBox>
</StackPanel>
```

## Converter Output (intelligence layer)

The converter detects `ListBox` and generates 5 dummy items:

```javascript
// Recent Files header
var s0 = storage.createText('RecentFiles', 'Recent Files', 14, 600, '#FFFFFF', 0.7, 'left');
s0.x = 40; s0.y = 300;
board.appendChild(s0);

// Dummy item 1
var s1_icon = storage.createFromSvg('FileIcon_1', '<svg>...</svg>');
s1_icon.x = 40; s1_icon.y = 330;
board.appendChild(s1_icon);

var s1_text = storage.createText('File_1', 'project_proposal_v3.pdf', 12, 400, '#FFFFFF', 0.9, 'left');
s1_text.x = 56; s1_text.y = 332;
board.appendChild(s1_text);

// ... 4 more items with varied names:
// "meeting_notes_july.docx", "budget_2026.xlsx",
// "design_system.fig", "sprint_retro.md"
```

## Prompt to Achieve This

```
Convert [ScreenName] to Penpot. The screen has a ListBox.
Add 5 dummy list items with realistic names from these categories:
- If the list context is "files" → use project document names
- If "playlist" → use song names + artist names
- If "settings" → use common setting names
- If unknown → use generic item names
```

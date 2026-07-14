using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Simba.Avalonia.Views.Dialogs;

/// <summary>Entry shown in the command palette.</summary>
public sealed record PaletteCommandEntry(string Description, Action Execute);

/// <summary>
/// A searchable command palette (Ctrl+K) modelled after VS Code / Sublime Text.
/// Shows all registered keyboard shortcuts filtered by typed text.
/// </summary>
public partial class CommandPaletteDialog : Window
{
    private readonly List<PaletteCommandEntry> _allCommands;

    public CommandPaletteDialog(List<(string description, Action action)> commands)
    {
        InitializeComponent();
        _allCommands = commands.Select(c => new PaletteCommandEntry(c.description, c.action)).ToList();
        ResultsCountText.Text = $"{_allCommands.Count} commands";
        CommandsList.ItemsSource = _allCommands;
        SearchBox.Focus();

        // Auto-size to content but cap height
        MinHeight = Math.Min(400, 48 + _allCommands.Count * 36 + 60);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(filter))
        {
            CommandsList.ItemsSource = _allCommands;
            ResultsCountText.Text = $"{_allCommands.Count} commands";
            return;
        }

        var filtered = _allCommands
            .Where(c => c.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        CommandsList.ItemsSource = filtered;
        ResultsCountText.Text = $"{filtered.Count} / {_allCommands.Count} commands";
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        // Enter on filtered list executes first result
        if (e.Key == Key.Enter && CommandsList.ItemsSource is IEnumerable<PaletteCommandEntry> items)
        {
            var first = items.FirstOrDefault();
            if (first != null)
            {
                ExecuteCommand(first);
                e.Handled = true;
            }
        }
    }

    private void OnCommandItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is PaletteCommandEntry entry)
            ExecuteCommand(entry);
    }

    private void ExecuteCommand(PaletteCommandEntry entry)
    {
        Close();
        entry.Execute();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        SearchBox?.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

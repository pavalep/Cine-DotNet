using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Input;

namespace Simba.Avalonia.Services;

/// <summary>
/// Validates the keyboard shortcut registry for conflicts at startup.
/// Detects duplicate bindings (same key + modifiers) in the same scope
/// and logs warnings so they can be resolved before causing user confusion.
///
/// Phase 3: Run once after all shortcuts are registered.
/// </summary>
public static class KeyboardConflictValidator
{
    /// <summary>
    /// Result of a validation run.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>Total bindings checked.</summary>
        public int TotalBindings { get; internal set; }

        /// <summary>Number of conflicts found (duplicate key+modifier in same scope).</summary>
        public int ConflictCount { get; internal set; }

        /// <summary>Detailed conflict entries.</summary>
        public List<ConflictEntry> Conflicts { get; } = new();

        /// <summary>True if no conflicts were found.</summary>
        public bool IsClean => ConflictCount == 0;
    }

    /// <summary>A single conflict between two bindings.</summary>
    public sealed class ConflictEntry
    {
        /// <summary>The conflicting key.</summary>
        public Key Key { get; internal set; }

        /// <summary>The conflicting modifiers.</summary>
        public KeyModifiers Modifiers { get; internal set; }

        /// <summary>The scope where the conflict exists.</summary>
        public InputRoutingService.InputScope Scope { get; internal set; }

        /// <summary>The human-readable gesture text.</summary>
        public string Gesture { get; internal set; } = "";

        /// <summary>The first registration's description.</summary>
        public string FirstDescription { get; internal set; } = "";

        /// <summary>The second registration's description.</summary>
        public string SecondDescription { get; internal set; } = "";

        public override string ToString() =>
            $"Conflict: {Gesture} in {Scope}: \"{FirstDescription}\" vs \"{SecondDescription}\"";
    }

    /// <summary>
    /// Run validation against all registered bindings.
    /// Returns a <see cref="ValidationResult"/> with details of any conflicts.
    /// </summary>
    public static ValidationResult Validate(IReadOnlyList<RegisteredShortcut> bindings)
    {
        var result = new ValidationResult
        {
            TotalBindings = bindings.Count
        };

        // Group by (Key, Modifiers, Scope) — true duplicates
        var groups = bindings
            .GroupBy(b => (b.Key, b.Modifiers, b.Scope))
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var items = group.ToList();
            result.Conflicts.Add(new ConflictEntry
            {
                Key = group.Key.Key,
                Modifiers = group.Key.Modifiers,
                Scope = group.Key.Scope,
                Gesture = items[0].GestureText,
                FirstDescription = items[0].Description,
                SecondDescription = items[1].Description
            });
        }

        result.ConflictCount = result.Conflicts.Count;

        // Log warnings for each conflict
        foreach (var conflict in result.Conflicts)
        {
            Debug.WriteLine($"[KeyboardConflictValidator] {conflict}");
            CrashReporter.LogError(
                $"Keyboard shortcut conflict: {conflict.Gesture} in {conflict.Scope} — " +
                $"\"{conflict.FirstDescription}\" vs \"{conflict.SecondDescription}\"");
        }

        if (result.IsClean)
        {
            Debug.WriteLine($"[KeyboardConflictValidator] No conflicts found in {bindings.Count} bindings.");
        }

        return result;
    }

    /// <summary>
    /// Convenience: validates bindings from an InputRoutingService.
    /// </summary>
    public static ValidationResult Validate(InputRoutingService service)
    {
        return Validate(service.GetAllBindings());
    }
}

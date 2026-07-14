using System;

namespace Simba.Avalonia.Services;

/// <summary>
/// Abstraction over Windows Registry access for file association registration.
/// Enables unit testing of <see cref="FileAssociationService"/> without touching the real registry.
/// </summary>
public interface IRegistryService
{
    /// <summary>Set a registry value (creates key path if needed).</summary>
    void SetValue(string keyPath, string valueName, object? value);

    /// <summary>Set a binary registry value (REG_NONE).</summary>
    void SetBinaryValue(string keyPath, string valueName, byte[] data);

    /// <summary>Get a registry value, or null if the key/value doesn't exist.</summary>
    object? GetValue(string keyPath, string valueName);

    /// <summary>Delete a named value from a registry key. No-op if key doesn't exist.</summary>
    void DeleteValue(string keyPath, string valueName);

    /// <summary>Delete a registry key and all its subkeys. No-op if key doesn't exist.</summary>
    void DeleteKey(string keyPath);

    /// <summary>Check if a registry key exists.</summary>
    bool KeyExists(string keyPath);
}

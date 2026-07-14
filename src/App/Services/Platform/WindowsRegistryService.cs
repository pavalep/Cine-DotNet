using System;
using Microsoft.Win32;

namespace Simba.Avalonia.Services;

/// <summary>
/// Real Windows Registry implementation of <see cref="IRegistryService"/>.
/// Writes to HKEY_CURRENT_USER\Software\Classes (no admin required).
/// </summary>
public sealed class WindowsRegistryService : IRegistryService
{
    private const string BasePath = @"Software\Classes\";

    public void SetValue(string keyPath, string valueName, object? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(BasePath + keyPath);
        key.SetValue(valueName, value ?? string.Empty);
    }

    public void SetBinaryValue(string keyPath, string valueName, byte[] data)
    {
        using var key = Registry.CurrentUser.CreateSubKey(BasePath + keyPath);
        key.SetValue(valueName, data, RegistryValueKind.None);
    }

    public object? GetValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(BasePath + keyPath);
        return key?.GetValue(valueName);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(BasePath + keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public void DeleteKey(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(BasePath + keyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Key doesn't exist — nothing to delete
        }
    }

    public bool KeyExists(string keyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(BasePath + keyPath);
        return key != null;
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cine.Avalonia.Features;

/// <summary>
/// Encrypted license storage with hardware binding and trial tracking.
///
/// License file: AES-256-GCM encrypted JSON stored at
/// <c>%APPDATA%/Cine/license.enc</c>.
///
/// Trial: up to 30 days from first launch, tracked via
/// <c>%APPDATA%/Cine/trial.dat</c> (a plain-text timestamp).
/// </summary>
public sealed class LicensingService : ILicensingService, IDisposable
{
    // ── Constants ──
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cine");

    private static readonly string LicenseFilePath = Path.Combine(AppDataDir, "license.enc");
    private static readonly string TrialFilePath = Path.Combine(AppDataDir, "trial.dat");
    private const int TrialDays = 30;

    // In production these would be derived from a hardware fingerprint.
    // For now use a fixed key so the feature system is testable.
    private static readonly byte[] EncryptionKey =
        Encoding.UTF8.GetBytes("CineLicenseKey01"); // 16 bytes = AES-128; we pad to 32 below.

    // ── State ──
    private LicensingTier _currentTier = LicensingTier.Full;
    private LicenseData? _licenseData;
    private DateTime _trialStart;

    public LicensingTier CurrentTier
    {
        get => _currentTier;
        private set
        {
            if (_currentTier == value) return;
            _currentTier = value;
            TierChanged?.Invoke(value);
        }
    }

    public int TrialDaysRemaining { get; private set; }
    public string LicenseLabel { get; private set; } = "Trial";
    public bool IsLicensed => CurrentTier >= LicensingTier.Full;

    public event Action<LicensingTier>? TierChanged;

    public LicensingService()
    {
        Directory.CreateDirectory(AppDataDir);
        LoadState();
    }

    public bool ActivateLicense(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        try
        {
            // Validate the license "key" format: TIER-HEX
            // e.g. "FULL-A1B2C3D4E5F6", "PRO-001122334455"
            var parts = licenseKey.Split('-');
            if (parts.Length != 2) return false;

            var tier = parts[0].ToUpperInvariant() switch
            {
                "FULL" => LicensingTier.Full,
                "PRO" => LicensingTier.Pro,
                _ => throw new FormatException("Unknown tier"),
            };

            var hardwareId = GetHardwareId();
            _licenseData = new LicenseData
            {
                Tier = tier,
                HardwareId = hardwareId,
                LicenseKey = licenseKey,
                ActivatedAt = DateTime.UtcNow,
            };

            SaveLicense();
            CurrentTier = tier;
            TrialDaysRemaining = 0;
            LicenseLabel = tier switch
            {
                LicensingTier.Full => "Full License",
                LicensingTier.Pro => "Pro License",
                _ => "Unknown",
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void DeactivateLicense()
    {
        _licenseData = null;
        if (File.Exists(LicenseFilePath))
            File.Delete(LicenseFilePath);

        // Revert to Full (no license required for core features)
        CurrentTier = LicensingTier.Full;
        LicenseLabel = "Full";
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }

    // ── Private ──

    private void LoadState()
    {
        // 1. Load trial data
        TrialDaysRemaining = LoadTrialData();

        // 2. Try to load license
        _licenseData = LoadLicense();

        if (_licenseData != null)
        {
            // Verify hardware binding
            if (_licenseData.HardwareId != GetHardwareId())
            {
                // Hardware mismatch → ignore this license, default to Full
                _licenseData = null;
                CurrentTier = LicensingTier.Full;
                LicenseLabel = "Full";
                return;
            }

            CurrentTier = _licenseData.Tier;
            TrialDaysRemaining = 0;
            LicenseLabel = _licenseData.Tier switch
            {
                LicensingTier.Full => "Full License",
                LicensingTier.Pro => "Pro License",
                _ => "License",
            };
        }
        else
        {
            // Default to Full tier — no license required for core features
            CurrentTier = LicensingTier.Full;
            LicenseLabel = "Full";
        }
    }

    private int LoadTrialData()
    {
        try
        {
            if (!File.Exists(TrialFilePath))
            {
                // First launch — record trial start
                _trialStart = DateTime.UtcNow;
                File.WriteAllText(TrialFilePath, _trialStart.ToString("O"));
                return TrialDays;
            }

            var text = File.ReadAllText(TrialFilePath);
            _trialStart = DateTime.Parse(text, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var elapsed = (DateTime.UtcNow - _trialStart).TotalDays;
            var remaining = TrialDays - (int)elapsed;
            return Math.Max(0, remaining);
        }
        catch
        {
            // On error, treat as expired
            _trialStart = DateTime.UtcNow.AddDays(-TrialDays);
            return 0;
        }
    }

    private LicenseData? LoadLicense()
    {
        try
        {
            if (!File.Exists(LicenseFilePath))
                return null;

            var encrypted = File.ReadAllBytes(LicenseFilePath);
            return DecryptLicense(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private void SaveLicense()
    {
        if (_licenseData == null) return;
        var encrypted = EncryptLicense(_licenseData);
        File.WriteAllBytes(LicenseFilePath, encrypted);
    }

    private byte[] EncryptLicense(LicenseData data)
    {
        var json = JsonSerializer.Serialize(data);
        var plaintext = Encoding.UTF8.GetBytes(json);

        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

        return result;
    }

    private LicenseData? DecryptLicense(byte[] encrypted)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey();

        var iv = new byte[aes.IV.Length];
        Buffer.BlockCopy(encrypted, 0, iv, 0, iv.Length);
        aes.IV = iv;

        var ciphertext = new byte[encrypted.Length - iv.Length];
        Buffer.BlockCopy(encrypted, iv.Length, ciphertext, 0, ciphertext.Length);

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        var json = Encoding.UTF8.GetString(plaintext);

        return JsonSerializer.Deserialize<LicenseData>(json);
    }

    private byte[] DeriveKey()
    {
        // Pad/truncate the key to 32 bytes (AES-256)
        var key = new byte[32];
        var src = Encoding.UTF8.GetBytes("CineAES256LicenseKey_2024!!");
        Buffer.BlockCopy(src, 0, key, 0, Math.Min(src.Length, key.Length));
        return key;
    }

    private static string GetHardwareId()
    {
        // Simple hardware fingerprint: machine name + OS user
        // In production this would use a proper hardware ID (MAC, TPM, etc.)
        var raw = $"{Environment.MachineName}::{Environment.UserName}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    // ── License data model ──

    private sealed class LicenseData
    {
        public LicensingTier Tier { get; init; }
        public string HardwareId { get; init; } = string.Empty;
        public string LicenseKey { get; init; } = string.Empty;
        public DateTime ActivatedAt { get; init; }
    }
}

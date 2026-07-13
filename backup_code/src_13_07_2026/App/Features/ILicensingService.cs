using System;

namespace Cine.Avalonia.Features;

/// <summary>
/// Validates license state and provides the current <see cref="LicensingTier"/>.
/// </summary>
public interface ILicensingService
{
    /// <summary>The resolved license tier for the current session.</summary>
    LicensingTier CurrentTier { get; }

    /// <summary>Days remaining in the trial period (0 if not in trial).</summary>
    int TrialDaysRemaining { get; }

    /// <summary>A human-readable label: "Trial (14 days left)" or "Full License" etc.</summary>
    string LicenseLabel { get; }

    /// <summary>Fires when the tier changes (e.g. license activated or trial expired).</summary>
    event Action<LicensingTier>? TierChanged;

    /// <summary>Try to activate a license key. Returns true if valid.</summary>
    bool ActivateLicense(string licenseKey);

    /// <summary>Deactivate the current license, reverting to trial/free.</summary>
    void DeactivateLicense();

    /// <summary>True if a valid paid license (Full or Pro) is active.</summary>
    bool IsLicensed { get; }
}

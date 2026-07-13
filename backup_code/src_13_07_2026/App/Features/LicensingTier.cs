namespace Cine.Avalonia.Features;

/// <summary>
/// License tiers in ascending order of capability.
/// </summary>
public enum LicensingTier
{
    /// <summary>Evaluation period — full features, time-limited.</summary>
    Trial = 0,

    /// <summary>Free tier — limited features, no time limit.</summary>
    Free = 1,

    /// <summary>Paid tier — most features, no watermark.</summary>
    Full = 2,

    /// <summary>Professional tier — all features + early access.</summary>
    Pro = 3,
}

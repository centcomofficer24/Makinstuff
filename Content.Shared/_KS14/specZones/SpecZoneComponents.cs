using System.ComponentModel.DataAnnotations;

namespace Content.Shared.KS14.SpecZones.Systems;

/// <summary>
/// Marker component for spec zone spawns
/// </summary>
[RegisterComponent]
public sealed partial class SpecialZoneSpawnComponent : Component
{
    /// <summary>
    /// ID of the zone that this spawn is for.
    /// </summary>
    [DataField(required: true)]
    public string ZoneId;
}


/// <summary>
/// Marker component for spec zone maps
/// </summary>
[RegisterComponent]
public sealed partial class SpecialZoneMapComponent : Component
{
    /// <summary>
    /// ID of the zone.
    /// </summary>
    [DataField]
    public string ZoneId;
}

/// <summary>
/// Ends a SpecialZone when triggered
/// </summary>
[RegisterComponent]
public sealed partial class EndSpecialZoneOnTriggerComponent : Component
{
    /// <summary>
    /// ID of the zone to end.
    /// </summary>
    [DataField]
    public string? ZoneId = null;
}

/// <summary>
/// For keys that lead to Special Zones.
/// </summary>
[RegisterComponent]
public sealed partial class SpecZoneKeyComponent : Component
{
    /// <summary>
    /// ID of the zone that this key belongs to.
    /// Set to null to be random.
    /// </summary>
    [DataField]
    public string? ZoneId = null;
}


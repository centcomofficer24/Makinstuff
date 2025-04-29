using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Shared.KS14.SpecZones.Systems;

/// <summary>
/// Prototype for a special zone.
/// </summary>
[Prototype("specialZone")]
public sealed partial class SpecialZone : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Biome that this zone spawns with.
    /// </summary>
    [DataField("biome", customTypeSerializer: typeof(PrototypeIdSerializer<BiomeTemplatePrototype>))]
    public string? ZoneBiome { get; private set; } = null;

    /// <summary>
    /// Path to the map for this zone
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath { get; private set; } = default!;
}

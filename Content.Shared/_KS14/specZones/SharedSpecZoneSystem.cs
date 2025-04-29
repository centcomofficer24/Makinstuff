using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Popups;
using Robust.Shared.Audio;

namespace Content.Shared.KS14.SpecZones.Systems;

public abstract class SharedSpecZoneSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;


    public static TimeSpan ZoneExitEffectDuration = TimeSpan.FromSeconds(8);
    public static SoundSpecifier ZoneFinishSoundSpec = new SoundPathSpecifier("/Audio/Ambience/ambimo2.ogg");
    public static SoundSpecifier ZoneEnterSoundSpec = new SoundPathSpecifier("/Audio/Ambience/ambiodd.ogg");

    public override void Initialize()
    {
        base.Initialize();
    }

    // jank
    public bool TryGetRandomZoneSpawnMarker([NotNullWhen(true)] out Entity<SpecialZoneSpawnComponent>? marker)
    {
        var spawnEntities = EntityQueryEnumerator<SpecialZoneSpawnComponent>();
        var spawns = new List<(EntityUid, SpecialZoneSpawnComponent)>();

        while (spawnEntities.MoveNext(out var entityUid, out var spawnComponent))
            spawns.Add((entityUid, spawnComponent));

        if (spawns.Count == 0)
        {
            marker = null;
            return false;
        }

        var chosenSpawn = _random.Pick(spawns);

        marker = new Entity<SpecialZoneSpawnComponent>(chosenSpawn.Item1, chosenSpawn.Item2);
        return true;
    }
}

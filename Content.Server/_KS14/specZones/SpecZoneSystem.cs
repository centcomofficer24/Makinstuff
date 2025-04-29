using System.Linq;
using Content.Server.GameTicking.Events;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.KS14.SpecZones.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Microsoft.CodeAnalysis;
using Robust.Shared.Random;
using Content.Shared._Goobstation.Wizard.Traps;
using Robust.Shared.Map;
using Content.Server.Spawners.Components;
using Content.Shared.Mind;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Stunnable;
using Content.Server.Flash;
using System.Threading.Tasks;
using Content.Shared.Popups;
using Content.Server.IdentityManagement;
using Robust.Server.Audio;
using Content.Server.Administration.Systems;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.RCD.Components;
using Content.Shared.Tag;
using Content.Shared.Wires;
using Content.Shared.Doors.Components;
using Content.Server.Atmos.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;

namespace Content.Server.KS14.SpecZones.Systems;

public sealed class SpecZoneSystem : SharedSpecZoneSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly BiomeSystem _biomes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SparksSystem _sparks = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenateSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;


    private List<EntityCoordinates> _zoneExitPositions = new();
    private Dictionary<string, (EntityUid, SpecialZoneMapComponent)> _activeZoneMaps = new(); // string is the zone ID
    private static DeserializationOptions _zoneDeserializationOptions = new DeserializationOptions { PauseMaps = false, InitializeMaps = true };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<EndSpecialZoneOnTriggerComponent, TriggerEvent>(OnEndZoneTrigger);

        SubscribeLocalEvent<SpecZoneKeyComponent, UseInHandEvent>(OnKeyUseInhand);
        SubscribeLocalEvent<SpecZoneKeyComponent, SpecZoneKeyDoAfterEvent>(OnBadDecision);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent args)
    {
        _zoneExitPositions.Clear();
        _activeZoneMaps.Clear();
    }

    private void InitZone(SpecialZone zone)
    {
        if (!_mapLoaderSystem.TryLoadMap(zone.MapPath, out var map, out var grids, _zoneDeserializationOptions))
            return;

        //var mapId = map.Value.Comp.MapId;
        var mapUid = map.Value.Owner;

        if (zone.ZoneBiome != null && _prototypeManager.TryIndex(zone.ZoneBiome, out BiomeTemplatePrototype? zoneBiome))
            _biomes.EnsurePlanet(mapUid, zoneBiome);

        var mapZoneComponent = EnsureComp<SpecialZoneMapComponent>(mapUid);
        mapZoneComponent.ZoneId = zone.ID;

        if (!_activeZoneMaps.TryGetValue(zone.ID, out _))
            _activeZoneMaps.Add(zone.ID, (mapUid, mapZoneComponent));

        // lel
        var damageableEnumerator = EntityQueryEnumerator<DamageableComponent, TransformComponent, MetaDataComponent>();
        while (damageableEnumerator.MoveNext(out var entityUid, out var damageable, out var transformComponent, out var metadata))
        {
            if (transformComponent.MapUid == null || transformComponent.MapUid != mapUid)
                continue;

            if (ShouldMakeInvincibleAndEdgecase(entityUid, damageable, metadata))
            {
                RemComp(entityUid, damageable);

                if (TryComp<RCDDeconstructableComponent>(entityUid, out var rcdDeconComp))
                    RemComp(entityUid, rcdDeconComp);

                if (TryComp<AnchorableComponent>(entityUid, out var anchorableComp))
                    RemComp(entityUid, anchorableComp);

                if (TryComp<AccessReaderComponent>(entityUid, out var accessComponent))
                    accessComponent.BreakOnAccessBreaker = false;
            }

        }
    }

    private bool ShouldMakeInvincibleAndEdgecase(EntityUid entityUid, DamageableComponent damageable, MetaDataComponent metadata)
    {
        if (HasComp<AirtightComponent>(entityUid))
            return true;

        if (metadata.EntityPrototype != null)
        {
            var entityProtoId = metadata.EntityPrototype.ID;

            if (entityProtoId.Contains("wall", StringComparison.OrdinalIgnoreCase))
                return true;

            if (entityProtoId.Contains("grille", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (HasComp<DoorComponent>(entityUid))
        {
            if (TryComp<WiresPanelComponent>(entityUid, out var wiresPanelComponent))
                RemComp(entityUid, wiresPanelComponent);

            return true;
        }

        if (_tagSystem.HasTag(entityUid, "Window"))
            return true;

        return false;
    }

    private void FindExitPositions()
    {
        _zoneExitPositions.Clear();
        var spawnEnum = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possibleExitPositions = new List<EntityCoordinates>();

        while (spawnEnum.MoveNext(out var spawnUid, out var spawnComponent, out var transformComponent))
            possibleExitPositions.Add(transformComponent.Coordinates);

        if (possibleExitPositions.Count == 0)
            return;

        _zoneExitPositions = possibleExitPositions;
    }

    public EntityCoordinates? GetRandomZoneEntrance(string zoneId)
    {
        var zone = _activeZoneMaps[zoneId];

        var entranceEnum = EntityQueryEnumerator<SpecialZoneSpawnComponent, TransformComponent>();
        var possibleEntrancePositions = new List<EntityCoordinates>();

        while (entranceEnum.MoveNext(out var spawnUid, out var spawnComponent, out var transformComponent))
        {
            if (spawnComponent.ZoneId != zoneId)
                continue;

            possibleEntrancePositions.Add(transformComponent.Coordinates);
        }

        if (possibleEntrancePositions.Count == 0)
            return null;

        return _random.Pick(possibleEntrancePositions);
    }

    public bool EjectFromZone(EntityUid ejecteeUid)
    {
        if (_zoneExitPositions.Count == 0)
            return false;

        var exitPosition = _random.Pick(_zoneExitPositions);

        // fuck them up a bit
        _transform.SetCoordinates(ejecteeUid, exitPosition);
        _flash.Flash(ejecteeUid, null, null, ZoneExitEffectDuration.Seconds, 0.2f);
        _sparks.DoSparks(exitPosition);

        _stunSystem.TryParalyze(ejecteeUid, ZoneExitEffectDuration, false);
        _popupSystem.PopupCoordinates($"{_identity.GetEntityIdentity(ejecteeUid)} gets twisted back into this realm!", exitPosition, PopupType.MediumCaution);

        return true;
    }

    public void InsertIntoZone(EntityUid entityUid, EntityCoordinates position)
    {
        // fuck them up a bit #2
        _transform.SetCoordinates(entityUid, position);
        _sparks.DoSparks(position);

        _rejuvenateSystem.PerformRejuvenate(entityUid);
        _stunSystem.TryParalyze(entityUid, ZoneExitEffectDuration, false);
    }

#pragma warning disable RA0030
    public void EndZone((EntityUid, SpecialZoneMapComponent) zone)
    {
        var zoneMapUid = zone.Item1;
        var allLivingMinds = _mindSystem.GetAliveHumans();

        FindExitPositions();
        Parallel.ForEach(allLivingMinds, mindEntity =>
        {
            var humanUid = mindEntity.Comp.OwnedEntity;
            if (humanUid == null)
                return;

            if (!TryComp<TransformComponent>(humanUid, out var humanTransform))
                return;

            var humanMapUid = _transform.GetMap(humanTransform.Coordinates);
            if (humanMapUid == null || humanMapUid != zoneMapUid)
                return;

            EjectFromZone(humanUid.Value);

            if (_mindSystem.TryGetSession(humanUid, out var mind))
                _audio.PlayGlobal(ZoneFinishSoundSpec, mind);
        });
    }
#pragma warning restore RA0030

    public (EntityUid, SpecialZoneMapComponent) GetRandomZone() => _activeZoneMaps.ElementAt(_random.Next(0, _activeZoneMaps.Count())).Value;
    public string GetRandomZoneId() => _activeZoneMaps.ElementAt(_random.Next(0, _activeZoneMaps.Count())).Key;

    private void OnRoundStarting(RoundStartingEvent roundStartEv)
    {
        var specZones = _prototypeManager.EnumeratePrototypes<SpecialZone>().ToList();
        specZones.ForEach(InitZone);
    }

    private void OnEndZoneTrigger(Entity<EndSpecialZoneOnTriggerComponent> triggerEnt, ref TriggerEvent triggerEv)
    {
        var triggerEntMapUid = _transform.GetMap(triggerEnt.Owner);

        if (triggerEntMapUid != null)
            foreach (var (activeZoneId, activeZone) in _activeZoneMaps)
            {
                var zoneMapUid = activeZone.Item1;
                if (triggerEntMapUid != zoneMapUid)
                    continue;

                EndZone(activeZone);
                return;
            }

        var zoneId = triggerEnt.Comp.ZoneId;
        if (zoneId != null)
        {
            if (!_activeZoneMaps.TryGetValue(zoneId, out var activeZone))
                return;

            EndZone(activeZone);

            return;
        }
    }

    private void OnKeyUseInhand(Entity<SpecZoneKeyComponent> key, ref UseInHandEvent args)
    {
        var user = args.User;
        var keyDoAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new SpecZoneKeyDoAfterEvent(), key.Owner)
        {
            DistanceThreshold = 1f,
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(keyDoAfter))
            _popupSystem.PopupEntity($"{_identity.GetEntityIdentity(user)} raises the key into the air...", user, PopupType.Medium);

        FindExitPositions();
    }

    private void OnBadDecision(Entity<SpecZoneKeyComponent> key, ref SpecZoneKeyDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;
        var targetZoneId = key.Comp.ZoneId ?? GetRandomZoneId();

        var zoneEntrancePosition = GetRandomZoneEntrance(targetZoneId);
        if (zoneEntrancePosition == null)
            return;

        _transform.TryGetMapOrGridCoordinates(user, out var useCoordinates);

        // you're fucked now
        InsertIntoZone(user, zoneEntrancePosition.Value);
        EjectFromZone(key.Owner);

        if (_mindSystem.TryGetMind(user, out var mindId, out var mindComponent) && _mindSystem.TryGetSession(mindId, out var mind))
            _audio.PlayGlobal(ZoneEnterSoundSpec, mind);

        if (useCoordinates != null)
            _popupSystem.PopupCoordinates($"{_identity.GetEntityIdentity(user)} disappears in a flash of light!", useCoordinates.Value, PopupType.LargeCaution);
    }
}

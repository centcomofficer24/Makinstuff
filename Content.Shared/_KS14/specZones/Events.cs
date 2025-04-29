using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.KS14.SpecZones.Systems;

[Serializable, NetSerializable]
public sealed partial class SpecZoneKeyDoAfterEvent : SimpleDoAfterEvent;

using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Server.DeviceNetwork;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Server.KS14.SpecZones.Systems;

/// <summary>
/// Component for a signal thingy
/// </summary>
[RegisterComponent, Serializable]
public sealed partial class SignalPropagatorComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Status = true;

    [DataField]
    public ProtoId<SinkPortPrototype> EnablePort = "On";

    [DataField]
    public ProtoId<SinkPortPrototype> DisablePort = "Off";

    [DataField]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";

    [DataField]
    public List<ProtoId<SourcePortPrototype>> HighSourcePorts = new() { "On" };

    [DataField]
    public List<ProtoId<SourcePortPrototype>> LowSourcePorts = new() { "Off" };
}

/// <summary>
/// Handles the control of output based on the input and enable ports.
/// </summary>
public sealed class SignalPropagatorSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceSignalSystem = default!;
    private float _updateAccumulator = 0;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalPropagatorComponent, ComponentInit>(CompInit);
        SubscribeLocalEvent<SignalPropagatorComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    /*
    public override void Update(float dt)
    {
        base.Update(dt);
        _updateAccumulator += dt;

        if (_updateAccumulator < 15)
            return;

        _updateAccumulator = 0;

        var propagatorQuery = EntityQueryEnumerator<SignalPropagatorComponent>();
        while (propagatorQuery.MoveNext(out var uid, out var propagatorComp))
            UpdateOutput(new Entity<SignalPropagatorComponent>(uid, propagatorComp));
    }
    */

    private void SignalPortList(Entity<SignalPropagatorComponent, DeviceLinkSourceComponent?> ent, List<ProtoId<SourcePortPrototype>> portList, bool signal)
    {
        var (entUid, propagatorComp, linkSourceComp) = ent;
        portList.ForEach(port => _deviceSignalSystem.SendSignal(ent, port, signal, linkSourceComp));
    }

    private void CompInit(Entity<SignalPropagatorComponent> ent, ref ComponentInit args)
    {
        var (entUid, propagatorComp) = ent;

        //var sourcePorts = propagatorComp.HighSourcePorts;
        //sourcePorts.AddRange(propagatorComp.LowSourcePorts);

        _deviceSignalSystem.EnsureSourcePorts(entUid, propagatorComp.HighSourcePorts.ToArray());
        _deviceSignalSystem.EnsureSourcePorts(entUid, propagatorComp.LowSourcePorts.ToArray());

        _deviceSignalSystem.EnsureSinkPorts(entUid, propagatorComp.EnablePort, propagatorComp.DisablePort, propagatorComp.TogglePort);

        UpdateOutput(ent);
    }

    private void OnSignalReceived(Entity<SignalPropagatorComponent> ent, ref SignalReceivedEvent args)
    {
        var (entUid, propagatorComp) = ent;
        UpdateOutput(ent);

        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);

        if (args.Port == propagatorComp.EnablePort)
        {
            if (state == SignalState.High || state == SignalState.Momentary)
                if (propagatorComp.Status == false)
                    SetOutput(ent, true);
        }
        else if (args.Port == propagatorComp.DisablePort)
        {
            if (state == SignalState.High || state == SignalState.Momentary)
                if (propagatorComp.Status == true)
                    SetOutput(ent, false);
        }
        else if (args.Port == propagatorComp.TogglePort)
        {
            if (state == SignalState.Momentary) // not high just momentary
                SetOutput(ent, propagatorComp.Status ^ true);
        }
    }

    public void UpdateOutput(Entity<SignalPropagatorComponent, DeviceLinkSourceComponent?> ent)
    {
        var (entUid, propagatorComp, linkSourceComp) = ent;

        if (!Resolve(ent, ref ent.Comp2))
            return;

        if (propagatorComp.Status)
        {
            //SignalPortList(ent, propagatorComp.LowSourcePorts, false);
            SignalPortList(ent, propagatorComp.HighSourcePorts, true);
        }
        else
        {
            //SignalPortList(ent, propagatorComp.HighSourcePorts, false);
            SignalPortList(ent, propagatorComp.LowSourcePorts, true);
        }


    }

    private void SetOutput(Entity<SignalPropagatorComponent> ent, bool newStatus)
    {
        ent.Comp.Status = newStatus;
        UpdateOutput(ent);
    }
}

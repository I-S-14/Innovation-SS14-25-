// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Wires;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Wires;

namespace Content.Server._IS14.Cyborgs.Wires;

/// <summary>
/// tg's AI control wire: cut it to drop the AI and keep it off, pulse it to reconnect.
/// </summary>
public sealed partial class CyborgAiLinkWireAction : ComponentWireAction<CyborgAiLinkComponent>
{
    public override Color Color { get; set; } = Color.Cyan;
    public override string Name { get; set; } = "wire-name-cyborg-ai";
    public override bool LightRequiresPower { get; set; } = false;
    public override object StatusKey { get; } = CyborgWireStatus.AiLink;

    public override StatusLightState? GetLightState(Wire wire, CyborgAiLinkComponent comp)
        => comp.Ai != null ? StatusLightState.On : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        var system = EntityManager.System<CyborgAiLinkSystem>();
        comp.LinkCut = true;
        system.Unlink((wire.Owner, comp));
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        comp.LinkCut = false;
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        if (comp.LinkCut)
            return;

        var system = EntityManager.System<CyborgAiLinkSystem>();
        system.Unlink((wire.Owner, comp));
        system.TryLinkToBestAi((wire.Owner, comp));
    }
}

/// <summary>
/// tg's law sync wire: cut it and the AI's law changes no longer reach the cyborg, pulse it to sync now.
/// </summary>
public sealed partial class CyborgLawSyncWireAction : ComponentWireAction<CyborgAiLinkComponent>
{
    public override Color Color { get; set; } = Color.Gold;
    public override string Name { get; set; } = "wire-name-cyborg-law-sync";
    public override bool LightRequiresPower { get; set; } = false;
    public override object StatusKey { get; } = CyborgWireStatus.LawSync;

    public override StatusLightState? GetLightState(Wire wire, CyborgAiLinkComponent comp)
        => comp.LawSync ? StatusLightState.On : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        comp.LawSync = false;
        EntityManager.Dirty(wire.Owner, comp);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        comp.LawSync = true;
        EntityManager.Dirty(wire.Owner, comp);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        if (comp.LawSync)
            EntityManager.System<CyborgAiLinkSystem>().SyncLaws((wire.Owner, comp));
    }
}

/// <summary>
/// tg's lockdown wire: cutting it locks the cyborg down for good, pulsing it toggles the lockdown.
/// </summary>
public sealed partial class CyborgLockdownWireAction : ComponentWireAction<CyborgAiLinkComponent>
{
    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-cyborg-lockdown";
    public override bool LightRequiresPower { get; set; } = false;
    public override object StatusKey { get; } = CyborgWireStatus.Lockdown;

    public override StatusLightState? GetLightState(Wire wire, CyborgAiLinkComponent comp)
        => comp.Lockdown ? StatusLightState.BlinkingFast : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        EntityManager.System<CyborgAiLinkSystem>().SetLockdownByAi((wire.Owner, comp), true, user);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        EntityManager.System<CyborgAiLinkSystem>().SetLockdownByAi((wire.Owner, comp), false, user);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, CyborgAiLinkComponent comp)
    {
        EntityManager.System<CyborgAiLinkSystem>().SetLockdownByAi((wire.Owner, comp), !comp.Lockdown, user);
    }
}

/// <summary>
/// tg's reset module wire: a pulse wipes the model so the cyborg picks a new one, like the reclassification board.
/// </summary>
public sealed partial class CyborgResetModelWireAction : ComponentWireAction<CyborgComponent>
{
    public override Color Color { get; set; } = Color.Purple;
    public override string Name { get; set; } = "wire-name-cyborg-reset";
    public override bool LightRequiresPower { get; set; } = false;
    public override object StatusKey { get; } = CyborgWireStatus.ResetModel;

    public override StatusLightState? GetLightState(Wire wire, CyborgComponent comp)
        => comp.Model != null ? StatusLightState.On : StatusLightState.Off;

    public override bool Cut(EntityUid user, Wire wire, CyborgComponent comp) => true;

    public override bool Mend(EntityUid user, Wire wire, CyborgComponent comp) => true;

    public override void Pulse(EntityUid user, Wire wire, CyborgComponent comp)
    {
        if (comp.Model != null)
            EntityManager.System<SharedCyborgSystem>().ResetModel((wire.Owner, comp));
    }
}

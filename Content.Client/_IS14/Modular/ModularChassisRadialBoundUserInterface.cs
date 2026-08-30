// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Client._IS14.Modular.Controls;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Modular;

/// <summary>
///     The wearer's quick module ring: one press of the action, one click, done.
///
///     Built straight from the modules' own networked state rather than from
///     <see cref="ModularChassisUiState"/>. The readout's state is a server push, so
///     waiting for one would open an empty ring in the middle of the screen and fill it
///     a tick later — which is a long time for a menu you are holding open with a
///     keypress. Everything the ring shows is on <see cref="ChassisModuleComponent"/>,
///     and that is networked.
/// </summary>
[UsedImplicitly]
public sealed class ModularChassisRadialBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IGameTiming _timing = default!;

    [ViewVariables]
    private SimpleRadialMenu? _menu;

    public ModularChassisRadialBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(BuildOptions());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildOptions()
    {
        var options = new List<RadialMenuOptionBase>();

        if (!EntMan.TryGetComponent<ModularChassisComponent>(Owner, out var chassis))
            return options;

        var chassisSystem = EntMan.System<SharedModularChassisSystem>();

        foreach (var module in chassisSystem.GetModuleEntities((Owner, chassis)))
        {
            if (!EntMan.TryGetComponent<ChassisModuleComponent>(module, out var comp))
                continue;

            // Passive modules are left out on purpose. The ring is for the things there
            // is a decision to make about; a module that is simply always on would be a
            // slice you can click that does nothing.
            if (comp.Kind == ModuleKind.Passive)
                continue;

            options.Add(BuildModuleOption(module, comp));
        }

        return options;
    }

    private RadialMenuActionOptionBase BuildModuleOption(EntityUid module, ChassisModuleComponent comp)
    {
        var name = ModuleName(module);
        var cooling = comp.CooldownEnd > _timing.CurTime;

        string tooltip;
        Color background;

        if (!comp.Enabled)
        {
            // The client knows the module is not runnable but not why — the reason is
            // worked out server-side. Saying so plainly beats guessing: click it anyway
            // and the suit answers with the actual complaint.
            tooltip = Loc.GetString("chassis-radial-blocked",
                ("module", name),
                ("reason", Loc.GetString("chassis-module-unavailable")));
            background = ChassisStyle.Muted;
        }
        else if (cooling)
        {
            tooltip = Loc.GetString("chassis-radial-blocked",
                ("module", name),
                ("reason", Loc.GetString("chassis-module-cooldown")));
            background = ChassisStyle.Warn;
        }
        else if (comp.Active)
        {
            tooltip = Loc.GetString("chassis-radial-active", ("module", name));
            background = ChassisStyle.Accent;
        }
        else
        {
            tooltip = name;
            background = ChassisStyle.PanelRaised;
        }

        // Blocked slices stay clickable. The suit refuses with a popup naming the real
        // reason, which is better information than the ring could have put in a tooltip.
        return new RadialMenuActionOption<EntityUid>(Select, module)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(module),
            ToolTip = tooltip,
            BackgroundColor = background.WithAlpha(0.55f),
            HoverBackgroundColor = background.WithAlpha(0.85f),
        };
    }

    private void Select(EntityUid module)
    {
        SendMessage(new ChassisSelectModuleMessage(EntMan.GetNetEntity(module)));
    }

    private string ModuleName(EntityUid module)
    {
        return EntMan.TryGetComponent<MetaDataComponent>(module, out var meta)
            ? meta.EntityName
            : string.Empty;
    }
}

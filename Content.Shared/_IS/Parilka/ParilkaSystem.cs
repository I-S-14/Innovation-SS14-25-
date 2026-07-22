using System;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Temperature;

namespace Content.Shared._IS.Parilka;

public sealed class ParilkaSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ParilkaComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ParilkaComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, ParilkaComponent comp, MapInitEvent args)
    {
        _appearance.SetData(uid, ParilkaVisuals.IsRunning, comp.Active);
        _appearance.SetData(uid, ParilkaVisuals.ShowSteam, false);
    }

    private bool HasWater(EntityUid uid)
    {
        if (!_solution.TryGetSolution(uid, "tank", out var soln, out _))
            return false;

        var tank = soln.Value.Comp.Solution;
        return tank.GetTotalPrototypeQuantity("Water") > 0;
    }

    private void RemoveWater(EntityUid uid, int amount)
    {
        if (!_solution.TryGetSolution(uid, "tank", out var soln, out _))
            return;

        var tank = soln.Value.Comp.Solution;
        tank.RemoveReagent("Water", amount);
    }

    private bool IsHot(EntityUid used)
    {
        var ev = new IsHotEvent();
        RaiseLocalEvent(used, ev);
        return ev.IsHot;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ParilkaComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SteamVisualTimer > 0f)
            {
                comp.SteamVisualTimer -= frameTime;

                if (comp.SteamVisualTimer <= 0f)
                {
                    comp.SteamVisualTimer = 0f;
                    _appearance.SetData(uid, ParilkaVisuals.ShowSteam, false);
                }
            }

            if (!comp.Active)
                continue;

            if (comp.Temperature >= 100f && HasWater(uid))
            {
                comp.SteamTimer += frameTime;

                if (comp.SteamTimer >= comp.SteamInterval)
                {
                    comp.SteamTimer = 0f;
                    RemoveWater(uid, 5);
                    RaiseLocalEvent(uid, new ParilkaSteamEvent(5f));

                    comp.SteamVisualTimer = comp.SteamVisualDuration;
                    _appearance.SetData(uid, ParilkaVisuals.ShowSteam, true);
                }
            }
            else
            {
                comp.SteamTimer = 0f;
            }

            comp.BurnTime += frameTime;
            if (comp.BurnTime < comp.FuelUseInterval)
                continue;

            comp.BurnTime -= comp.FuelUseInterval;

            if (comp.Fuel <= 0)
            {
                comp.Active = false;
                _appearance.SetData(uid, ParilkaVisuals.IsRunning, false);
                continue;
            }

            comp.Fuel = Math.Max(0, comp.Fuel - comp.UsePerSecond);
            comp.Temperature = Math.Min(comp.Temperature + comp.HeatPerSecond, 500f);
        }
    }

    private void OnInteractUsing(EntityUid uid, ParilkaComponent component, InteractUsingEvent args)
    {
        var used = args.Used;

        if (_tag.HasTag(used, "FuelForFire"))
        {
            if (component.Fuel >= component.MaxFuel)
                return;

            if (!EntityManager.TryGetComponent<StackComponent>(used, out var stack))
                return;

            if (!_stack.TryUse((used, stack), 1))
                return;

            component.Fuel = Math.Min(component.Fuel + 1, component.MaxFuel);
            args.Handled = true;
            return;
        }

        if (!IsHot(used))
            return;

        if (component.Fuel <= 0)
            return;

        if (component.Active)
            return;

        component.Active = true;
        _appearance.SetData(uid, ParilkaVisuals.IsRunning, true);
        args.Handled = true;
    }
}

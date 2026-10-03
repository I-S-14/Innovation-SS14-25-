// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// What a technology does besides unlocking recipes. Written in YAML with <c>!type:</c>, so a
/// new kind of reward is a new class here and nothing else.
/// </summary>
/// <remarks>
/// Effects live in shared code because the console prints their descriptions on the card, but
/// they only ever run server-side, through <see cref="IS14TechEffectContext"/>. The context
/// hands them the few operations they need instead of letting them reach for systems, which
/// keeps them testable and safe inside the engine sandbox.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class IS14TechEffect
{
    /// <summary>
    /// One line for the card: "печать дешевле на 10%". Left out, the effect still works but
    /// the player has no idea it happened, so fill it in.
    /// </summary>
    [DataField]
    public LocId? Description;

    public abstract void Apply(in IS14TechEffectContext context);
}

/// <summary>Everything an effect is allowed to touch.</summary>
public readonly struct IS14TechEffectContext
{
    /// <summary>Station that bought the technology; owner of balances and modifiers.</summary>
    public readonly EntityUid Station;

    /// <summary>The console it was bought at — where physical rewards appear.</summary>
    public readonly EntityUid Console;

    public readonly EntityCoordinates ConsoleCoordinates;

    private readonly Action<ProtoId<ResearchPointTypePrototype>, int> _addPoints;
    private readonly Action<ProtoId<IS14ResearchModifierPrototype>, float> _addModifier;
    private readonly Action<EntProtoId, EntityCoordinates> _spawn;

    public IS14TechEffectContext(
        EntityUid station,
        EntityUid console,
        EntityCoordinates consoleCoordinates,
        Action<ProtoId<ResearchPointTypePrototype>, int> addPoints,
        Action<ProtoId<IS14ResearchModifierPrototype>, float> addModifier,
        Action<EntProtoId, EntityCoordinates> spawn)
    {
        Station = station;
        Console = console;
        ConsoleCoordinates = consoleCoordinates;
        _addPoints = addPoints;
        _addModifier = addModifier;
        _spawn = spawn;
    }

    public void AddPoints(ProtoId<ResearchPointTypePrototype> type, int amount) => _addPoints(type, amount);

    public void AddModifier(ProtoId<IS14ResearchModifierPrototype> modifier, float delta) => _addModifier(modifier, delta);

    public void Spawn(EntProtoId prototype, EntityCoordinates coordinates) => _spawn(prototype, coordinates);
}

/// <summary>Moves a station-wide number: print speed, payout rate, instrument range.</summary>
public sealed partial class IS14ModifierEffect : IS14TechEffect
{
    [DataField(required: true)]
    public ProtoId<IS14ResearchModifierPrototype> Modifier;

    /// <summary>Added to the modifier's running total. Negative is fine and often the point.</summary>
    [DataField(required: true)]
    public float Delta;

    public override void Apply(in IS14TechEffectContext context)
    {
        context.AddModifier(Modifier, Delta);
    }
}

/// <summary>A one-off grant of research data: a breakthrough that pays for the next one.</summary>
public sealed partial class IS14GrantPointsEffect : IS14TechEffect
{
    [DataField(required: true)]
    public ProtoId<ResearchPointTypePrototype> PointType;

    [DataField(required: true)]
    public int Amount;

    public override void Apply(in IS14TechEffectContext context)
    {
        context.AddPoints(PointType, Amount);
    }
}

/// <summary>
/// Prints a physical item at the console: the first working instrument comes out of the
/// research, not out of a lathe queue.
/// </summary>
public sealed partial class IS14GrantEntityEffect : IS14TechEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Count = 1;

    public override void Apply(in IS14TechEffectContext context)
    {
        for (var i = 0; i < Count; i++)
        {
            context.Spawn(Prototype, context.ConsoleCoordinates);
        }
    }
}

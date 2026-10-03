// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text;
using Content.Shared._IS14.Research;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;

namespace Content.Server._IS14.Research;

/// <summary>
/// Makes a tank or a canister of gas describe itself to the measuring core: what is in it, and
/// whether it is the hottest or the most compressed thing the station has produced this shift.
/// </summary>
/// <remarks>
/// No machine of its own and no new measuring code — the sample answers
/// <see cref="IS14GetExperimentProfileEvent"/>, which is what that event exists for. This is
/// the atmospherics half of industrial data: a mixture nobody has made before is worth data,
/// and so is a record, while hauling the same canister back twenty times is not.
/// </remarks>
public sealed class IS14GasSampleSystem : EntitySystem
{
    [Dependency] private readonly IS14RecordSystem _records = default!;
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;

    /// <summary>Below this there is not enough gas to say anything about it.</summary>
    public const float MinimumMoles = 0.5f;

    /// <summary>Mixtures are profiled by composition rounded to this many percent.</summary>
    public const int CompositionStep = 10;

    /// <summary>Records only count from here up: room-temperature air is not an achievement.</summary>
    public const float MinimumRecordTemperature = Atmospherics.T20C + 100f;

    public const float MinimumRecordPressure = 2000f;

    /// <summary>What a record adds to the payout at most, on top of the mixture itself.</summary>
    public const float RecordBonus = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTankComponent, IS14GetExperimentProfileEvent>(OnTankProfile);
        SubscribeLocalEvent<GasCanisterComponent, IS14GetExperimentProfileEvent>(OnCanisterProfile);
    }

    private void OnTankProfile(Entity<GasTankComponent> ent, ref IS14GetExperimentProfileEvent args)
    {
        Describe(ent.Owner, ent.Comp.Air, ref args);
    }

    private void OnCanisterProfile(Entity<GasCanisterComponent> ent, ref IS14GetExperimentProfileEvent args)
    {
        Describe(ent.Owner, ent.Comp.Air, ref args);
    }

    private void Describe(EntityUid sample, GasMixture mixture, ref IS14GetExperimentProfileEvent args)
    {
        if (mixture.TotalMoles < MinimumMoles)
        {
            args.Refuse = true;
            args.RefuseReason = "is14-gas-sample-empty";
            return;
        }

        args.ProfileKey = Composition(mixture);
        args.PointTypeOverride = "Industrial";

        // The container is handed back: a mixture is data, and the gas in it is the atmos
        // department's property, not the analyzer's lunch.
        args.PreventConsume = true;

        if (_points.ResolveStation(args.Device) is not { } station)
            return;

        var bonus = 0f;

        if (mixture.Temperature >= MinimumRecordTemperature)
            bonus += _records.TryRecord(station, "atmos:temperature", mixture.Temperature) * RecordBonus;

        if (mixture.Pressure >= MinimumRecordPressure)
            bonus += _records.TryRecord(station, "atmos:pressure", mixture.Pressure) * RecordBonus;

        args.Multiplier += bonus;
    }

    /// <summary>
    /// "plasma70-oxygen30" — the mixture by share, rounded, so the same recipe made twice is
    /// the same measurement and a genuinely different ratio is not.
    /// </summary>
    public string Composition(GasMixture mixture)
    {
        var total = mixture.TotalMoles;
        var parts = new List<(string Name, int Share)>();

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mixture.GetMoles(i);

            if (moles <= 0f)
                continue;

            var share = (int) MathF.Round(moles / total * 100f / CompositionStep) * CompositionStep;

            if (share <= 0)
                continue;

            parts.Add((((Gas) i).ToString().ToLowerInvariant(), share));
        }

        if (parts.Count == 0)
            return "trace";

        var builder = new StringBuilder();

        foreach (var (name, share) in parts.OrderByDescending(part => part.Share).ThenBy(part => part.Name))
        {
            if (builder.Length > 0)
                builder.Append('-');

            builder.Append(name).Append(share);
        }

        return builder.ToString();
    }
}

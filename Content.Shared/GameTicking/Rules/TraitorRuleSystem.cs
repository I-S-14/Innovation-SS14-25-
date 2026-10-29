using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.PDA.Ringer;
using Content.Server.Traitor.Uplink;
using Content.Goobstation.Maths.FixedPoint;
using System.Linq;
using System.Text;
using Content.Shared.Antag;
using Content.Shared.Codewords;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Rules.Components;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.PDA;
using Content.Shared.Random.Helpers;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Random;
using System.Linq;
using System.Text;
using Content.Server.Codewords;
using Content.Shared.Clumsy;
using Content.Goobstation.Common.CCVar;

namespace Content.Shared.GameTicking.Rules;

public sealed partial class TraitorRuleSystem : GameRuleSystem<TraitorRuleComponent> // Goob - made partial
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] protected CodewordSystem Codeword = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private SharedRoleSystem _roleSystem = default!;

    private static readonly Color TraitorCodewordColor = Color.FromHex("#cc3b3b");

    [SubscribeLocalEvent]
    private void AfterEntitySelected(Entity<TraitorRuleComponent> rule, ref AfterAntagEntitySelectedEvent args)
    {
        Log.Debug($"AfterAntagEntitySelected {ToPrettyString(rule)}");
        MakeTraitor(args.EntityUid, rule);
    }

    public bool MakeTraitor(EntityUid traitor, Entity<TraitorRuleComponent> rule)
    {
        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - start");
        var factionCodewords = Codeword.GetCodewords(rule.Comp.CodewordFactionPrototypeId);

        //Grab the mind if it wasn't provided
        if (!_mindSystem.TryGetMind(traitor, out var mindId, out var mind))
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)}  - failed, no Mind found");
            return false;
        }

        var briefing = "";

        if (rule.Comp.GiveCodewords)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - added codewords flufftext to briefing");
            briefing = Loc.GetString("traitor-role-codewords-short", ("codewords", string.Join(", ", factionCodewords)));
        }

        var issuer = _random.Pick(ProtoMan.Index(rule.Comp.ObjectiveIssuers));

        // <Goob>
        string? uplinkBriefing = null;
        string? uplinkBriefingShort = null;
        // </Goob>

        if (rule.Comp.GiveUplink)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink start");
            // Calculate the amount of currency on the uplink.
            var startingBalance = rule.Comp.StartingBalance;
            if (_jobs.MindTryGetJob(mindId, out var prototype))
            {
                if (startingBalance < prototype.AntagAdvantage) // Can't use Math functions on FixedPoint2
                    startingBalance = 0;
                else
                    startingBalance = startingBalance - prototype.AntagAdvantage;
            }

            // Choose and generate an Uplink, and return the uplink code if applicable
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink request start");
            if (!RequestUplink(traitor, mindId, startingBalance, out uplinkBriefing, out uplinkBriefingShort)) // Goob
                return false;
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink request completed");
        }

        string[]? codewords = null;
        if (rule.Comp.GiveCodewords)
        {
            // Goob start
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - added codewords flufftext to briefing");
            briefing = Loc.GetString("traitor-role-codewords-short", ("codewords", string.Join(", ", factionCodewords)));
            // Goob end
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - set codewords from component");
            codewords = factionCodewords;
        }

        if (rule.Comp.GiveBriefing)
        {
            _antag.SendBriefing(traitor, GenerateBriefing(codewords, uplinkBriefing, issuer), Color.Crimson, component.GreetSoundNotification); // Goob
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Sent the Briefing");
        }

        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Adding TraitorMind");
        rule.Comp.TraitorMinds.Add(mindId);

        // Assign briefing
        //Since this provides neither an antag/job prototype, nor antag status/roletype,
        //and is intrinsically related to the traitor role
        //it does not need to be a separate Mind Role Entity
        if (_roleSystem.MindHasRole<TraitorRoleComponent>((mindId, mind), out var traitorRole))
        {
            // goob edit - traitor flavor.
            traitorRole.Value.Comp2.ObjectiveIssuer = issuer;

            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Add traitor briefing components");
            EnsureComp<RoleBriefingComponent>(traitorRole.Value.Owner, out var briefingComp);
            // Goobstation Change - If you remove this, we lose ringtones and flavor in char menu. Upstream's version sucks.
            briefingComp.Briefing = GenerateBriefingCharacter(codewords, uplinkBriefingShort, issuer);
        }
        else
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - did not get traitor briefing");
        }

        // Change the faction
        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Change faction");
        _npcFaction.RemoveFaction(traitor, rule.Comp.NanoTrasenFaction, false);
        _npcFaction.AddFaction(traitor, rule.Comp.SyndicateFaction);

        // goob edit - clumsy antag no more
        var shouldRemoveClumsy = _cfg.GetCVar(GoobCVars.RemoveClumsyOnAntag);
        if (TryComp(traitor, out ClumsyComponent? clumsy) && shouldRemoveClumsy)
        {
            // if not for the clown car i would've nuked it off the planet
            clumsy.ClumsyCatching = false;
            clumsy.ClumsyDefib = false;
            clumsy.ClumsyGuns = false;
            clumsy.ClumsyVaulting = false;
            clumsy.ClumsyHypo = false;

            _popup.PopupEntity(Loc.GetString("antag-gain-remove-clumsy"), traitor, traitor, Shared.Popups.PopupType.Medium);
        }

        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Finished");
        return true;
    }

    protected virtual (Note[]?, string) RequestUplink(EntityUid traitor, FixedPoint2 startingBalance, string briefing)
    {
        var pda = _uplink.FindUplinkTarget(traitor);
        Note[]? code = null;

        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink add");
        var uplinked = _uplink.AddUplink(traitor, startingBalance, pda, true);

        if (pda is not null && uplinked)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink is PDA");
            // Codes are only generated if the uplink is a PDA
            var ev = new GenerateUplinkCodeEvent();
            RaiseLocalEvent(pda.Value, ref ev);

            if (ev.Code is { } generatedCode)
            {
                code = generatedCode;

                // If giveUplink is false the uplink code part is omitted
                briefing = string.Format("{0}\n{1}",
                    briefing,
                    Loc.GetString("traitor-role-uplink-code-short", ("code", string.Join("-", code).Replace("sharp", "#"))));
                return (code, briefing);
            }

            Log.Error($"MakeTraitor {ToPrettyString(traitor)} failed to generate an uplink code on {ToPrettyString(pda)}.");
        }
        else if (pda is null && uplinked)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink is implant");
            briefing += "\n" + Loc.GetString("traitor-role-uplink-implant-short");
        }
        else
        {
            Log.Error($"MakeTraitor failed on {ToPrettyString(traitor)} - No uplink could be added");
        }

        return (null, briefing);
    }
    Goob end
    */

    private (Note[]?, string) RequestUplink(EntityUid traitor, FixedPoint2 startingBalance, string briefing)
    {
        var pda = _uplink.FindUplinkTarget(traitor);
        Note[]? code = null;

        Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink add");
        var uplinked = _uplink.AddUplink(traitor, startingBalance, null, out _, out _, pda, true); // Goob

        if (pda is not null && uplinked)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink is PDA");
            // Codes are only generated if the uplink is a PDA
            var ev = new GenerateUplinkCodeEvent<Note[]>(); // Goob
            RaiseLocalEvent(pda.Value, ref ev);

            if (ev.Code is { } generatedCode)
            {
                code = generatedCode;

                // If giveUplink is false the uplink code part is omitted
                briefing = string.Format("{0}\n{1}",
                    briefing,
                    Loc.GetString("traitor-role-uplink-code-short", ("code", string.Join("-", code).Replace("sharp", "#"))));
                return (code, briefing);
            }

            Log.Error($"MakeTraitor {ToPrettyString(traitor)} failed to generate an uplink code on {ToPrettyString(pda)}.");
        }
        else if (pda is null && uplinked)
        {
            Log.Debug($"MakeTraitor {ToPrettyString(traitor)} - Uplink is implant");
            briefing += "\n" + Loc.GetString("traitor-role-uplink-implant-short");
        }
        else
        {
            Log.Error($"MakeTraitor failed on {ToPrettyString(traitor)} - No uplink could be added");
        }

        return (null, briefing);
    }

    // TODO: AntagCodewordsComponent
    private void OnObjectivesTextPrepend(EntityUid uid, TraitorRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        if (comp.GiveCodewords)
            args.Text += "\n" + Loc.GetString("traitor-round-end-codewords", ("codewords", string.Join(", ", _codewordSystem.GetCodewords(comp.CodewordFactionPrototypeId))));
    }

    // TODO: figure out how to handle this? add priority to briefing event?
    private string GenerateBriefing(string[]? codewords, string? uplinkBriefing, string objectiveIssuer) // Goob
    {
        var issuer = objectiveIssuer.Replace(" ", "").ToLower();
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("traitor-role-greeting", ("corporation", issuer ?? Loc.GetString("objective-issuer-unknown"))));
        if (codewords != null)
            sb.AppendLine(Loc.GetString("traitor-role-codewords", ("codewords", string.Join(", ", codewords))));
        if (uplinkBriefing != null) // Goob
            sb.AppendLine(uplinkBriefing);
        else
            sb.AppendLine(Loc.GetString("traitor-role-uplink-implant"));

        return sb.ToString();
    }

    public List<(EntityUid Id, MindComponent Mind)> GetOtherTraitorMindsAliveAndConnected(MindComponent ourMind)
    {
        List<(EntityUid Id, MindComponent Mind)> allTraitors = new();

        var query = EntityQueryEnumerator<TraitorRuleComponent>();
        while (query.MoveNext(out var uid, out var traitor))
        {
            foreach (var role in GetOtherTraitorMindsAliveAndConnected(ourMind, (uid, traitor)))
            {
                if (!allTraitors.Contains(role))
                    allTraitors.Add(role);
            }
        }

        return allTraitors;
    }

    private List<(EntityUid Id, MindComponent Mind)> GetOtherTraitorMindsAliveAndConnected(MindComponent ourMind, Entity<TraitorRuleComponent> rule)
    {
        var traitors = new List<(EntityUid Id, MindComponent Mind)>();
        foreach (var mind in _antag.GetAntagMinds(rule.Owner))
        {
            if (mind.Comp == ourMind)
                continue;

            traitors.Add((mind, mind));
        }

        return traitors;
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._IS14.CCVar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Shared._IS14.Paper;

/// <summary>
///     Applies a stamp where the player aimed it in the paper UI.
///     <see cref="PaperSystem"/> only opens the document when a stamp is used on it; the actual
///     stamping happens here, once the player has clicked a spot.
/// </summary>
public sealed class StampPlacementSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    ///     How far a stamp may be tilted from upright, in radians. Keeps the impression readable
    ///     and stops a stamp rotated on its side from sticking out of the page.
    /// </summary>
    public const float MaxRotation = 0.5f;

    /// <summary>
    ///     Upper bound on impressions per document. The same stamp may be used repeatedly, so
    ///     something has to stop a page from growing without limit; the list is networked to every
    ///     client that reads the document.
    /// </summary>
    public int MaxStampsPerPage => _cfg.GetCVar(IS14CVars.PaperMaxStamps);

    /// <summary>
    ///     Ink colour of a signature, shared so the placement preview matches what finally lands
    ///     on the page.
    /// </summary>
    public static readonly Color SignatureColor = Color.DarkSlateGray;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, PaperPlaceStampMessage>(OnPlaceStamp);
    }

    private void OnPlaceStamp(Entity<PaperComponent> entity, ref PaperPlaceStampMessage args)
    {
        if (_hands.GetActiveItem(args.Actor) is not { } stamp
            || !TryComp<StampComponent>(stamp, out var stampComp))
            return;

        var info = PaperSystem.GetStampInfo(stampComp);
        info.Position = Vector2.Clamp(args.Position, Vector2.Zero, Vector2.One);
        info.Rotation = Math.Clamp(args.Rotation, -MaxRotation, MaxRotation);

        // The client hides the preview once the page is full, so this is a safety net against a
        // stale UI rather than something a player should normally run into.
        if (entity.Comp.StampedBy.Count >= MaxStampsPerPage)
        {
            _popup.PopupClient(Loc.GetString("paper-component-action-stamp-paper-full",
                    ("target", entity.Owner)),
                args.Actor,
                args.Actor);
            return;
        }

        if (!_paper.TryStamp(entity, info, stampComp.StampState))
            return;

        var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other",
            ("user", args.Actor),
            ("target", entity.Owner),
            ("stamp", stamp));
        _popup.PopupEntity(stampPaperOtherMessage,
            args.Actor,
            Filter.PvsExcept(args.Actor, entityManager: EntityManager),
            true);

        var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self",
            ("target", entity.Owner),
            ("stamp", stamp));
        _popup.PopupClient(stampPaperSelfMessage, args.Actor, args.Actor);

        _audio.PlayPredicted(stampComp.Sound, entity.Owner, args.Actor);

        _paper.UpdateUserInterface(entity);
    }
}

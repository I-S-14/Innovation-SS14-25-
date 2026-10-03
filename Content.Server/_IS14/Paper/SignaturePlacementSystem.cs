// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._DV.Paper;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Paper;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server._IS14.Paper;

/// <summary>
///     Writes a signature where the signer aimed it in the paper UI.
///     <see cref="SignatureSystem"/> only opens the document and records that a signature was
///     asked for; this puts it on the page once a spot has been clicked.
/// </summary>
public sealed class SignaturePlacementSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SignatureSystem _signature = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, PaperPlaceSignatureMessage>(OnPlaceSignature);
        SubscribeLocalEvent<PaperComponent, PaperCancelSignatureMessage>(OnCancelSignature);
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnUiClosed);
    }

    private void OnPlaceSignature(Entity<PaperComponent> entity, ref PaperPlaceSignatureMessage args)
    {
        // Only the player who asked to sign may, and the pen has to still be in hand. The client is
        // trusted for the spot on the page, never for who is signing or whether they may.
        if (entity.Comp.SignatureRequestedBy != args.Actor)
            return;

        if (_hands.GetActiveItem(args.Actor) is not { } pen || !_tags.HasTag(pen, WriteTag))
            return;

        if (entity.Comp.StampedBy.Count >= _cfg.GetCVar(IS14CVars.PaperMaxStamps))
        {
            _popup.PopupClient(Loc.GetString("paper-component-action-stamp-paper-full",
                    ("target", entity.Owner)),
                args.Actor,
                args.Actor);
            return;
        }

        var position = Vector2.Clamp(args.Position, Vector2.Zero, Vector2.One);
        var rotation = Math.Clamp(args.Rotation,
            -StampPlacementSystem.MaxRotation,
            StampPlacementSystem.MaxRotation);

        if (!_signature.TrySignPaper(entity, args.Actor, pen, position, rotation))
            return;

        ClearRequest(entity, args.Actor);
    }

    private void OnCancelSignature(Entity<PaperComponent> entity, ref PaperCancelSignatureMessage args)
    {
        ClearRequest(entity, args.Actor);
    }

    private void OnUiClosed(Entity<PaperComponent> entity, ref BoundUIClosedEvent args)
    {
        // Don't leave an offer hanging on a document the signer walked away from.
        ClearRequest(entity, args.Actor);
    }

    private void ClearRequest(Entity<PaperComponent> entity, EntityUid actor)
    {
        if (entity.Comp.SignatureRequestedBy != actor)
            return;

        entity.Comp.SignatureRequestedBy = null;
        entity.Comp.SignatureRequestedName = null;
        _paper.UpdateUserInterface(entity);
    }
}

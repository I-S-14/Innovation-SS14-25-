// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Paper;
using Content.Goobstation.Shared.Devil;
using Content.Server.Access.Systems;
using Content.Server.Popups;
using Content.Shared.Paper;
using Content.Shared._IS14.Paper; //IS14-change: signature placement
using System.Numerics; //IS14-change: signature placement
using static Content.Shared.Paper.PaperComponent; //IS14-change: PaperUiKey
using Content.Server.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Player;

namespace Content.Server._DV.Paper;

public sealed class SignatureSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!; //IS14-change: signature placement

    // The sprite used to visualize "signatures" on paper entities.
    private const string SignatureStampState = "paper_stamp-signature";


    public override void Initialize()
    {
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnGetAltVerbs(Entity<PaperComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Using is not {} pen || !_tags.HasTag(pen, "Write"))
            return;

        var user = args.User;
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                //IS14-change: signing no longer lands blind, it opens the page to be aimed
                OfferSignaturePlacement(ent, user, pen);
            },
            Text = Loc.GetString("paper-sign-verb"),
            DoContactInteraction = true,
            Priority = 10
        };
        args.Verbs.Add(verb);
    }

    //IS14-change start: signatures are placed by hand, like stamps
    /// <summary>
    ///     Opens the document for the signer and hands the UI a signature to position. Nothing is
    ///     written yet; <see cref="TrySignPaper"/> runs once they have picked a spot.
    /// </summary>
    public void OfferSignaturePlacement(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
        // Cheap rejections up front, so a signature that would be refused never opens the page.
        if (!CanSign(paper, signer, pen))
        {
            _popup.PopupEntity(Loc.GetString("paper-signed-failure", ("target", paper.Owner)),
                signer,
                signer,
                PopupType.SmallCaution);
            return;
        }

        paper.Comp.SignatureRequestedBy = signer;
        paper.Comp.SignatureRequestedName = DetermineEntitySignature(signer);

        _uiSystem.OpenUi(paper.Owner, PaperUiKey.Key, signer);
        _paper.UpdateUserInterface(paper);
    }

    /// <summary>
    ///     Whether this pen and this signer are allowed to sign this document at all.
    /// </summary>
    public bool CanSign(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
        var ev = new SignAttemptEvent(paper, signer);
        RaiseLocalEvent(pen, ref ev);
        if (ev.Cancelled)
            return false;

        var paperEvent = new BeingSignedAttemptEvent(paper, signer); // Goobstation
        RaiseLocalEvent(paper.Owner, ref paperEvent);
        return !paperEvent.Cancelled;
    }
    //IS14-change end

    /// <summary>
    ///     Tries to add a signature to the paper with signer's name.
    /// </summary>
    public bool TrySignPaper(Entity<PaperComponent> paper,
        EntityUid signer,
        EntityUid pen,
        Vector2? position = null, //IS14-change: where the signer put it, null for a blind signature
        float rotation = 0.0f) //IS14-change
    {
        var comp = paper.Comp;

        if (!CanSign(paper, signer, pen)) //IS14-change: the attempt events moved into CanSign
            return false;

        var signatureName = DetermineEntitySignature(signer);

        var stampInfo = new StampDisplayInfo()
        {
            StampedName = signatureName,
            StampedColor = StampPlacementSystem.SignatureColor, //IS14-change: shared with the preview
            Position = position, //IS14-change
            Rotation = rotation, //IS14-change
        };

        if (!comp.StampedBy.Contains(stampInfo) && _paper.TryStamp(paper, stampInfo, SignatureStampState))
        {
            // Show popups and play a paper writing sound
            if (!HasComp<DevilComponent>(signer)) // Goobstation - Don't display popups for devils, it covers the others.
            {
                var signedOtherMessage = Loc.GetString("paper-signed-other", ("user", signer), ("target", paper.Owner));
                _popup.PopupEntity(signedOtherMessage, signer, Filter.PvsExcept(signer, entityManager: EntityManager), true);

                var signedSelfMessage = Loc.GetString("paper-signed-self", ("target", paper.Owner));
                _popup.PopupEntity(signedSelfMessage, signer, signer);
            }

            _audio.PlayPvs(comp.Sound, signer);

            _paper.UpdateUserInterface(paper);

            var evSignSucessfulEvent = new SignSuccessfulEvent(paper, signer); // Goobstation - Devil Antagonist
            RaiseLocalEvent(paper, ref evSignSucessfulEvent); // Goobstation - Devil Antagonist

            return true;
        }
        else
        {
            // Show an error popup
            _popup.PopupEntity(Loc.GetString("paper-signed-failure", ("target", paper.Owner)), signer, signer, PopupType.SmallCaution);

            return false;
        }
    }

    public string DetermineEntitySignature(EntityUid uid) //IS14-change: was private, the placement flow needs it
    {
        // Goobstation - Allow devils to sign their true name.
        if (TryComp<DevilComponent>(uid, out var devilComp) && !string.IsNullOrWhiteSpace(devilComp.TrueName))
            return devilComp.TrueName;

        // If the entity has an ID, use the name on it.
        if (_idCard.TryFindIdCard(uid, out var id) && !string.IsNullOrWhiteSpace(id.Comp.FullName))
            return id.Comp.FullName;

        // Alternatively, return the entity name
        return Name(uid);
    }
}

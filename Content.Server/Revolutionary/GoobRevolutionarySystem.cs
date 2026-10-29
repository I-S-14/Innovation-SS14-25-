// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._EinsteinEngines.Language;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Revolutionary.Components;

namespace Content.Server.Revolutionary;

/// <summary>
///     Goob Station - Revolutionary Language and polymorph carry-over.
///     Used to be the body of the server RevolutionarySystem; wizden folded that system into Shared,
///     so the Goob part lives on as its own system. ComponentStartup is owned by the shared system,
///     hence ComponentInit here.
/// </summary>
public sealed class GoobRevolutionarySystem : EntitySystem
{
    [Dependency] private readonly LanguageSystem _languageSystem = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryComponent, ComponentInit>(OnRevolutionaryComponentInit);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentInit>(OnRevolutionaryComponentInit);

        SubscribeLocalEvent<RevolutionaryComponent, ComponentShutdown>(OnRevolutionaryComponentShutdown);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentShutdown>(OnRevolutionaryComponentShutdown);

        SubscribeLocalEvent<RevolutionaryComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<HeadRevolutionaryComponent, PolymorphedEvent>(OnHeadPolymorphed);
    }

    private void OnPolymorphed(Entity<RevolutionaryComponent> ent, ref PolymorphedEvent args)
        => _polymorph.CopyPolymorphComponent<RevolutionaryComponent>(ent, args.NewEntity);

    private void OnHeadPolymorphed(Entity<HeadRevolutionaryComponent> ent, ref PolymorphedEvent args)
        => _polymorph.CopyPolymorphComponent<HeadRevolutionaryComponent>(ent, args.NewEntity);

    private void OnRevolutionaryComponentInit<T>(EntityUid uid, T component, ComponentInit args)
    {
        switch (component)
        {
            case HeadRevolutionaryComponent headRevComp:
                _languageSystem.AddLanguage(uid, headRevComp.Language);
                break;
            case RevolutionaryComponent revComp:
                _languageSystem.AddLanguage(uid, revComp.Language);
                break;
        }
    }

    private void OnRevolutionaryComponentShutdown<T>(EntityUid uid, T component, ComponentShutdown args)
    {
        switch (component)
        {
            case HeadRevolutionaryComponent headRevComp:
                _languageSystem.RemoveLanguage(uid, headRevComp.Language);
                break;
            case RevolutionaryComponent revComp:
                _languageSystem.RemoveLanguage(uid, revComp.Language);
                break;
        }
    }
}

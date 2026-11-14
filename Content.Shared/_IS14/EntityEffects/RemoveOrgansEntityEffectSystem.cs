using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Whitelist;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>Runs Wizards organ-removal effects against the retained Goob anatomy.</summary>
public sealed partial class RemoveOrgansEntityEffectSystem : EntityEffectSystem<BodyComponent, RemoveOrgans>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<RemoveOrgans> args)
    {
        if (args.Effect.MaxCount is <= 0)
            return;

        var candidates = new List<(EntityUid Uid, string Category)>();
        foreach (var (uid, organ) in _body.GetBodyOrgans(entity.Owner, entity.Comp))
        {
            var category = organ.SlotId.ToLowerInvariant() switch
            {
                "heart" => "Heart", "lungs" => "Lungs", "stomach" => "Stomach",
                "liver" => "Liver", "kidneys" => "Kidneys", "brain" => "Brain",
                _ => organ.SlotId,
            };
            candidates.Add((uid, category));
        }
        foreach (var (uid, part) in _body.GetBodyChildren(entity.Owner, entity.Comp))
        {
            var category = part.PartType == BodyPartType.Hand
                ? "Hand" + part.Symmetry
                : part.PartType.ToString();
            candidates.Add((uid, category));
        }

        var selected = new List<EntityUid>();
        var categories = args.Effect.Categories;
        var excluded = args.Effect.ExcludedCategories;
        foreach (var (uid, category) in candidates)
        {
            if (categories != null && !categories.Contains(category)
                || excluded.Contains(category)
                || !_whitelist.CheckBoth(uid, args.Effect.Blacklist, args.Effect.Whitelist))
                continue;
            selected.Add(uid);
            if (selected.Count == args.Effect.MaxCount)
                break;
        }
        foreach (var uid in selected)
        {
            if (args.Effect.Delete)
            {
                QueueDel(uid);
                continue;
            }
            if (TryComp<OrganComponent>(uid, out var organ))
            {
                if (!_body.RemoveOrgan(uid, organ))
                    continue;
            }
            else if (TryComp<BodyPartComponent>(uid, out var part))
            {
                if (!_body.TryDetachPart(uid, part))
                    continue;
            }
            _transform.AttachToGridOrMap(uid);
        }
    }
}

public sealed partial class RemoveOrgans : EntityEffectBase<RemoveOrgans>
{
    [DataField] public HashSet<string>? Categories;
    [DataField] public HashSet<string> ExcludedCategories = new();
    [DataField] public EntityWhitelist? Whitelist;
    [DataField] public EntityWhitelist? Blacklist;
    [DataField] public bool Delete;
    [DataField] public int? MaxCount;
}

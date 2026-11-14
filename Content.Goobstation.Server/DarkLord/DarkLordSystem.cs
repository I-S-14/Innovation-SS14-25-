using Robust.Shared.Prototypes;
using GameTicker = Content.Server.GameTicking.ServerGameTicker;
using Content.Goobstation.Shared.DarkLord;
using Robust.Shared.Random;
using Content.Server.GameTicking;

namespace Content.Goobstation.Server.DarkLord;

public sealed partial class DarkLordSystem : EntitySystem
{
    //IS14-change start: typed IDs preserve the retained Goob prototypes.
    private static readonly EntProtoId CompatibilityChosenOneMidroundEntityId = "ChosenOneMidround";
    //IS14-change end

    [Dependency] private IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DarkLordComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, DarkLordComponent component, MapInitEvent args)
    {
        if (_random.Prob(component.ChosenOneChance))
        {
            var ticker = _entitySystemManager.GetEntitySystem<GameTicker>();
            ticker.AddGameRule(CompatibilityChosenOneMidroundEntityId);
            ticker.StartGameRule(CompatibilityChosenOneMidroundEntityId);
        }
    }
}

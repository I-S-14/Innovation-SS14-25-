// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chemistry.Components;
using Content.Shared._Funkystation.WallStains; // Goob
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Content.Shared.Chemistry.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Spawners;
using System.Numerics;
using Content.Goobstation.Shared.Chemistry;
using Content.Shared.Vapor;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.EntitySystems
{
    [UsedImplicitly]
    public sealed class VaporSystem : EntitySystem // Goobstation: Made this public instead of internal. Cry about it.
    {
        [Dependency] private ReactiveSystem _reactive = default!;
        [Dependency] private ThrowingSystem _throwing = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        [Dependency] private SharedMapSystem _map = default!;
        [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
        [Dependency] private SharedTransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VaporComponent, StartCollideEvent>(HandleCollide);
        }

        private void HandleCollide(Entity<VaporComponent> entity, ref StartCollideEvent args)
        {
            if (!TryComp(entity.Owner, out SolutionContainerManagerComponent? contents)) return;

            if (!entity.Comp.ReactedEntities.Add(args.OtherEntity)) // Goob check
                return;

            foreach (var (_, soln) in _solutionContainerSystem.EnumerateSolutions((entity.Owner, contents)))
            {
                var solution = soln.Comp.Solution;
                _reactive.DoEntityReaction(args.OtherEntity, solution, ReactionMethod.Touch);

                var ev = new VaporCheckEyeProtectionEvent(); // Goobstation - Start
                RaiseLocalEvent(args.OtherEntity, ev);

                if (!ev.Protected)
                    _reactive.DoEntityReaction(args.OtherEntity, solution, ReactionMethod.Eyes); // Goobstation - End

                if ((args.OtherFixture.CollisionLayer & (int) CollisionGroup.Impassable) != 0 && args.OtherFixture.Hard && solution.Volume > 0) // Goob
                {
                    var wallSplash = new SplashOnWallEvent(Transform(entity.Owner).Coordinates, solution.Clone()); // Goob
                    RaiseLocalEvent(ref wallSplash); // Goob
                }
            }

            // Check for collision with a impassable object (e.g. wall) and stop
            if ((args.OtherFixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0 && args.OtherFixture.Hard)
            {
                QueueDel(entity);
            }
        }

        public void Start(Entity<VaporComponent?> vapor,
            TransformComponent vaporXform,
            Vector2 dir,
            float speed,
            MapCoordinates target,
            float aliveTime,
            EntityUid? user = null)
        {
            if (!Resolve(vapor, ref vapor.Comp))
                return;

            vapor.Comp.Active = true;
            var despawn = EnsureComp<TimedDespawnComponent>(vapor);
            despawn.Lifetime = aliveTime;

            // Set Move
            if (TryComp(vapor, out PhysicsComponent? physics))
            {
                _physics.SetLinearDamping(vapor, physics, 0f);
                _physics.SetAngularDamping(vapor, physics, 0f);

                _throwing.TryThrow(vapor, dir, speed, user: user,
                    predicted: false); // Trauma

                var distance = (target.Position - _transformSystem.GetWorldPosition(vaporXform)).Length();
                var time = (distance / physics.LinearVelocity.Length());
                despawn.Lifetime = MathF.Min(aliveTime, time);
            }
        }

        public bool TryAddSolution(Entity<VaporComponent> vapor, Solution solution) // Goobstation: Made this public instead of internal.
        {
            if (solution.Comp.Solution.Volume <= 0 || split <= 0 || !Resolve(vapor, ref vapor.Comp))
                return false;

            var newSolution = _solutionContainer.SplitSolution(solution, split);

            if (TryComp<AppearanceComponent>(vapor, out var appearance))
            {
                _appearance.SetData(vapor, VaporVisuals.Color, newSolution.GetColor(ProtoMan).WithAlpha(1f), appearance);
                _appearance.SetData(vapor, VaporVisuals.State, true, appearance);
            }

            return _solutionContainer.TryAddSolution((vapor, vapor.Comp), newSolution);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Enumerate over all VaporComponents
            // TODO: Vapor should just use SolutionComponent and not be capable of having multiple solutions.
            var query = EntityQueryEnumerator<VaporComponent, SolutionComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var vaporComp, out var solution, out var xform))
            {
                // Return early if we're not active
                if (!vaporComp.Active)
                    continue;

                // Get the current location of the vapor entity first
                if (TryComp(xform.GridUid, out MapGridComponent? gridComp))
                {
                    var tile = _map.GetTileRef(xform.GridUid.Value, gridComp, xform.Coordinates);

                    // Check if the tile is a tile we've reacted with previously. If so, skip it.
                    // If we have no previous tile reference, we don't return so we can save one.
                    if (vaporComp.PreviousTileRef != null && tile == vaporComp.PreviousTileRef)
                        continue;

                    // Iterate over the reagents in the solution
                    // Reason: Each reagent in our solution may have a unique TileReaction
                    // In this instance, we check individually for each reagent's TileReaction
                    // This is not doing chemical reactions!
                    var contents = solution.Solution;
                    foreach (var reagentQuantity in contents.Contents.ToArray())
                    {
                        // Check if the reagent is empty
                        if (reagentQuantity.Quantity == FixedPoint2.Zero)
                            continue;

                        var reagent = ProtoMan.Index<ReagentPrototype>(reagentQuantity.Reagent.Prototype);

                        // Limit the reaction amount to a minimum value to ensure no floating point funnies.
                        // Ex: A solution with a low percentage transfer amount will slowly approach 0.01... and never get deleted
                        var clampedAmount = Math.Max(
                            (float)reagentQuantity.Quantity * vaporComp.TransferAmountPercentage,
                            vaporComp.MinimumTransferAmount);

                        // Preform the reagent's TileReaction
                        var reaction =
                            reagent.ReactionTile(tile,
                                clampedAmount,
                                EntityManager,
                                reagentQuantity.Reagent.Data);

                        if (reaction > reagentQuantity.Quantity)
                            reaction = reagentQuantity.Quantity;

                        _solutionContainer.RemoveReagent((uid, solution), reagentQuantity.Reagent, reaction);
                    }

                    // Delete the vapor entity if it has no contents
                    if (contents.Volume == 0)
                        QueueDel(uid);


                    // Set the previous tile reference to the current tile
                    vaporComp.PreviousTileRef = tile;
                }
            }

        }
    }
}

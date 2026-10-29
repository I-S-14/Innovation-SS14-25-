// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Stack;
using Content.Server.Stunnable;
using Content.Shared._Shitmed.Body.Events; // Shitmed Change
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems; // Shitmed Change
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared._White.Grab;

namespace Content.Server.Hands.Systems
{
    public sealed partial class HandsSystem : SharedHandsSystem
    {
        [Dependency] private readonly SharedBodySystem _bodySystem = default!; // Shitmed Change

        // Trauma - moved throwing dependencies, query and DropHeldItemsSpread to PredictedHandsSystem

        public override void Initialize()
        {
            base.Initialize();

            // Trauma - moved OnDisarmed to PredictedHandsSystem

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            SubscribeLocalEvent<HandsComponent, BeforeExplodeEvent>(OnExploded);
            SubscribeLocalEvent<HandsComponent, BodyPartEnabledEvent>(HandleBodyPartEnabled); // Shitmed Change
            SubscribeLocalEvent<HandsComponent, BodyPartDisabledEvent>(HandleBodyPartDisabled); // Shitmed Change

            // Trauma - moved OnDropHandItems and HandleThrowItem to PredictedHandsSystem
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            // If we only switch hands don't send a full state.
            if (args.FromTick > hands.CreationTick && hands.LastUnclassifiedDirty >= args.FromTick)
            {
                var aspects = EntityManager.GetModifiedAspects(hands, args.FromTick);

                if (aspects == ActiveHandIdIndex)
                {
                    args.State = new HandsComponentActiveHandDeltaState(hands.ActiveHandId);
                    return;
                }
            }

            // Get full state.
            args.State = new HandsComponentState(
                hands.ActiveHandId,
                hands.Hands,
                hands.SortedHands,
                hands.ShowInHands,
                hands.HandDisplacement,
                hands.LeftHandDisplacement,
                hands.RightHandDisplacement,
                hands.CanBeStripped,
                hands.NextThrowTime); // Trauma
        }


        private void OnExploded(Entity<HandsComponent> ent, ref BeforeExplodeEvent args)
        {
            if (ent.Comp.DisableExplosionRecursion)
                return;

            foreach (var held in EnumerateHeld(ent.AsNullable()))
            {
                args.Contents.Add(held);
            }
        }

        // Trauma - moved OnDisarmed to PredictedHandsSystem

        // Shitmed Change Start
        private void TryAddHand(EntityUid uid, HandsComponent component, Entity<BodyPartComponent> part, string slot)
        {
            if (args.Handled)
                return;

            // Break any pulls
            if (TryComp(uid, out PullerComponent? puller) && TryComp(puller.Pulling, out PullableComponent? pullable))
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);

            var offsetRandomCoordinates = _transformSystem.GetMoverCoordinates(args.Target).Offset(_random.NextVector2(1f, 1.5f));
            if (!ThrowHeldItem(args.Target, offsetRandomCoordinates))
                return;

            args.PopupPrefix = "disarm-action-";

            args.Handled = true; // no shove/stun.
        }

        #region interactions

        // Trauma - moved everything here to PredictedHandsSystem

        #endregion
    }
}

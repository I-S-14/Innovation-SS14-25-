// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Input;
using Robust.Shared.Input;

namespace Content.Client.Input
{
    /// <summary>
    ///     Contains a helper function for setting up all content
    ///     contexts, and modifying existing engine ones.
    /// </summary>
    public static class ContentContexts
    {
        public static void SetupContexts(IInputContextContainer contexts)
        {
            var common = contexts.GetContext("common");
            common.AddFunction(ContentKeyFunctions.FocusChat);
            common.AddFunction(ContentKeyFunctions.FocusLocalChat);
            common.AddFunction(ContentKeyFunctions.FocusEmote);
            common.AddFunction(ContentKeyFunctions.FocusWhisperChat);
            common.AddFunction(ContentKeyFunctions.FocusRadio);
            common.AddFunction(ContentKeyFunctions.FocusLOOC);
            common.AddFunction(ContentKeyFunctions.FocusOOC);
            common.AddFunction(ContentKeyFunctions.FocusAdminChat);
            common.AddFunction(ContentKeyFunctions.FocusConsoleChat);
            common.AddFunction(ContentKeyFunctions.FocusDeadChat);
            common.AddFunction(ContentKeyFunctions.CycleChatChannelForward);
            common.AddFunction(ContentKeyFunctions.CycleChatChannelBackward);
            common.AddFunction(ContentKeyFunctions.EscapeContext);
            common.AddFunction(ContentKeyFunctions.ExamineEntity);
            common.AddFunction(ContentKeyFunctions.OpenAHelp);
            common.AddFunction(ContentKeyFunctions.TakeScreenshot);
            common.AddFunction(ContentKeyFunctions.TakeScreenshotNoUI);
            common.AddFunction(ContentKeyFunctions.ToggleFullscreen);
            common.AddFunction(ContentKeyFunctions.MoveStoredItem);
            common.AddFunction(ContentKeyFunctions.RotateStoredItem);
            common.AddFunction(ContentKeyFunctions.SaveItemLocation);
            common.AddFunction(ContentKeyFunctions.Point);
            common.AddFunction(ContentKeyFunctions.ResistGrab); // Goobstation - Grab Intent
            common.AddFunction(ContentKeyFunctions.VoicePushToTalk); // Goobstation - Voice chat
            common.AddFunction(ContentKeyFunctions.VoicePushToTalkRadio); // Goobstation - Voice chat
            common.AddFunction(ContentKeyFunctions.NamePeek); // Goob
            common.AddFunction(ContentKeyFunctions.ZoomOut);
            common.AddFunction(ContentKeyFunctions.ZoomIn);
            common.AddFunction(ContentKeyFunctions.ResetZoom);
            common.AddFunction(ContentKeyFunctions.InspectEntity);
            common.AddFunction(ContentKeyFunctions.InspectServerComponent);
            common.AddFunction(ContentKeyFunctions.InspectClientComponent);
            common.AddFunction(ContentKeyFunctions.ToggleRoundEndSummaryWindow);

            // Not in engine, because engine cannot check for sanbox/admin status before starting placement.
            common.AddFunction(ContentKeyFunctions.EditorCopyObject);

            // Not in engine because the engine doesn't understand what a flipped object is
            common.AddFunction(ContentKeyFunctions.EditorFlipObject);

            // Not in engine so that the RCD can rotate objects
            common.AddFunction(EngineKeyFunctions.EditorRotateObject);

            // CorvaxGoob-changes-start:
            common.AddFunction(ContentKeyFunctions.EditorNextObject);
            common.AddFunction(ContentKeyFunctions.EditorPreviousObject);
            // CorvaxGoob-changes-end.

            var human = contexts.GetContext("human");
            human.AddFunction(EngineKeyFunctions.MoveUp);
            human.AddFunction(EngineKeyFunctions.MoveDown);
            human.AddFunction(EngineKeyFunctions.MoveLeft);
            human.AddFunction(EngineKeyFunctions.MoveRight);
            human.AddFunction(EngineKeyFunctions.Walk);
            human.AddFunction(ContentKeyFunctions.ToggleKnockdown);
            human.AddFunction(ContentKeyFunctions.Sprint);
            human.AddFunction(ContentKeyFunctions.SwapHands);
            human.AddFunction(ContentKeyFunctions.SwapHandsReverse);
            human.AddFunction(ContentKeyFunctions.Drop);
            human.AddFunction(ContentKeyFunctions.UseItemInHand);
            human.AddFunction(ContentKeyFunctions.AltUseItemInHand);
            human.AddFunction(ContentKeyFunctions.OpenCharacterMenu);
            human.AddFunction(ContentKeyFunctions.OpenEmotesMenu);
            human.AddFunction(ContentKeyFunctions.OpenLanguageMenu); // Einstein Engines - Language
            human.AddFunction(ContentKeyFunctions.ActivateItemInWorld);
            human.AddFunction(ContentKeyFunctions.ThrowItemInHand);
            human.AddFunction(ContentKeyFunctions.AltActivateItemInWorld);
            human.AddFunction(ContentKeyFunctions.TryPullObject);
            human.AddFunction(ContentKeyFunctions.MovePulledObject);
            human.AddFunction(ContentKeyFunctions.ReleasePulledObject);
            human.AddFunction(ContentKeyFunctions.OpenCraftingMenu);
            human.AddFunction(ContentKeyFunctions.OpenInventoryMenu);
            human.AddFunction(ContentKeyFunctions.SmartEquipBackpack);
            human.AddFunction(ContentKeyFunctions.SmartEquipBelt);
            human.AddFunction(ContentKeyFunctions.SmartEquipPocket1);
            human.AddFunction(ContentKeyFunctions.SmartEquipPocket2);
            human.AddFunction(ContentKeyFunctions.SmartEquipSuitStorage);
            human.AddFunction(ContentKeyFunctions.SmartEquipBack); // Goobstation - Smart equip to back
            human.AddFunction(ContentKeyFunctions.OpenBackpack);
            human.AddFunction(ContentKeyFunctions.OpenBelt);
            human.AddFunction(ContentKeyFunctions.RotateObjectClockwise);
            human.AddFunction(ContentKeyFunctions.RotateObjectCounterclockwise);
            human.AddFunction(ContentKeyFunctions.FlipObject);
            human.AddFunction(ContentKeyFunctions.ArcadeUp);
            human.AddFunction(ContentKeyFunctions.ArcadeDown);
            human.AddFunction(ContentKeyFunctions.ArcadeLeft);
            human.AddFunction(ContentKeyFunctions.ArcadeRight);
            human.AddFunction(ContentKeyFunctions.Arcade1);
            human.AddFunction(ContentKeyFunctions.Arcade2);
            human.AddFunction(ContentKeyFunctions.Arcade3);
            human.AddFunction(ContentKeyFunctions.OfferItem); // CorvaxGoob-OfferItem
            // Shitmed Change Start - TODO: Add hands, feet and groin targeting.
            human.AddFunction(ContentKeyFunctions.TargetHead);
            human.AddFunction(ContentKeyFunctions.TargetChest);
            human.AddFunction(ContentKeyFunctions.TargetGroin);
            human.AddFunction(ContentKeyFunctions.TargetLeftArm);
            human.AddFunction(ContentKeyFunctions.TargetLeftHand);
            human.AddFunction(ContentKeyFunctions.TargetRightArm);
            human.AddFunction(ContentKeyFunctions.TargetRightHand);
            human.AddFunction(ContentKeyFunctions.TargetLeftLeg);
            human.AddFunction(ContentKeyFunctions.TargetLeftFoot);
            human.AddFunction(ContentKeyFunctions.TargetRightLeg);
            human.AddFunction(ContentKeyFunctions.TargetRightFoot);
            // Shitmed Change End

            // actions should be common (for ghosts, mobs, etc)
            common.AddFunction(ContentKeyFunctions.OpenActionsMenu);

            foreach (var boundKey in ContentKeyFunctions.GetHotbarBoundKeys())
            {
                common.AddFunction(boundKey);
            }

            common.AddFunction(ContentKeyFunctions.OpenEntitySpawnWindow);
            common.AddFunction(ContentKeyFunctions.OpenSandboxWindow);
            common.AddFunction(ContentKeyFunctions.OpenTileSpawnWindow);
            common.AddFunction(ContentKeyFunctions.OpenDecalSpawnWindow);
            common.AddFunction(ContentKeyFunctions.OpenAdminMenu);
            common.AddFunction(ContentKeyFunctions.OpenGuidebook);

            var aghost = contexts.New("aghost", "common");

            // Key functions shared between human and aghost
            IEnumerable<BoundKeyFunction> sharedKeyFunctions =
            [
                EngineKeyFunctions.MoveUp,
                EngineKeyFunctions.MoveDown,
                EngineKeyFunctions.MoveLeft,
                EngineKeyFunctions.MoveRight,
                EngineKeyFunctions.Walk,
                ContentKeyFunctions.SwapHands,
                ContentKeyFunctions.SwapHandsReverse,
                ContentKeyFunctions.Drop,
                ContentKeyFunctions.UseItemInHand,
                ContentKeyFunctions.AltUseItemInHand,
                ContentKeyFunctions.OpenCharacterMenu,
                ContentKeyFunctions.ActivateItemInWorld,
                ContentKeyFunctions.ThrowItemInHand,
                ContentKeyFunctions.AltActivateItemInWorld,
                ContentKeyFunctions.TryPullObject,
                ContentKeyFunctions.MovePulledObject,
                ContentKeyFunctions.ReleasePulledObject,
                ContentKeyFunctions.OpenCraftingMenu,
                ContentKeyFunctions.OpenInventoryMenu,
                ContentKeyFunctions.SmartEquipBackpack,
                ContentKeyFunctions.SmartEquipBelt,
                ContentKeyFunctions.SmartEquipPocket1,
                ContentKeyFunctions.SmartEquipPocket2,
                ContentKeyFunctions.SmartEquipSuitStorage,
                ContentKeyFunctions.OpenBackpack,
                ContentKeyFunctions.OpenBelt,
                ContentKeyFunctions.RotateObjectClockwise,
                ContentKeyFunctions.RotateObjectCounterclockwise,
                ContentKeyFunctions.FlipObject,
                ContentKeyFunctions.ArcadeUp,
                ContentKeyFunctions.ArcadeDown,
                ContentKeyFunctions.ArcadeLeft,
                ContentKeyFunctions.ArcadeRight,
                ContentKeyFunctions.Arcade1,
                ContentKeyFunctions.Arcade2,
                ContentKeyFunctions.Arcade3
            ];

            foreach (var keyFunction in sharedKeyFunctions)
            {
                human.AddFunction(keyFunction);
                aghost.AddFunction(keyFunction);
            }
            human.AddFunction(ContentKeyFunctions.ToggleKnockdown);
            human.AddFunction(ContentKeyFunctions.OpenEmotesMenu);

            var ghost = contexts.New("ghost", "human");
            ghost.AddFunction(EngineKeyFunctions.MoveUp);
            ghost.AddFunction(EngineKeyFunctions.MoveDown);
            ghost.AddFunction(EngineKeyFunctions.MoveLeft);
            ghost.AddFunction(EngineKeyFunctions.MoveRight);
            ghost.AddFunction(EngineKeyFunctions.Walk);

        }
    }
}

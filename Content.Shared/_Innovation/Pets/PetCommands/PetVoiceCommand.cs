
using Content.Shared._Innovation.Pets.PetCommands;
using Content.Shared.Chat;

public sealed partial class PetVoiceCommand : PetCommand
{
    public override void Execute(PetBaseArgs args)
    {
        args.EntityManager.System<SharedChatSystem>().TrySendInGameICMessage(
            args.PetEntity,
            "Иди нахуй",
            InGameICChatType.Speak,
            false
        );
    }
}

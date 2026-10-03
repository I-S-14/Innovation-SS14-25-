// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaperComponent : Component
{
    public PaperAction Mode;
    [DataField("content"), AutoNetworkedField]
    public string Content { get; set; } = "";

    [DataField("contentSize")]
    public int ContentSize { get; set; } = 10000;

    [DataField("stampedBy"), AutoNetworkedField]
    public List<StampDisplayInfo> StampedBy { get; set; } = new();

    /// <summary>
    ///     Stamp to be displayed on the paper, state from bureaucracy.rsi
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string? StampState { get; set; }

    //IS14-change start: the shared greyscale stamp state is tinted per stamp, so the world sprite
    // matches the ink of the impression instead of needing its own coloured sprite.
    [DataField, AutoNetworkedField]
    public Color StampColor { get; set; } = Color.White;
    //IS14-change end

    [DataField, AutoNetworkedField]
    public bool EditingDisabled;

    //IS14-change start: a signature waiting to be positioned by the player who asked for it.
    // Runtime only and deliberately not networked as a component field; it reaches the one client
    // that needs it through the UI state, which is guaranteed to arrive with the open interface.
    public EntityUid? SignatureRequestedBy;

    public string? SignatureRequestedName;
    //IS14-change end

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly PaperAction Mode;

        //IS14-change start: who may currently place a signature, and how it reads
        public readonly NetEntity? SignatureRequestedBy;
        public readonly string? SignatureRequestedName;
        //IS14-change end

        public PaperBoundUserInterfaceState(string text,
            List<StampDisplayInfo> stampedBy,
            PaperAction mode = PaperAction.Read,
            NetEntity? signatureRequestedBy = null, //IS14-change
            string? signatureRequestedName = null) //IS14-change
        {
            Text = text;
            StampedBy = stampedBy;
            Mode = mode;
            SignatureRequestedBy = signatureRequestedBy; //IS14-change
            SignatureRequestedName = signatureRequestedName; //IS14-change
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputTextMessage : BoundUserInterfaceMessage
    {
        public readonly string Text;

        public PaperInputTextMessage(string text)
        {
            Text = text;
        }
    }

    // Starlight-start
    [Serializable, NetSerializable]
    public sealed class PaperSignatureRequestMessage : BoundUserInterfaceMessage
    {
        public readonly int SignatureIndex;

        public PaperSignatureRequestMessage(int signatureIndex)
        {
            SignatureIndex = signatureIndex;
        }
    }
    // Starlight-end
    [Serializable, NetSerializable]
    public enum PaperUiKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public enum PaperAction
    {
        Read,
        Write,
    }

    [Serializable, NetSerializable]
    public enum PaperVisuals : byte
    {
        Status,
        Stamp,
        StampColor //IS14-change: tint for the shared greyscale stamp state
    }

    [Serializable, NetSerializable]
    public enum PaperStatus : byte
    {
        Blank,
        Written
    }
}
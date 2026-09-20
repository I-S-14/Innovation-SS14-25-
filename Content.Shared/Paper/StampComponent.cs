// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility; //IS14-change: sprite specifier stamp icons

namespace Content.Shared.Paper;

/// <summary>
///     Set of required information to draw a stamp in UIs, where
///     representing the state of the stamp at the point in time
///     when it was applied to a paper. These fields mirror the
///     equivalent in the component.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial struct StampDisplayInfo
{
    StampDisplayInfo(string s)
    {
        StampedName = s;
    }

    [DataField("stampedName")]
    public string StampedName;

    [DataField("stampedColor")]
    public Color StampedColor;

    //IS14-change start: the large icon is a sprite specifier so it can point at an animated RSI state
    [DataField]
    public SpriteSpecifier? StampLargeIcon;
    //IS14-change end

    [DataField]
    public string? StampFont; // goob

    [DataField]
    public bool HasIcon = true; // goob
};

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // CorvaxGoob-ChameleonStamp : добавлено серверно-клиенсткое взаимодействие
public sealed partial class StampComponent : Component
{
    /// <summary>
    ///     The loc string name that will be stamped to the piece of paper on examine.
    /// </summary>
    [DataField("stampedName"), AutoNetworkedField] // CorvaxGoob-ChameleonStamp : AutoNetworkedField
    public string StampedName { get; set; } = "stamp-component-stamped-name-default";

    /// <summary>
    ///     The sprite state of the stamp to display on the paper from paper Sprite path.
    /// </summary>
    [DataField("stampState"), AutoNetworkedField] // CorvaxGoob-ChameleonStamp : AutoNetworkedField
    public string StampState { get; set; } = "paper_stamp-generic";

    /// <summary>
    /// The color of the ink used by the stamp in UIs
    /// </summary>
    [DataField("stampedColor"), AutoNetworkedField] // CorvaxGoob-ChameleonStamp : AutoNetworkedField
    public Color StampedColor = Color.FromHex("#BB3232"); // StyleNano.DangerousRedFore

    /// <summary>
    /// The sound when stamp stamped
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound = null;

    //IS14-change start: the large icon is a sprite specifier so it can point at an animated RSI state
    /// <summary>
    ///     The large icon drawn over the paper when the document is read. Either a bare texture path
    ///     or an RSI state; an animated state is played back frame by frame.
    /// </summary>
    [DataField]
    public SpriteSpecifier? StampLargeIcon = null;
    //IS14-change end

}

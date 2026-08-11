// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Medical.Disease;

/// <summary>
/// Somebody who can develop an IS14 condition. Holds their illnesses as entities.
/// </summary>
/// <remarks>
/// A container and not a list of ids: an illness with its own entity can be inspected in
/// view-variables, spawned by an admin, given live state per stage, and deleted by the same
/// rules as everything else in the game. None of that is free if a disease is a dictionary row.
/// <para>
/// Its own container, separate from Goobstation's virology carrier, so a patient can have one,
/// the other, or both without the two systems ever seeing each other's entities.
/// </para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IS14DiseaseCarrierComponent : Component
{
    public const string ContainerId = "is14-diseases";

    [ViewVariables]
    public Container? Diseases;

    /// <summary>
    /// What an analyser would print: one line per illness, name and stage.
    /// </summary>
    /// <remarks>
    /// Kept as a flat networked summary rather than letting the client read the illness
    /// entities themselves, because entities inside a container are not something a client can
    /// be relied upon to have. A diagnosis is a few short strings; sending them costs nothing
    /// and means the readout works the same whether or not the patient is in view.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public List<IS14Diagnosis> Diagnoses = new();
}

/// <summary>One line of a chart: what the patient has, and how far along it is.</summary>
[Serializable, NetSerializable]
public readonly record struct IS14Diagnosis(LocId Disease, LocId Stage, float Progress);

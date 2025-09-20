using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Content.Shared._IS.Payment;

/// <summary>
/// Key representing which <see cref="PlayerBoundUserInterface"/> is currently open.
/// Useful when there are multiple UI for an object. Here it's future-proofing only.
/// </summary>
[Serializable, NetSerializable]

public enum WithdrawWindowUiKey
{
    Key,
}

/// <summary>
/// Sends from client to server to ask for money withdrawing.
/// </summary>
[Serializable, NetSerializable]
public sealed class WithdrawWindowWithdrawMessage(int value) : BoundUserInterfaceMessage
{
    public int Value { get; } = value;
}

/// <summary>
/// Sends from server to client to update WithdrawWindow UI
/// </summary>
[Serializable, NetSerializable]
public sealed class WithdrawWindowInterfaceState(string personalAccount, int balance) : BoundUserInterfaceState
{
    public string PersonalAccount{ get; } = personalAccount;
    public int Balance { get; } = balance;
}

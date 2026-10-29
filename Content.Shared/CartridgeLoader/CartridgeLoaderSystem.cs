// SPDX-License-Identifier: MIT


/// <summary>
/// Gets sent by the cartridge loader system to the cartridge loader entity so another system
/// can handle displaying the notification
/// </summary>
/// <param name="Message">The message to be displayed</param>
[ByRefEvent]
public record struct CartridgeLoaderNotificationSentEvent(string Header, string Message);

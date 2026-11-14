// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Module;
using System;
using Content.Shared;

namespace Content.Client.IoC
{
    public sealed class ClientModuleTestingCallbacks : SharedModuleTestingCallbacks
    {
        public Action? ClientBeforeIoC { get; set; }
    }
}
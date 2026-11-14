// SPDX-License-Identifier: MIT

using Content.Shared.Humanoid.Markings;
using Content.Shared.Localizations;
using Content.Shared.Tag;
using Content.Shared.Whitelist;

namespace Content.Shared.IoC
{
    public static class SharedContentIoC
    {
        public static void Register(IDependencyCollection deps)
        {
            deps.Register<MarkingManager, MarkingManager>();
            deps.Register<ContentLocalizationManager, ContentLocalizationManager>();
            //IS14-change: interaction verbs resolve entity systems through IEntityManager.
        }
    }
}

// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Maps;
using Content.Shared.Module;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Sandboxing;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Shared.Entry
{
    public sealed partial class EntryPoint : GameShared
    {
        [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
        [Dependency] private readonly IReflectionManager _refMan = default!; // Goobstation - Module Throws
        [Dependency] private readonly ISandboxHelper _sandbox = default!; // Goobstation - Module Throws
        [Dependency] private readonly INetManager _net = default!; // Goobstation - Module Throws
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IResourceManager _resMan = default!;
#if DEBUG
        [Dependency] private IConfigurationManager _configurationManager = default!;
#endif

        private readonly ResPath _ignoreFileDirectory = new("/IgnoredPrototypes/");

        public override void PreInit()
        {
            Dependencies.InjectDependencies(this);

            VerifyModules(); // Goobstation - Module Throws
        }

        public override void Init()
        {
            IgnorePrototypes();
        }

        public override void PostInit()
        {
            base.PostInit();

            Dependencies.Resolve<MarkingManager>().Initialize();

#if DEBUG
            _configurationManager.OverrideDefault(CVars.NetFakeLagMin, 0.075f);
            _configurationManager.OverrideDefault(CVars.NetFakeLoss, 0.005f);
            _configurationManager.OverrideDefault(CVars.NetFakeDuplicates, 0.005f);
#endif
        }

        private void IgnorePrototypes()
        {
            if (!TryReadFile(out var sequences))
                return;

            foreach (var sequence in sequences)
            {
                foreach (var node in sequence.Sequence)
                {
                    var path = new ResPath(((ValueDataNode) node).Value);

                    if (string.IsNullOrEmpty(path.Extension))
                    {
                        _prototypeManager.AbstractDirectory(path);
                    }
                    else
                    {
                        _prototypeManager.AbstractFile(path);
                    }
                }
            }
        }

        private bool TryReadFile([NotNullWhen(true)] out List<SequenceDataNode>? sequence)
        {
            sequence = new();

            foreach (var path in _resMan.ContentFindFiles(_ignoreFileDirectory))
            {
                if (!_resMan.TryContentFileRead(path, out var stream))
                    continue;

                using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
                var documents = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();

                if (documents == null)
                    continue;

                sequence.Add((SequenceDataNode) documents.Root);
            }

            return true;
        }

        // Goobstation - GoobMod Throws Start
        private void VerifyModules()
        {
            var loadedAssemblies = _refMan.Assemblies
                .Select(assembly => assembly.GetName().Name)
                .ToHashSet();

            var packs = _refMan.GetAllChildren<ModulePack>()
                .Select(type => _sandbox.CreateInstance(type))
                .OfType<ModulePack>();

            foreach (var module in packs)
            {
                var missing = module.RequiredAssemblies
                    .Where(req =>
                        (_net.IsClient && req.IsClient || _net.IsServer && req.IsServer) &&
                        !loadedAssemblies.Contains(req.AssemblyName))
                    .ToList();

                if (missing.Count <= 0)
                    continue;

                throw new InvalidOperationException($"Missing required assemblies to build. Try deleting your bin folder, running dotnet clean, and rebuilding the {module.PackName} solution.\nMissing Modules:\n{string.Join("\n", missing.Select(t => t.AssemblyName))}");
            }
        }

        // Goobstation - GoobMod Throws Start End
    }
}

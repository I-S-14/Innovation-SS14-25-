#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Access;
using EconomyEditor;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.UnitTesting.Pool;

namespace Content.YAMLLinter._IS14;

/// <summary>Exports fully resolved production prototypes, without a running game server.</summary>
internal static class EconomyCatalogExport
{
    public static async Task Run(string output, ExternalTestContext context)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var root = FindRoot();
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { NoLoadTestPrototypes = true }, context);
        var catalog = new Catalog { GeneratedAt = DateTimeOffset.UtcNow, Commit = Commit(root) };
        var missing = new HashSet<string>();
        var textures = new HashSet<string>();
        await pair.Server.WaitPost(() =>
        {
            var pm = pair.Server.ResolveDependency<IPrototypeManager>();
            var loc = pair.Server.ResolveDependency<ILocalizationManager>();
            var factory = pair.Server.EntMan.ComponentFactory;
            var english = new Dictionary<string, string>();
            loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            foreach (var proto in pm.EnumeratePrototypes<EntityPrototype>().Where(p => !p.Abstract))
            {
                if (!proto.TryGetComponent(out IS14VendingMachineComponent? vendor, factory)) continue;
                foreach (var tab in vendor.Tabs)
                    english[tab.Name] = loc.TryGetString(tab.Name, out var text) ? text : tab.Name;
            }
            loc.SetCulture(CultureInfo.GetCultureInfo("ru-RU"));
            foreach (var proto in pm.EnumeratePrototypes<EntityPrototype>().Where(p => !p.Abstract).OrderBy(p => p.ID, StringComparer.Ordinal))
            {
                var item = new CatalogItem
                {
                    Id = proto.ID, Name = proto.Name, Description = proto.Description,
                    Hidden = proto.HideSpawnMenu, IsItem = proto.Components.ContainsKey("Item"),
                    Components = proto.Components.Keys.OrderBy(k => k).ToArray(), Parents = proto.Parents ?? Array.Empty<string>(),
                    Category = Category(proto),
                };
                catalog.Items.Add(item);
                if (!proto.TryGetComponent(out IS14VendingMachineComponent? vending, factory)) continue;
                catalog.Vendors.Add(new Vendor
                {
                    Id = proto.ID, Name = proto.Name, RevenueAccount = vending.RevenueAccount?.Id ?? "",
                    TaxPercent = (decimal)vending.TaxPercent,
                    Tabs = vending.Tabs.Select(t => new VendorTab
                    {
                        Name = t.Name,
                        LabelRu = loc.TryGetString(t.Name, out var ru) ? ru : t.Name,
                        LabelEn = english.GetValueOrDefault(t.Name, t.Name),
                        Access = t.Access.Select(a => a.Id).ToList(),
                        Inventory = t.Inventory.Select(Entry).ToList(),
                    }).ToList(),
                    ContrabandInventory = vending.ContrabandInventory.Select(Entry).ToList(),
                });
            }
            catalog.AccessLevels = pm.EnumeratePrototypes<AccessLevelPrototype>().OrderBy(p => p.ID)
                .Select(p => new NamedId { Id = p.ID, Name = p.GetAccessLevelName() }).ToList();
            catalog.Accounts = pm.EnumeratePrototypes<StationAccountPrototype>().OrderBy(p => p.ID)
                .Select(p => new NamedId { Id = p.ID, Name = p.DisplayName }).ToList();
        });
        // Sprite is ignored server-side, so its fields must be resolved by the
        // CLIENT prototype manager. Server mappings can omit inherited RSI paths.
        await pair.Client.WaitPost(() =>
        {
            var pm = pair.Client.ResolveDependency<IPrototypeManager>();
            foreach (var item in catalog.Items)
            {
                if (!pm.TryGetMapping<EntityPrototype>(item.Id, out var mapping) ||
                    !mapping.TryGetValue("components", out var components) || components is not SequenceDataNode seq) continue;
                var maps = seq.Sequence.OfType<MappingDataNode>().ToList();
                var sprite = maps.FirstOrDefault(m => Value(m, "type") == "Icon")
                             ?? maps.FirstOrDefault(m => Value(m, "type") == "Sprite");
                if (sprite == null) continue;
                item.Icon = JsonSerializer.SerializeToElement(ToObject(sprite), EditorJson.Options);
                CopySprite(sprite, root, output, missing, textures);
            }
        });
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { catalog.Items, catalog.Vendors, catalog.AccessLevels, catalog.Accounts }, EditorJson.Options));
            foreach (var texture in textures.OrderBy(p => p, StringComparer.Ordinal))
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(texture));
                hash.AppendData(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "textures", texture))));
            }
            catalog.Version = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()[..16];
        }
        // Replace the manifest only after a complete successful export.
        var temporary = Path.Combine(output, "catalog.json.tmp");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(catalog, EditorJson.Options));
        File.Move(temporary, Path.Combine(output, "catalog.json"), true);
        await File.WriteAllLinesAsync(Path.Combine(output, "missing-textures.txt"), missing.OrderBy(s => s));
        await pair.CleanReturnAsync();
        Console.WriteLine($"Economy catalog: {catalog.Items.Count} entities, {catalog.Vendors.Count} vendors, {missing.Count} missing texture references. Version {catalog.Version}. Output: {output}");
    }

    private static StockEntry Entry(VendingMachineEntry e) => new() { ItemId = e.ItemId.Id, Price = e.Price, Stock = e.Stock };

    private static string Category(EntityPrototype p)
    {
        bool Has(string key) => p.Components.ContainsKey(key);
        if (Has("Clothing")) return "clothing";
        if (Has("Gun") || Has("MeleeWeapon") && !Has("Tool")) return "weapons";
        if (Has("BallisticAmmoProvider") || Has("CartridgeAmmo") || Has("AmmoBox")) return "ammo";
        if (Has("Food") || Has("Drink")) return "food";
        if (Has("Healing") || Has("Hypospray") || Has("Pill") || Has("Syringe")) return "medical";
        if (Has("Tool") || Has("Multitool") || Has("Welder")) return "tools";
        if (Has("Material") || Has("Stack")) return "materials";
        if (Has("Storage")) return "storage";
        return "other";
    }

    private static object ToObject(DataNode node) => node switch
    {
        MappingDataNode map => map.Children.ToDictionary(kv => kv.Key, kv => ToObject(kv.Value)),
        SequenceDataNode seq => seq.Sequence.Select(ToObject).ToArray(),
        ValueDataNode value => value.Value,
        _ => "",
    };

    private static string? Value(MappingDataNode map, string key) => map.TryGetValue(key, out var value) && value is ValueDataNode scalar ? scalar.Value : null;

    private static void CopySprite(MappingDataNode sprite, string root, string output, HashSet<string> missing, HashSet<string> textures)
    {
        var rsi = Value(sprite, "sprite");
        var layers = new List<MappingDataNode> { sprite };
        if (sprite.TryGetValue("layers", out var node) && node is SequenceDataNode seq) layers.AddRange(seq.Sequence.OfType<MappingDataNode>());
        foreach (var layer in layers)
        {
            var texture = Value(layer, "texture");
            if (texture != null) Copy(texture);
            var path = Value(layer, "sprite") ?? rsi;
            if (path == null) continue;
            if (!path.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase)) { Copy(path); continue; }
            Copy(path.TrimEnd('/') + "/meta.json");
            var state = Value(layer, "state");
            if (state == null)
            {
                var metaPath = Path.Combine(root, "Resources", "Textures", path, "meta.json");
                if (File.Exists(metaPath))
                {
                    using var meta = JsonDocument.Parse(File.ReadAllText(metaPath));
                    state = meta.RootElement.GetProperty("states").EnumerateArray().FirstOrDefault().GetProperty("name").GetString();
                }
            }
            if (state != null) Copy(path.TrimEnd('/') + "/" + state + ".png");
        }

        void Copy(string relative)
        {
            relative = relative.Replace('\\', '/').TrimStart('/');
            if (relative.StartsWith("Textures/")) relative = relative[9..];
            if (relative.Split('/').Any(part => part == "..") || Path.IsPathRooted(relative)) return;
            var src = Path.Combine(root, "Resources", "Textures", relative);
            if (!File.Exists(src)) src = Path.Combine(root, "RobustToolbox", "Resources", "Textures", relative);
            if (!File.Exists(src)) { missing.Add(relative); return; }
            textures.Add(relative);
            var dst = Path.Combine(output, "textures", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst)) File.Copy(src, dst, true);
        }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "Resources", "Prototypes"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run the catalog exporter from the game repository.");
    }

    private static string Commit(string root)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, CreateNoWindow = true });
            var result = process!.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return result;
        }
        catch { return "unknown"; }
    }
}

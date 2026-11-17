using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace EconomyEditor;

public static class Export
{
    // JSON quoted strings are also valid YAML scalars.
    private static string Q(string s) => JsonSerializer.Serialize(s);

    public static byte[] Zip(Project project)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            Write("project.json", JsonSerializer.Serialize(project, EditorJson.Options));
            var ru = new SortedDictionary<string, string>();
            var en = new SortedDictionary<string, string>();
            foreach (var v in project.Vendors)
            {
                var yaml = new StringBuilder("- type: IS14VendingMachine\n");
                yaml.AppendLine($"  revenueAccount: {(v.RevenueAccount == "" ? "null" : Q(v.RevenueAccount))}");
                yaml.AppendLine($"  taxPercent: {v.TaxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                yaml.AppendLine(v.Tabs.Count == 0 ? "  tabs: []" : "  tabs:");
                foreach (var tab in v.Tabs)
                {
                    yaml.AppendLine($"  - name: {Q(tab.Name)}");
                    yaml.AppendLine($"    access: [{string.Join(", ", tab.Access.Select(Q))}]");
                    Stock(yaml, "    inventory", tab.Inventory);
                    ru[tab.Name] = tab.LabelRu;
                    en[tab.Name] = tab.LabelEn;
                }
                Stock(yaml, "  contrabandInventory", v.ContrabandInventory);
                Write($"components/{v.Id}.yml", yaml.ToString());
            }
            Write("ru-RU.ftl", string.Join("\n", ru.Select(kv => $"{kv.Key} = {kv.Value}")) + "\n");
            Write("en-US.ftl", string.Join("\n", en.Select(kv => $"{kv.Key} = {kv.Value}")) + "\n");
            Write("README.txt", "IS14 Economy Editor export\n\ncomponents/*.yml are component fragments, not entity prototypes.\nDo not add them directly to Resources/Prototypes.\n\nExtract this archive, then run from the repository root:\npython Tools/EconomyEditor/scripts/apply_project.py <extracted>/project.json\nReview the diff, then repeat with --apply.\n\nThis updates only the configured assortment fields and Fluent tab labels.\n");

            void Write(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private static void Stock(StringBuilder yaml, string field, List<StockEntry> entries)
    {
        yaml.AppendLine(field + (entries.Count == 0 ? ": []" : ":"));
        var indent = new string(' ', field.TakeWhile(c => c == ' ').Count());
        foreach (var e in entries)
        {
            yaml.AppendLine($"{indent}- itemId: {Q(e.ItemId)}");
            yaml.AppendLine($"{indent}  price: {e.Price}");
            yaml.AppendLine($"{indent}  stock: {e.Stock}");
        }
    }
}

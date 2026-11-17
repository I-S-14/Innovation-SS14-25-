using System.Text.RegularExpressions;

namespace EconomyEditor;

public static partial class Validation
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,150}$")]
    private static partial Regex Identifier();
    [GeneratedRegex("^[a-z][a-z0-9-]{0,150}$")]
    private static partial Regex LocKey();

    public static List<string> Check(Project p, Catalog catalog, bool export)
    {
        var errors = new List<string>();
        if (p.Title is null || p.Author is null || p.Notes is null || p.CatalogVersion is null || p.Vendors is null || p.Vendors.Any(v =>
                v is null || v.Id is null || v.Name is null || v.RevenueAccount is null || v.Tabs is null || v.ContrabandInventory is null ||
                v.Tabs.Any(t => t is null || t.Name is null || t.LabelRu is null || t.LabelEn is null || t.Access is null || t.Access.Any(a => a is null) || t.Inventory is null || t.Inventory.Any(e => e is null || e.ItemId is null)) ||
                v.ContrabandInventory.Any(e => e is null || e.ItemId is null)))
            return new List<string> { "Некорректная структура проекта: обязательные поля не могут быть null." };
        if (p.SchemaVersion != 1) errors.Add("Неподдерживаемая версия проекта.");
        if (string.IsNullOrWhiteSpace(p.Title) || p.Title.Length > 150) errors.Add("Название проекта: от 1 до 150 символов.");
        if (p.Author.Length > 150 || p.Notes.Length > 20000) errors.Add("Слишком длинное имя автора или описание.");
        if (p.Vendors.Count is 0 or > 100) errors.Add("Проект должен содержать от 1 до 100 автоматов.");
        var vendors = catalog.Vendors.Select(v => v.Id).ToHashSet();
        var items = catalog.Items.Select(i => i.Id).ToHashSet();
        var access = catalog.AccessLevels.Select(a => a.Id).ToHashSet();
        var accounts = catalog.Accounts.Select(a => a.Id).ToHashSet();
        var seen = new HashSet<string>();
        var labels = new Dictionary<string, (string, string)>();
        foreach (var v in p.Vendors)
        {
            if (!Identifier().IsMatch(v.Id) || !seen.Add(v.Id)) errors.Add($"Повторный или некорректный ID автомата: {v.Id}");
            if (export && !vendors.Contains(v.Id)) errors.Add($"Автомат отсутствует в текущем каталоге: {v.Id}");
            if (v.TaxPercent is < 0 or > 1) errors.Add($"{v.Id}: налог должен быть от 0 до 100%.");
            if (v.RevenueAccount != "" && !accounts.Contains(v.RevenueAccount)) errors.Add($"{v.Id}: неизвестный счёт {v.RevenueAccount}");
            if (v.Tabs.Count > 40) errors.Add($"{v.Id}: слишком много вкладок.");
            foreach (var tab in v.Tabs)
            {
                if (!LocKey().IsMatch(tab.Name)) errors.Add($"{v.Id}: название вкладки должно быть ключом локализации: {tab.Name}");
                if (new[] { tab.LabelRu, tab.LabelEn }.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 150 || s.Contains('\n') || s.Contains('\r') || s.Contains('{') || s.Contains('}')))
                    errors.Add($"{v.Id}: заполните русское и английское название вкладки (одна строка без фигурных скобок).");
                if (labels.TryGetValue(tab.Name, out var previous) && previous != (tab.LabelRu, tab.LabelEn))
                    errors.Add($"Разные переводы одного ключа вкладки: {tab.Name}");
                labels[tab.Name] = (tab.LabelRu, tab.LabelEn);
                foreach (var a in tab.Access)
                    if (!access.Contains(a)) errors.Add($"{v.Id}: неизвестный доступ {a}");
                CheckStock(v.Id, tab.Inventory);
            }
            CheckStock(v.Id, v.ContrabandInventory);
        }
        return errors.Distinct().Take(100).ToList();

        void CheckStock(string vendor, List<StockEntry> inventory)
        {
            if (inventory.Count > 2000) errors.Add($"{vendor}: слишком много товаров.");
            var ids = new HashSet<string>();
            foreach (var e in inventory)
            {
                if (!Identifier().IsMatch(e.ItemId) || !ids.Add(e.ItemId)) errors.Add($"{vendor}: некорректный или повторный товар {e.ItemId}");
                if (export && !items.Contains(e.ItemId)) errors.Add($"{vendor}: предмет удалён из каталога: {e.ItemId}");
                if (e.Price is < 0 or > 100000000 || e.Stock is < 0 or > 1000000) errors.Add($"{vendor}: цена или запас вне допустимого диапазона: {e.ItemId}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EconomyEditor;

public static class EditorJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public sealed class Catalog
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public string Commit { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
    public string Locale { get; set; } = "ru-RU";
    public List<CatalogItem> Items { get; set; } = new();
    public List<Vendor> Vendors { get; set; } = new();
    public List<NamedId> AccessLevels { get; set; } = new();
    public List<NamedId> Accounts { get; set; } = new();
}

public sealed class NamedId
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CatalogItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Hidden { get; set; }
    public bool IsItem { get; set; }
    public string Category { get; set; } = "other";
    public string[] Components { get; set; } = Array.Empty<string>();
    public string[] Parents { get; set; } = Array.Empty<string>();
    // Resolved Sprite/Icon component mappings. The browser composes static first frames.
    public JsonElement? Icon { get; set; }
}

public sealed class Vendor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RevenueAccount { get; set; } = "";
    public decimal TaxPercent { get; set; } = 0.1m;
    public List<VendorTab> Tabs { get; set; } = new();
    public List<StockEntry> ContrabandInventory { get; set; } = new();
}

public sealed class VendorTab
{
    public string Name { get; set; } = "";
    public string LabelRu { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public List<string> Access { get; set; } = new();
    public List<StockEntry> Inventory { get; set; } = new();
}

public sealed class StockEntry
{
    public string ItemId { get; set; } = "";
    public int Price { get; set; }
    public int Stock { get; set; }
}

public sealed class Project
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Notes { get; set; } = "";
    public string CatalogVersion { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Vendor> Vendors { get; set; } = new();
}

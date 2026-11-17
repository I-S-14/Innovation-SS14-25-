using System.Text.Json;
using EconomyEditor;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;

if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync(Environment.GetEnvironmentVariable("EDITOR_HEALTHCHECK_URL") ?? "http://127.0.0.1:8080/health");
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Environment.ExitCode = payload.RootElement.GetProperty("status").GetString() == "ok" ? 0 : 1;
    }
    catch { Environment.ExitCode = 1; }
    return;
}

var contentRoot = new[] { Environment.CurrentDirectory, Path.Combine(Environment.CurrentDirectory, "Tools/EconomyEditor"), AppContext.BaseDirectory }
    .FirstOrDefault(p => Directory.Exists(Path.Combine(p, "wwwroot"))) ?? Environment.CurrentDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });
if (builder.Configuration["urls"] is null) builder.WebHost.UseUrls("http://127.0.0.1:5187");
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddResponseCompression();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8 * 1024 * 1024);
var catalogDirectory = Path.GetFullPath(builder.Configuration["catalog"] ?? Path.Combine(builder.Environment.ContentRootPath, "data/catalog"));
var catalogPath = Path.Combine(catalogDirectory, "catalog.json");
if (!File.Exists(catalogPath))
    throw new InvalidOperationException($"Catalog missing: {catalogPath}. Run Tools/EconomyEditor/scripts/export_catalog.ps1 first (see README.md).");
var catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(catalogPath), EditorJson.Options)!;
if (catalog.SchemaVersion != 1 || catalog.Items.Count == 0) throw new InvalidOperationException("Invalid or empty economy catalog.");
var store = new ProjectStore(Path.GetFullPath(builder.Configuration["data"] ?? Path.Combine(builder.Environment.ContentRootPath, "data/projects")));
var token = builder.Configuration["EDITOR_TOKEN"] ?? "";
var app = builder.Build();
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: blob:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (token.Length > 0 && context.Request.Headers["X-Editor-Token"] != token)
        { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Нужен ключ доступа к редактору." }); return; }
        // Require same-origin browser writes; command-line clients omit Origin.
        if (context.Request.Method is "POST" or "PUT" && context.Request.Headers.Origin.FirstOrDefault() is { } origin
            && origin != $"{context.Request.Scheme}://{context.Request.Host}")
        { context.Response.StatusCode = 403; return; }
    }
    try { await next(); }
    catch (BadHttpRequestException e) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = e.Message }); }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(catalogDirectory), RequestPath = "/snapshot" });
app.MapGet("/api/catalog", () => catalog);
app.MapGet("/api/projects", () => store.List());
app.MapGet("/api/projects/{id}", (string id) => store.Get(id) is { } p ? Results.Ok(p) : Results.NotFound());
app.MapGet("/api/projects/{id}/history", (string id) => store.History(id));
app.MapGet("/api/projects/{id}/history/{revision:int}", (string id, int revision) => store.Get(id, revision) is { } p ? Results.Ok(p) : Results.NotFound());
app.MapPost("/api/projects", (Project p) => Save(p, true));
app.MapPut("/api/projects/{id}", (string id, Project p) => { p.Id = id; return Save(p, false); });
app.MapPost("/api/validate", (Project p, bool? draft) => Results.Ok(new { errors = Validation.Check(p, catalog, draft != true), stale = p.CatalogVersion != catalog.Version }));
app.MapPost("/api/export", (Project p) =>
{
    var errors = Validation.Check(p, catalog, true);
    return errors.Count > 0 ? Results.BadRequest(new { errors }) : Results.File(Export.Zip(p), "application/zip", "is14-assortment.zip");
});
app.MapGet("/health", () => new { status = "ok", catalogVersion = catalog.Version, catalogCommit = catalog.Commit, items = catalog.Items.Count, vendors = catalog.Vendors.Count });
app.Run();

IResult Save(Project p, bool create)
{
    var errors = Validation.Check(p, catalog, false);
    if (errors.Count > 0) return Results.BadRequest(new { errors });
    if (!create && store.Get(p.Id) is null) return Results.NotFound();
    var saved = store.Save(p, create);
    return saved is null ? Results.Conflict(new { error = "Проект уже изменён другим редактором. Сохраните свою работу как новый проект или откройте актуальную версию." }) : Results.Ok(saved);
}

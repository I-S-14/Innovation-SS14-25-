using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EconomyEditor;

public sealed class ProjectStore
{
    private readonly string _connection;

    public ProjectStore(string directory)
    {
        Directory.CreateDirectory(directory);
        SQLitePCL.Batteries_V2.Init();
        _connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "projects.sqlite3") }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS projects(id TEXT PRIMARY KEY, revision INTEGER NOT NULL, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS history(id TEXT NOT NULL, revision INTEGER NOT NULL, payload TEXT NOT NULL,
              PRIMARY KEY(id,revision));
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connection);
        db.Open();
        return db;
    }

    public Project? Get(string id, int? revision = null)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = revision.HasValue ? "SELECT payload FROM history WHERE id=$id AND revision=$r" : "SELECT payload FROM projects WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        if (revision.HasValue) cmd.Parameters.AddWithValue("$r", revision.Value);
        return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Project>(json, EditorJson.Options) : null;
    }

    public object[] List()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT payload FROM projects ORDER BY json_extract(payload,'$.updatedAt') DESC";
        using var reader = cmd.ExecuteReader();
        var result = new List<object>();
        while (reader.Read())
        {
            var p = JsonSerializer.Deserialize<Project>(reader.GetString(0), EditorJson.Options)!;
            result.Add(new { p.Id, p.Title, p.Author, p.Revision, p.UpdatedAt, p.CatalogVersion });
        }
        return result.ToArray();
    }

    public int[] History(string id)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT revision FROM history WHERE id=$id ORDER BY revision DESC";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        var revisions = new List<int>();
        while (reader.Read()) revisions.Add(reader.GetInt32(0));
        return revisions.ToArray();
    }

    public Project? Save(Project project, bool create)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        var expected = project.Revision;
        if (create) { project.Id = Guid.NewGuid().ToString("N"); project.Revision = 1; }
        else project.Revision++;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(project, EditorJson.Options);
        cmd.CommandText = create
            ? "INSERT INTO projects(id,revision,payload) VALUES($id,$r,$p)"
            : "UPDATE projects SET revision=$r,payload=$p WHERE id=$id AND revision=$expected";
        cmd.Parameters.AddWithValue("$id", project.Id);
        cmd.Parameters.AddWithValue("$r", project.Revision);
        cmd.Parameters.AddWithValue("$p", json);
        if (!create) cmd.Parameters.AddWithValue("$expected", expected);
        if (cmd.ExecuteNonQuery() != 1) return null;
        cmd.CommandText = "INSERT INTO history(id,revision,payload) VALUES($id,$r,$p)";
        cmd.ExecuteNonQuery();
        tx.Commit();
        return project;
    }
}

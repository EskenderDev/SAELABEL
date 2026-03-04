using Microsoft.Data.Sqlite;
using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public sealed class LogicalPrinterStore : ILogicalPrinterStore
{
    private readonly string _connectionString;
    private readonly object _sync = new();

    public LogicalPrinterStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "editor.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureSchema();
    }

    // ── Mapper ─────────────────────────────────────────────────────
    private static LogicalPrinterDto Map(SqliteDataReader r) => new()
    {
        Id             = r.GetString(0),
        Name           = r.GetString(1),
        Description    = r.IsDBNull(2) ? null : r.GetString(2),
        PhysicalPrinter = r.GetString(3),
        IsActive       = r.GetInt32(4) == 1,
        Copies         = r.IsDBNull(5) ? 1  : r.GetInt32(5),
        PaperWidth     = r.IsDBNull(6) ? 80 : r.GetInt32(6),
        MediaType      = r.IsDBNull(7) ? "receipt" : r.GetString(7)
    };

    private const string Cols =
        "id, name, description, physical_printer, is_active, copies, paper_width, media_type";

    // ── CRUD ───────────────────────────────────────────────────────
    public IReadOnlyList<LogicalPrinterDto> GetAll()
    {
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT {Cols} FROM editor_logical_printers ORDER BY name COLLATE NOCASE;";
            using var r = cmd.ExecuteReader();
            var list = new List<LogicalPrinterDto>();
            while (r.Read()) list.Add(Map(r));
            return list;
        }
    }

    public LogicalPrinterDto? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT {Cols} FROM editor_logical_printers WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Trim());
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }
    }

    public LogicalPrinterDto? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT {Cols} FROM editor_logical_printers WHERE name = $name COLLATE NOCASE LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", name.Trim());
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }
    }

    public LogicalPrinterDto Upsert(UpsertLogicalPrinterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidDataException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.PhysicalPrinter)) throw new InvalidDataException("PhysicalPrinter is required.");

        var copies     = Math.Max(1, request.Copies);
        var paperWidth = request.PaperWidth is 58 or 80 ? request.PaperWidth : 80;
        var mediaType  = request.MediaType?.Trim().ToLowerInvariant() == "label" ? "label" : "receipt";

        lock (_sync)
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim();
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_logical_printers
                    (id, name, description, physical_printer, is_active, copies, paper_width, media_type)
                VALUES ($id, $name, $desc, $physical, $active, $copies, $pw, $mt)
                ON CONFLICT(id) DO UPDATE SET
                    name             = excluded.name,
                    description      = excluded.description,
                    physical_printer = excluded.physical_printer,
                    is_active        = excluded.is_active,
                    copies           = excluded.copies,
                    paper_width      = excluded.paper_width,
                    media_type       = excluded.media_type;
                """;
            cmd.Parameters.AddWithValue("$id",       id);
            cmd.Parameters.AddWithValue("$name",     request.Name.Trim());
            cmd.Parameters.AddWithValue("$desc",     request.Description?.Trim() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$physical", request.PhysicalPrinter.Trim());
            cmd.Parameters.AddWithValue("$active",   request.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("$copies",   copies);
            cmd.Parameters.AddWithValue("$pw",       paperWidth);
            cmd.Parameters.AddWithValue("$mt",       mediaType);
            cmd.ExecuteNonQuery();

            return new LogicalPrinterDto
            {
                Id = id, Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                PhysicalPrinter = request.PhysicalPrinter.Trim(),
                IsActive = request.IsActive,
                Copies = copies, PaperWidth = paperWidth, MediaType = mediaType
            };
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_sync)
        {
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM editor_logical_printers WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Trim());
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // ── Infrastructure ─────────────────────────────────────────────
    private SqliteConnection Open()
    {
        var cn = new SqliteConnection(_connectionString);
        cn.Open();
        return cn;
    }

    private void EnsureSchema()
    {
        lock (_sync)
        {
            using var cn = Open();
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS editor_logical_printers (
                        id               TEXT PRIMARY KEY,
                        name             TEXT NOT NULL,
                        description      TEXT,
                        physical_printer TEXT NOT NULL,
                        is_active        INTEGER NOT NULL DEFAULT 1
                    );
                    """;
                cmd.ExecuteNonQuery();
            }
            // Additive migration — safe with existing databases
            AddColIfMissing(cn, "editor_logical_printers", "copies",      "INTEGER NOT NULL DEFAULT 1");
            AddColIfMissing(cn, "editor_logical_printers", "paper_width", "INTEGER NOT NULL DEFAULT 80");
            AddColIfMissing(cn, "editor_logical_printers", "media_type",  "TEXT NOT NULL DEFAULT 'receipt'");
        }
    }

    private static void AddColIfMissing(SqliteConnection cn, string table, string col, string def)
    {
        using var chk = cn.CreateCommand();
        chk.CommandText = $"PRAGMA table_info({table});";
        using var r = chk.ExecuteReader();
        while (r.Read())
            if (r.GetString(1).Equals(col, StringComparison.OrdinalIgnoreCase)) return;
        using var alt = cn.CreateCommand();
        alt.CommandText = $"ALTER TABLE {table} ADD COLUMN {col} {def};";
        alt.ExecuteNonQuery();
    }
}

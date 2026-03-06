using Microsoft.Data.Sqlite;
using System.Text.Json;
using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public sealed class LogicalPrinterStore : ILogicalPrinterStore
{
    private readonly string _connectionString;
    private readonly object _sync = new();

    public LogicalPrinterStore()
    {
        // Usar AppContext.BaseDirectory para que funcione tanto en debug como en servicio
        var dir = Path.Combine(AppContext.BaseDirectory, "App_Data");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "editor.db");
        _connectionString = $"Data Source={dbPath}";
        
        try 
        {
            EnsureSchema();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogicalPrinterStore] Error initializing schema: {ex.Message}");
        }
    }

    // ── Mapper ─────────────────────────────────────────────────────
    private static LogicalPrinterDto Map(SqliteDataReader r)
    {
        var rawPrinters = r.GetString(3);
        List<PhysicalPrinterConfig> printers;

        if (string.IsNullOrWhiteSpace(rawPrinters))
        {
            printers = new();
        }
        else if (rawPrinters.TrimStart().StartsWith("["))
        {
            try
            {
                printers = JsonSerializer.Deserialize<List<PhysicalPrinterConfig>>(rawPrinters) ?? new();
            }
            catch
            {
                // Fallback for malformed JSON
                printers = new List<PhysicalPrinterConfig> { new() { Name = rawPrinters } };
            }
        }
        else
        {
            // Legacy plain text printer name
            printers = new List<PhysicalPrinterConfig> { new() { Name = rawPrinters } };
        }

        return new LogicalPrinterDto
        {
            Id             = r.GetString(0),
            Name           = r.GetString(1),
            Description    = r.IsDBNull(2) ? null : r.GetString(2),
            Printers       = printers,
            IsActive       = r.GetInt32(4) == 1,
            Copies         = r.IsDBNull(5) ? 1  : r.GetInt32(5),
            PaperWidth     = r.IsDBNull(6) || r.GetInt32(6) == 0 ? null : r.GetInt32(6),
            PaperHeight    = r.IsDBNull(7) || r.GetInt32(7) == 0 ? null : r.GetInt32(7),
            MediaType      = r.IsDBNull(8) ? "receipt" : r.GetString(8)
        };
    }

    private const string Cols =
        "id, name, description, physical_printer, is_active, copies, paper_width, paper_height, media_type";

    // ── CRUD ───────────────────────────────────────────────────────
    public IReadOnlyList<LogicalPrinterDto> GetAll()
    {
        try 
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
        catch (Exception ex)
        {
            throw new Exception($"Error al listar impresoras lógicas: {ex.Message}", ex);
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
        if (request.Printers == null || request.Printers.Count == 0) throw new InvalidDataException("At least one printer is required.");

        var copies     = Math.Max(1, request.Copies);
        var paperWidth = request.PaperWidth; // Permitir cualquier valor o null
        var paperHeight = request.PaperHeight; // Altura para escalado
        var mediaType  = request.MediaType?.Trim().ToLowerInvariant() == "label" ? "label" : "receipt";

        lock (_sync)
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim();
            using var cn  = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_logical_printers
                    (id, name, description, physical_printer, is_active, copies, paper_width, paper_height, media_type)
                VALUES ($id, $name, $desc, $physical, $active, $copies, $pw, $ph, $mt)
                ON CONFLICT(id) DO UPDATE SET
                    name             = excluded.name,
                    description      = excluded.description,
                    physical_printer = excluded.physical_printer,
                    is_active        = excluded.is_active,
                    copies           = excluded.copies,
                    paper_width      = excluded.paper_width,
                    paper_height     = excluded.paper_height,
                    media_type       = excluded.media_type;
                """;
            cmd.Parameters.AddWithValue("$id",       id);
            cmd.Parameters.AddWithValue("$name",     request.Name.Trim());
            cmd.Parameters.AddWithValue("$desc",     request.Description?.Trim() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$physical", JsonSerializer.Serialize(request.Printers));
            cmd.Parameters.AddWithValue("$active",   request.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("$copies",   copies);
            cmd.Parameters.AddWithValue("$pw",       paperWidth ?? 0);
            cmd.Parameters.AddWithValue("$ph",       paperHeight ?? 0);
            cmd.Parameters.AddWithValue("$mt",       mediaType);
            cmd.ExecuteNonQuery();

            return new LogicalPrinterDto
            {
                Id = id, Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Printers = request.Printers,
                IsActive = request.IsActive,
                Copies = copies, PaperWidth = paperWidth, PaperHeight = paperHeight, MediaType = mediaType
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
            AddColIfMissing(cn, "editor_logical_printers", "paper_height","INTEGER NOT NULL DEFAULT 0");
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

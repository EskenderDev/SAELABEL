using Microsoft.Data.Sqlite;
using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public sealed class EditorLibraryStore : IEditorLibraryStore
{
    private readonly string _connectionString;
    private readonly object _sync = new();

    public EditorLibraryStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "editor.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureSchema();
        SeedDefaultsIfEmpty();
    }

    public IReadOnlyList<EditorElementDto> GetElements()
    {
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, key, name, category, object_type, default_width_pt, default_height_pt, default_content
                FROM editor_elements
                ORDER BY category COLLATE NOCASE, name COLLATE NOCASE;
                """;
            using var r = cmd.ExecuteReader();
            var list = new List<EditorElementDto>();
            while (r.Read())
            {
                list.Add(new EditorElementDto
                {
                    Id = r.GetString(0),
                    Key = r.GetString(1),
                    Name = r.GetString(2),
                    Category = r.GetString(3),
                    ObjectType = r.GetString(4),
                    DefaultWidthPt = r.GetDouble(5),
                    DefaultHeightPt = r.GetDouble(6),
                    DefaultContent = r.GetString(7)
                });
            }
            return list;
        }
    }

    public EditorElementDto UpsertElement(UpsertEditorElementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key)) throw new InvalidDataException("Key es requerido.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidDataException("Name es requerido.");

        lock (_sync)
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim();
            var key = request.Key.Trim();
            var name = request.Name.Trim();
            var category = string.IsNullOrWhiteSpace(request.Category) ? "basic" : request.Category.Trim();
            var objectType = NormalizeObjectType(request.ObjectType);
            var w = request.DefaultWidthPt <= 0 ? 80 : request.DefaultWidthPt;
            var h = request.DefaultHeightPt <= 0 ? 24 : request.DefaultHeightPt;
            var content = request.DefaultContent ?? string.Empty;

            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_elements (id, key, name, category, object_type, default_width_pt, default_height_pt, default_content)
                VALUES ($id, $key, $name, $category, $type, $w, $h, $content)
                ON CONFLICT(id) DO UPDATE SET
                    key = excluded.key,
                    name = excluded.name,
                    category = excluded.category,
                    object_type = excluded.object_type,
                    default_width_pt = excluded.default_width_pt,
                    default_height_pt = excluded.default_height_pt,
                    default_content = excluded.default_content;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$category", category);
            cmd.Parameters.AddWithValue("$type", objectType);
            cmd.Parameters.AddWithValue("$w", w);
            cmd.Parameters.AddWithValue("$h", h);
            cmd.Parameters.AddWithValue("$content", content);
            cmd.ExecuteNonQuery();

            return new EditorElementDto
            {
                Id = id,
                Key = key,
                Name = name,
                Category = category,
                ObjectType = objectType,
                DefaultWidthPt = w,
                DefaultHeightPt = h,
                DefaultContent = content
            };
        }
    }

    public bool DeleteElement(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM editor_elements WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Trim());
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public IReadOnlyList<EditorDocumentSummaryDto> GetDocuments()
    {
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, name, kind, updated_at_utc
                FROM editor_documents
                ORDER BY updated_at_utc DESC;
                """;
            using var r = cmd.ExecuteReader();
            var list = new List<EditorDocumentSummaryDto>();
            while (r.Read())
            {
                list.Add(new EditorDocumentSummaryDto
                {
                    Id = r.GetString(0),
                    Name = r.GetString(1),
                    Kind = r.GetString(2),
                    UpdatedAtUtc = DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
            return list;
        }
    }

    public EditorDocumentDto? GetDocument(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, name, kind, xml, created_at_utc, updated_at_utc
                FROM editor_documents
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", id.Trim());
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new EditorDocumentDto
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Kind = r.GetString(2),
                Xml = r.GetString(3),
                CreatedAtUtc = DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }
    }

    public EditorDocumentDto? GetDocumentByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, name, kind, xml, created_at_utc, updated_at_utc
                FROM editor_documents
                WHERE name = $name
                ORDER BY updated_at_utc DESC
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$name", name.Trim());
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new EditorDocumentDto
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Kind = r.GetString(2),
                Xml = r.GetString(3),
                CreatedAtUtc = DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }
    }


    public EditorDocumentDto UpsertDocument(UpsertEditorDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidDataException("Name es requerido.");
        if (string.IsNullOrWhiteSpace(request.Xml)) throw new InvalidDataException("Xml es requerido.");

        lock (_sync)
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim();
            var name = request.Name.Trim();
            var now = DateTime.UtcNow;
            var kind = NormalizeKind(request.Kind);

            using var cn = Open();
            
            using (var checkCmd = cn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT id FROM editor_documents WHERE name = @name AND id <> @id LIMIT 1;";
                checkCmd.Parameters.AddWithValue("@name", name);
                checkCmd.Parameters.AddWithValue("@id", id);
                var existingId = checkCmd.ExecuteScalar();
                if (existingId != null)
                {
                    throw new InvalidDataException($"Ya existe un documento con el nombre '{name}'.");
                }
            }
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_documents (id, name, kind, xml, created_at_utc, updated_at_utc)
                VALUES ($id, $name, $kind, $xml, $created, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    kind = excluded.kind,
                    xml = excluded.xml,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$name", request.Name.Trim());
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$xml", request.Xml);
            cmd.Parameters.AddWithValue("$created", now.ToString("O"));
            cmd.Parameters.AddWithValue("$updated", now.ToString("O"));
            cmd.ExecuteNonQuery();

            using var read = cn.CreateCommand();
            read.CommandText = """
                SELECT id, name, kind, xml, created_at_utc, updated_at_utc
                FROM editor_documents
                WHERE id = $id;
                """;
            read.Parameters.AddWithValue("$id", id);
            using var r = read.ExecuteReader();
            r.Read();
            return new EditorDocumentDto
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Kind = r.GetString(2),
                Xml = r.GetString(3),
                CreatedAtUtc = DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind)
            };
        }
    }

    public bool DeleteDocument(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM editor_documents WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Trim());
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public IReadOnlyList<EditorTemplateDto> GetTemplates()
    {
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, name, kind, icon, description, xml, created_at_utc, updated_at_utc
                FROM editor_templates
                ORDER BY kind, name COLLATE NOCASE;
                """;
            using var r = cmd.ExecuteReader();
            var list = new List<EditorTemplateDto>();
            while (r.Read())
            {
                list.Add(new EditorTemplateDto
                {
                    Id = r.GetString(0),
                    Name = r.GetString(1),
                    Kind = r.GetString(2),
                    Icon = r.IsDBNull(3) ? "📄" : r.GetString(3),
                    Description = r.IsDBNull(4) ? "" : r.GetString(4),
                    Xml = r.GetString(5),
                    CreatedAtUtc = DateTime.Parse(r.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    UpdatedAtUtc = DateTime.Parse(r.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
            return list;
        }
    }

    public EditorTemplateDto UpsertTemplate(UpsertEditorTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidDataException("Name es requerido.");
        if (string.IsNullOrWhiteSpace(request.Xml)) throw new InvalidDataException("Xml es requerido.");

        lock (_sync)
        {
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim();
            var name = request.Name.Trim();
            var now = DateTime.UtcNow;
            var kind = NormalizeKind(request.Kind);

            using var cn = Open();

            using (var checkCmd = cn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT id FROM editor_templates WHERE name = @name AND id <> @id LIMIT 1;";
                checkCmd.Parameters.AddWithValue("@name", name);
                checkCmd.Parameters.AddWithValue("@id", id);
                var existingId = checkCmd.ExecuteScalar();
                if (existingId != null)
                {
                    throw new InvalidDataException($"Ya existe una plantilla con el nombre '{name}'.");
                }
            }
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_templates (id, name, kind, icon, description, xml, created_at_utc, updated_at_utc)
                VALUES ($id, $name, $kind, $icon, $desc, $xml, $created, $updated)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    kind = excluded.kind,
                    icon = excluded.icon,
                    description = excluded.description,
                    xml = excluded.xml,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$name", request.Name.Trim());
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$icon", request.Icon ?? "📄");
            cmd.Parameters.AddWithValue("$desc", request.Description ?? "");
            cmd.Parameters.AddWithValue("$xml", request.Xml);
            cmd.Parameters.AddWithValue("$created", now.ToString("O"));
            cmd.Parameters.AddWithValue("$updated", now.ToString("O"));
            cmd.ExecuteNonQuery();

            return new EditorTemplateDto
            {
                Id = id,
                Name = request.Name,
                Kind = kind,
                Icon = request.Icon ?? "📄",
                Description = request.Description ?? "",
                Xml = request.Xml,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }
    }

    public string? GetSetting(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM editor_settings WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key.Trim().ToLowerInvariant());
            var val = cmd.ExecuteScalar();
            return val?.ToString();
        }
    }

    public void SaveSetting(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidDataException("Key es requerido.");
        lock (_sync)
        {
            using var cn = Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO editor_settings (key, value)
                VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("$key", key.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("$value", value ?? string.Empty);
            cmd.ExecuteNonQuery();
        }
    }

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
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS editor_elements (
                    id TEXT PRIMARY KEY,
                    key TEXT NOT NULL,
                    name TEXT NOT NULL,
                    category TEXT NOT NULL,
                    object_type TEXT NOT NULL,
                    default_width_pt REAL NOT NULL,
                    default_height_pt REAL NOT NULL,
                    default_content TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS editor_documents (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    xml TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS editor_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS editor_templates (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    icon TEXT,
                    description TEXT,
                    xml TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedDefaultsIfEmpty()
    {
        lock (_sync)
        {
            using var cn = Open();
            using var check = cn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM editor_elements;";
            var count = Convert.ToInt32(check.ExecuteScalar());
            if (count == 0)
            {
                var defaults = new[]
                {
                    new UpsertEditorElementRequest { Key = "text", Name = "Texto", Category = "basic", ObjectType = "text", DefaultWidthPt = 90, DefaultHeightPt = 24, DefaultContent = "${texto}" },
                    new UpsertEditorElementRequest { Key = "barcode", Name = "Barcode", Category = "basic", ObjectType = "barcode", DefaultWidthPt = 80, DefaultHeightPt = 32, DefaultContent = "${barcode}" },
                    new UpsertEditorElementRequest { Key = "box", Name = "Caja", Category = "shapes", ObjectType = "box", DefaultWidthPt = 60, DefaultHeightPt = 30 },
                    new UpsertEditorElementRequest { Key = "ellipse", Name = "Elipse", Category = "shapes", ObjectType = "ellipse", DefaultWidthPt = 40, DefaultHeightPt = 40 },
                    new UpsertEditorElementRequest { Key = "line", Name = "Linea", Category = "shapes", ObjectType = "line", DefaultWidthPt = 70, DefaultHeightPt = 1 },
                    new UpsertEditorElementRequest { Key = "image", Name = "Imagen", Category = "media", ObjectType = "image", DefaultWidthPt = 40, DefaultHeightPt = 40 }
                };

                foreach (var item in defaults)
                {
                    UpsertElement(item);
                }
            }

            // Seed Templates
            using var checkT = cn.CreateCommand();
            checkT.CommandText = "SELECT COUNT(*) FROM editor_templates;";
            var templateCount = Convert.ToInt32(checkT.ExecuteScalar());
            if (templateCount == 0)
            {
                var templates = new[]
                {
                    new UpsertEditorTemplateRequest 
                    { 
                        Name = "Tiquete Estándar", 
                        Kind = "saetickets", 
                        Icon = "📄", 
                        Description = "Diseño básico de 80mm para ventas generales",
                        Xml = """
                            <?xml version="1.0" encoding="utf-8"?>
                            <saetickets version="1.0">
                              <setup width="42"/>
                              <commands>
                                <text align="center" bold="true" size="extra-large">NOMBRE COMERCIAL</text>
                                <text align="center" bold="false" size="normal">Ced. Jur: 3-101-000000</text>
                                <text align="center">Tel: 2222-2222</text>
                                <separator char="="/>
                                <text align="left" bold="true">Factura: #\${ID}</text>
                                <text align="left">Cliente: \${CLIENTE}</text>
                                <text align="left">Vendedor: \${VENDEDOR}</text>
                                <separator char="-"/>
                                <each listVar="ITEMS" header="true">
                                  <column field="QTY" label="Cant" width="5" align="left"/>
                                  <column field="DESC" label="Desc" width="auto" align="left"/>
                                  <column field="TOTAL" label="Total" width="10" align="right"/>
                                </each>
                                <separator char="="/>
                                <text align="right" bold="true" size="large">TOTAL: \${TOTAL}</text>
                                <separator char="-"/>
                                <text align="center" size="small">¡Gracias por su compra!</text>
                                <feed lines="2"/>
                                <cut/>
                              </commands>
                            </saetickets>
                            """
                    },
                    new UpsertEditorTemplateRequest 
                    { 
                        Name = "Orden de Cocina", 
                        Kind = "saetickets", 
                        Icon = "🍳", 
                        Description = "Optimizado para barra y cocina con sub-items",
                        Xml = """
                            <?xml version="1.0" encoding="utf-8"?>
                            <saetickets version="1.0">
                              <setup width="42"/>
                              <commands>
                                <text align="center" bold="true" size="extra-large">ORDEN #\${ID}</text>
                                <separator char="="/>
                                <text align="left" bold="false" size="normal">Mesa: \${MESA}</text>
                                <separator char="-"/>
                                <each listVar="ITEMS" header="false" childField="EXTRAS">
                                  <column field="QTY" label="" width="4" align="left"/>
                                  <column field="DESC" label="" width="auto" align="left"/>
                                </each>
                                <separator char="-"/>
                                <feed lines="2"/>
                                <cut/>
                              </commands>
                            </saetickets>
                            """
                    },
                    new UpsertEditorTemplateRequest 
                    { 
                        Name = "Etiqueta 50x25mm", 
                        Kind = "sae", 
                        Icon = "🏷️", 
                        Description = "Etiqueta estándar de producto",
                        Xml = """
                            <?xml version="1.0" encoding="utf-8"?>
                            <SaeLabels version="1.0">
                              <template brand="Custom" description="50x25mm" part="L-5025" size="custom">
                                <label_rectangle width_pt="141.73" height_pt="70.87" round_pt="5" x_waste_pt="0" y_waste_pt="0" />
                                <layout dx_pt="0" dy_pt="0" nx="1" ny="1" x0_pt="0" y0_pt="0" />
                              </template>
                              <objects>
                                <text x="5" y="5" width="130" height="20" font_family="Arial" font_size="10" font_weight="bold">\${NAME}</text>
                                <barcode x="5" y="30" width="130" height="30" type="code128">\${SKU}</barcode>
                              </objects>
                              <variables/>
                            </SaeLabels>
                            """
                    }
                };

                foreach (var t in templates)
                {
                    UpsertTemplate(t);
                }
            }
        }
    }

    private static string NormalizeObjectType(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "text" or "barcode" or "box" or "line" or "ellipse" or "image" => normalized,
            _ => "text"
        };
    }

    private static string NormalizeKind(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "sae" or "glabels" or "saetickets" => normalized,
            _ => "sae"
        };
    }
}


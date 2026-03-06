using Microsoft.Data.Sqlite;
using SAE.STUDIO.Api.Contracts;

namespace SAE.STUDIO.Api.Services;

public sealed class EditorLibraryStore : IEditorLibraryStore
{
    private readonly string _connectionString;
    private readonly object _sync = new();

    public EditorLibraryStore()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "App_Data");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "editor.db");
        _connectionString = $"Data Source={dbPath}";
        
        try 
        {
            EnsureSchema();
            SeedDefaultsIfEmpty();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EditorLibraryStore] Error initializing schema: {ex.Message}");
        }
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
            using var cmd = cn.CreateCommand();

            // Seed default elements if table is empty
            cmd.CommandText = "SELECT COUNT(*) FROM editor_elements;";
            var elementCount = Convert.ToInt32(cmd.ExecuteScalar());
            if (elementCount == 0)
            {
                var defaults = new[]
                {
                    new { Id = "10000000000000000000000000000001", Key = "text", Name = "Texto", Category = "basic", ObjectType = "text", DefaultWidthPt = 90, DefaultHeightPt = 24, DefaultContent = "${texto}" },
                    new { Id = "10000000000000000000000000000002", Key = "barcode", Name = "Barcode", Category = "basic", ObjectType = "barcode", DefaultWidthPt = 80, DefaultHeightPt = 32, DefaultContent = "${barcode}" },
                    new { Id = "10000000000000000000000000000003", Key = "box", Name = "Caja", Category = "shapes", ObjectType = "box", DefaultWidthPt = 60, DefaultHeightPt = 30, DefaultContent = "" },
                    new { Id = "10000000000000000000000000000004", Key = "ellipse", Name = "Elipse", Category = "shapes", ObjectType = "ellipse", DefaultWidthPt = 40, DefaultHeightPt = 40, DefaultContent = "" },
                    new { Id = "10000000000000000000000000000005", Key = "line", Name = "Linea", Category = "shapes", ObjectType = "line", DefaultWidthPt = 70, DefaultHeightPt = 1, DefaultContent = "" },
                    new { Id = "10000000000000000000000000000006", Key = "image", Name = "Imagen", Category = "media", ObjectType = "image", DefaultWidthPt = 40, DefaultHeightPt = 40, DefaultContent = "" }
                };

                foreach (var item in defaults)
                {
                    cmd.CommandText = """
                        INSERT OR IGNORE INTO editor_elements (id, key, name, category, object_type, default_width_pt, default_height_pt, default_content)
                        VALUES ($id, $key, $name, $category, $type, $w, $h, $content);
                        """;
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("$id", item.Id);
                    cmd.Parameters.AddWithValue("$key", item.Key);
                    cmd.Parameters.AddWithValue("$name", item.Name);
                    cmd.Parameters.AddWithValue("$category", item.Category);
                    cmd.Parameters.AddWithValue("$type", item.ObjectType);
                    cmd.Parameters.AddWithValue("$w", item.DefaultWidthPt);
                    cmd.Parameters.AddWithValue("$h", item.DefaultHeightPt);
                    cmd.Parameters.AddWithValue("$content", item.DefaultContent);
                    cmd.ExecuteNonQuery();
                }
            }

            // Seed default templates using INSERT OR IGNORE with fixed IDs
            var now = DateTime.UtcNow.ToString("O");
            var templates = new[]
            {
                new TemplateSeederItem
                { 
                    Id = "20000000000000000000000000000001",
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
                new TemplateSeederItem
                { 
                    Id = "20000000000000000000000000000002",
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
                new TemplateSeederItem
                { 
                    Id = "20000000000000000000000000000003",
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
                },

                // ── NEW TEMPLATES (based on real-world photos) ─────────────────

                new TemplateSeederItem
                {
                    Id = "20000000000000000000000000000004",
                    Name = "DEKRA ITV - Inspección Vehicular",
                    Kind = "saetickets",
                    Icon = "🚗",
                    Description = "Tiquete electrónico para inspección técnica vehicular (ITV Periódica)",
                    Xml = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <saetickets version="1.0">
                          <setup width="42"/>
                          <commands>
                            <text align="center" bold="true" extraBold="false" size="normal">Tiquete electrónico</text>
                            <text align="center" bold="true" extraBold="false" size="normal">TE ${TIQUETE_NUM}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Fecha emisión: ${FECHA} ${HORA}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Clave: ${CLAVE_NUM}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Documento No: ${DOC_NUM}</text>
                            <separator char="=" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">**DEKRA COSTA RICA SOCIEDAD ANONIMA**</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Cédula Jurídica: 3-101-860392</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Centro Empresarial Forum | Edificio C1</text>
                            <text align="left" bold="true" extraBold="false" size="normal">10903 Pozos</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Tel: 4000 1100 </text>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Estación</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${ESTACION_NOMBRE}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${ESTACION_DIR}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Tel: ${ESTACION_TEL} Fax: -</text>
                            <text align="left" bold="false" extraBold="false" size="normal">factura.cr@dekra.com       Original</text>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Cliente:</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${CLIENTE_NOMBRE}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${CLIENTE_ID}</text>
                            <separator char="=" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Descripción</text>
                            <total label="Servicio:" value="${SERVICIO}" bold="false" extraBold="false" align="left"/>
                            <total label="Tipo:" value="${TIPO_VEHICULO}" bold="false" extraBold="false" align="left"/>
                            <total label="Placa:" value="${PLACA}" bold="false" extraBold="false" align="left"/>
                            <total label="Marca:" value="${MARCA}" bold="false" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <total label="Servicios Gravados" value="${SUBTOTAL} ${MONEDA}" bold="false" extraBold="false" align="left"/>
                            <total label="IVA 13%" value="${IVA} ${MONEDA}" bold="false" extraBold="false" align="left"/>
                            <total label="Total" value="${TOTAL} ${MONEDA}" bold="true" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Medios de pago</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${MEDIO_PAGO}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Orden de Compra</text>
                            <total label="Operador(a)" value="${OPERADOR}" bold="false" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <text align="center" bold="false" extraBold="false" size="normal">Emitido según Resolución MH-DGT-RES-0027-2024</text>
                            <text align="center" bold="false" extraBold="false" size="normal">(versión 4.4)</text>
                            <text align="center" bold="false" extraBold="false" size="medium">Software SAE SYSTEM</text>
                            <text align="center" bold="false" extraBold="false" size="medium">Gracias y buen viaje</text>
                            <feed lines="2"/>
                            <cut/>
                            <beep/>
                          </commands>
                        </saetickets>
                        """
                },

                new TemplateSeederItem
                {
                    Id = "20000000000000000000000000000005",
                    Name = "DEKRA Orden de Trabajo",
                    Kind = "saetickets",
                    Icon = "📋",
                    Description = "Orden de trabajo ITV con datos técnicos completos del vehículo",
                    Xml = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <saetickets version="1.0">
                          <setup width="42"/>
                          <commands>
                            <text align="center" bold="true" size="large">ORDEN DE TRABAJO</text>
                            <separator char="="/>
                            <total label="ENTIDAD" value="${ENTIDAD}" align="left"/>
                            <total label="PLACA" value="${PLACA}" align="left"/>
                            <total label="VIN" value="${VIN}" align="left"/>
                            <total label="NUMERO DE CHASIS" value="${CHASIS}" align="left"/>
                            <total label="MARCA" value="${MARCA}" align="left"/>
                            <total label="ESTILO" value="${ESTILO}" align="left"/>
                            <total label="ESTILO | MODELO" value="${MODELO}" align="left"/>
                            <total label="AÑO MODELO" value="${ANO}" align="left"/>
                            <total label="COMBUSTIBLE" value="${COMBUSTIBLE}" align="left"/>
                            <total label="COLOR" value="${COLOR}" align="left"/>
                            <total label="PLAZAS" value="${PLAZAS}" align="left"/>
                            <total label="TRACCION" value="${TRACCION}" align="left"/>
                            <total label="NUM. PUERTAS" value="${PUERTAS}" align="left"/>
                            <total label="FECHA INGRESO" value="${FECHA_INGRESO}" align="left"/>
                            <total label="CATEGORIA" value="${CATEGORIA}" align="left"/>
                            <total label="CARROCERIA" value="${CARROCERIA}" align="left"/>
                            <total label="PESO BRUTO" value="${PESO_BRUTO}" align="left"/>
                            <total label="NUM. MOTOR" value="${NUM_MOTOR}" align="left"/>
                            <total label="TIPO MOTOR" value="${TIPO_MOTOR}" align="left"/>
                            <total label="CILINDRADA" value="${CILINDRADA}" align="left"/>
                            <total label="CANT. CILINDROS" value="${CILINDROS}" align="left"/>
                            <total label="POWER" value="${POWER}" align="left"/>
                            <total label="MOTIVO" value="${MOTIVO}" align="left"/>
                            <total label="OBSERVACIONES" value="${OBSERVACIONES}" align="left"/>
                            <separator char="-"/>
                            <feed lines="2"/>
                            <cut/>
                          </commands>
                        </saetickets>
                        """
                },

                new TemplateSeederItem
                {
                    Id = "20000000000000000000000000000006",
                    Name = "Servicentro - Combustible con QR",
                    Kind = "saetickets",
                    Icon = "⛽",
                    Description = "Recibo de combustible con QR para verificación electrónica",
                    Xml = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <saetickets version="1.0">
                          <setup width="42"/>
                          <commands>
                            <text align="center" bold="true" extraBold="false" size="normal">###${EMPRESA_NOMBRE}###</text>
                            <text align="center" bold="false" extraBold="false" size="normal">Direccion: ${EMPRESA_DIR}</text>
                            <text align="center" bold="false" extraBold="false" size="normal">Ced. ${EMPRESA_CED} - ${EMPRESA_RAZON}</text>
                            <text align="center" bold="false" extraBold="false" size="normal">${EMPRESA_EMAIL}</text>
                            <text align="center" bold="false" extraBold="false" size="normal">Telefono: ${EMPRESA_TEL}</text>
                            <feed lines="1"/>
                            <text align="left" bold="false" extraBold="false" size="normal">Factura de Contado</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Cliente:${CLIENTE}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Actividad Economica:</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Correo:</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Consecutivo FE: ${CONSECUTIVO}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Clave Numerica: ${CLAVE_NUM}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${CLAVE_NUM_2}</text>
                            <text align="left" bold="true" extraBold="false" size="normal">Detalle:</text>
                            <feed lines="1"/>
                            <text align="center" bold="false" extraBold="false" size="normal">* Original *</text>
                            <text align="center" bold="false" extraBold="false" size="normal">- - - Productos - - -</text>
                            <each listVar="ITEMS" header="false" align="left">
                              <column field="DESC_FULL" label="" width="auto" align="left"/>
                            </each>
                            <separator char=" " align="left"/>
                            <total label="Total:" value="${TOTAL}" bold="true" extraBold="false" align="right"/>
                            <feed lines="1"/>
                            <text align="center" bold="false" extraBold="false" size="normal">Gracias por su compra</text>
                            <text align="center" bold="false" extraBold="false" size="normal">Emitida conforme a lo establecido en la resolución de Facturación Electrónica, No. MH-DGT-RES-0027-2024</text>
                            <text align="center" bold="false" extraBold="false" size="normal">- - - Despacho - - -</text>
                            <total label="Despacho:" value="${DESPACHO_NUM} * Original *" bold="false" extraBold="false" align="left"/>
                            <total label="Pistero:" value="${PISTERO}" bold="false" extraBold="false" align="left"/>
                            <total label="Fecha Despacho:" value="${FECHA_DESPACHO}" bold="false" extraBold="false" align="left"/>
                            <total label="Vehiculo:" value="${VEHICULO}" bold="false" extraBold="false" align="left"/>
                            <total label="Descripcion:" value="${VEHICULO_DESC}" bold="false" extraBold="false" align="left"/>
                            <text align="left" bold="false" extraBold="false" size="normal">Orden de Compra:</text>
                            <text align="left" bold="false" extraBold="false" size="normal">Comentarios:</text>
                            <feed lines="1"/>
                            <total label="Odometro:" value="${ODOMETRO}" bold="false" extraBold="false" align="left"/>
                            <total label="Kilometraje:" value="${KILOMETRAJE}" bold="false" extraBold="false" align="left"/>
                            <total label="KPL:" value="${KPL}" bold="false" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <total label="Posicion - ${POSICION}" value="${COMBUSTIBLE_TIPO}" bold="false" extraBold="false" align="left"/>
                            <total label="Combustible:" value="${TOTAL}" bold="false" extraBold="false" align="left"/>
                            <total label="Monto:" value="${MONTO_LETRAS}" bold="false" extraBold="false" align="left"/>
                            <total label="TOTAL" value="${VOLUMEN}L" bold="false" extraBold="false" align="right"/>
                            <total label="Volumen:" value="${PPU}" bold="false" extraBold="false" align="left"/>
                            <total label="PPU:" value="${MEDIO_PAGO}" bold="false" extraBold="false" align="left"/>
                            <total label="Modo de pago:" value="0" bold="false" extraBold="false" align="left"/>
                            <feed lines="1"/>
                            <text align="center" bold="false" extraBold="false" size="normal">* Original *</text>
                            <qr align="center" size="120">${QR_URL}</qr>
                            <feed lines="2"/>
                            <cut/>
                          </commands>
                        </saetickets>
                        """
                },

                new TemplateSeederItem
                {
                    Id = "20000000000000000000000000000007",
                    Name = "Banco Nacional - Comprobante de Pago",
                    Kind = "saetickets",
                    Icon = "🏦",
                    Description = "Comprobante de transacción BN-Servicios / corresponsal bancario",
                    Xml = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <saetickets version="1.0">
                          <setup width="42"/>
                          <commands>
                            <text align="center" bold="true" extraBold="false" size="medium">Comprobante de transacción</text>
                            <text align="center" bold="true" extraBold="false" size="normal">Banco Nacional</text>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">${EMPRESA_NOMBRE}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${EMPRESA_CIUDAD}</text>
                            <text align="left" bold="false" extraBold="false" size="normal">${EMPRESA_TEL}</text>
                            <separator char="=" align="left"/>
                            <text align="center" bold="true" extraBold="false" size="normal">Transacción procesada</text>
                            <separator char="-" align="left"/>
                            <text align="left" bold="false" extraBold="false" size="normal">Pago servicios</text>
                            <total label="Fecha pago:" value="${FECHA_PAGO}" bold="false" extraBold="false" align="left"/>
                            <total label="Institución:" value="${INSTITUCION}" bold="false" extraBold="false" align="left"/>
                            <total label="Convenio:" value="${CONVENIO}" bold="false" extraBold="false" align="left"/>
                            <total label="Identificación:" value="${IDENTIFICACION}" bold="false" extraBold="false" align="left"/>
                            <total label="Mes al cobro:" value="${MES_COBRO}" bold="false" extraBold="false" align="left"/>
                            <total label="Vencimiento:" value="${VENCIMIENTO}" bold="false" extraBold="false" align="left"/>
                            <total label="Monto:" value="${MONTO}" bold="true" extraBold="false" align="left"/>
                            <total label="Nombre:" value="${NOMBRE_CLIENTE}" bold="false" extraBold="false" align="left"/>
                            <total label="Pagado por:" value="${PAGADO_POR}" bold="false" extraBold="false" align="left"/>
                            <total label="Comprobante:" value="${COMPROBANTE}" bold="false" extraBold="false" align="left"/>
                            <total label="Monto debitado:" value="${MONTO_DEBITADO}" bold="false" extraBold="false" align="left"/>
                            <total label="Núm. factura:" value="${NUM_FACTURA}" bold="false" extraBold="false" align="left"/>
                            <total label="ID Cajero:" value="${ID_CAJERO}" bold="false" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Rubros informativos</text>
                            <total label="Número de Recibo" value="${NUM_RECIBO}" bold="false" extraBold="false" align="left"/>
                            <separator char="-" align="left"/>
                            <text align="left" bold="true" extraBold="false" size="normal">Rubros de pago</text>
                            <each listVar="RUBROS" header="false" align="left">
                              <column field="DESC" label="" width="auto" align="left"/>
                              <column field="MONTO" label="" width="12" align="right"/>
                            </each>
                            <separator char="-" align="left"/>
                            <total label="TOTAL PAGADO:" value="${TOTAL_PAGADO}" bold="true" extraBold="false" align="left"/>
                            <separator char="=" align="left"/>
                            <text align="center" bold="false" extraBold="false" size="medium">Este establecimiento opera como corresponsal no bancario del Banco Nacional.</text>
                            <text align="center" bold="false" extraBold="false" size="small"></text>
                            <separator char="-" align="left"/>
                            <text align="center" bold="true" extraBold="false" size="normal">BN-SERVICIOS</text>
                            <text align="center" bold="true" extraBold="false" size="normal">JUNTOS SOMOS PROGRESO!</text>
                            <feed lines="2"/>
                            <cut/>
                          </commands>
                        </saetickets>
                        """
                },

                new TemplateSeederItem
                {
                    Id = "20000000000000000000000000000008",
                    Name = "Control de Efectivo - Cierre de Caja",
                    Kind = "saetickets",
                    Icon = "💰",
                    Description = "Reporte completo de cierre de caja por turnos (X/Z)",
                    Xml = """
                        <?xml version="1.0" encoding="utf-8"?>
                        <saetickets version="1.0">
                          <setup width="42"/>
                          <commands>
                            <text align="center" bold="true">###EL MAICERO SAN RAFAEL###</text>
                            <text align="center" size="small">402480896</text>
                            <feed lines="1"/>
                            <each listVar="HDR_1" header="false">
                              <column field="C1" label="" width="8" align="center"/>
                              <column field="C2" label="" width="12" align="center"/>
                              <column field="C3" label="" width="auto" align="center"/>
                            </each>
                            <each listVar="HDR_2" header="false">
                              <column field="C1" label="" width="8" align="center"/>
                              <column field="C2" label="" width="12" align="center"/>
                              <column field="C3" label="" width="auto" align="center"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">DOCUMENTOS POR SERIE</text>
                            <separator char="-"/>
                            <each listVar="DOCS_SERIE" header="false">
                              <column field="DESC" label="" width="auto" align="left"/>
                              <column field="RANGE" label="" width="16" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">CONTROL DE EFECTIVO(Colón)</text>
                            <separator char="-"/>
                            <each listVar="EFECTIVO_ROWS" header="false">
                              <column field="CONCEPTO" label="" width="auto" align="left"/>
                              <column field="MONTO" label="" width="12" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">RESUMEN POR MONEDA(Colón)</text>
                            <separator char="-"/>
                            <each listVar="RESUMEN_MONEDA" header="true">
                              <column field="DESC" label="" width="10" align="left"/>
                              <column field="CALC" label="Calculado" width="10" align="right"/>
                              <column field="DECL" label="Declarado" width="10" align="right"/>
                              <column field="DIFF" label="Descuadre" width="10" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">VENTAS POR CAJERO</text>
                            <separator char="-"/>
                            <each listVar="VENTAS_CAJERO" header="true">
                              <column field="CAJERO" label="" width="auto" align="left"/>
                              <column field="VENTAS" label="Ventas" width="8" align="right"/>
                              <column field="IMPORTE" label="Importe" width="12" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">RESUMEN DE IMPUESTOS</text>
                            <separator char="-"/>
                            <each listVar="IMPUESTOS" header="false">
                              <column field="DESC" label="" width="auto" align="left"/>
                              <column field="BASE" label="Importe base" width="12" align="right"/>
                              <column field="CUOTA" label="Cuota" width="10" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">VENTAS POR FAMILIA</text>
                            <separator char="-"/>
                            <each listVar="VENTAS_FAMILIA" header="true">
                              <column field="FAMILIA" label="" width="auto" align="left"/>
                              <column field="UNIDADES" label="Unidades" width="8" align="right"/>
                              <column field="IMPORTE" label="Importe" width="12" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">VENTAS POR SERIE</text>
                            <separator char="-"/>
                            <each listVar="VENTAS_SERIE" header="true">
                              <column field="SERIE" label="" width="8" align="left"/>
                              <column field="VENTAS" label="Ventas" width="7" align="right"/>
                              <column field="IMPORTE" label="Importe" width="12" align="right"/>
                              <column field="MEDIA" label="Media" width="8" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">RESUMEN DE VENTAS</text>
                            <separator char="-"/>
                            <each listVar="RESUMEN_VENTAS" header="false">
                              <column field="CONCEPTO" label="" width="auto" align="left"/>
                              <column field="MONTO" label="" width="12" align="right"/>
                            </each>
                            <feed lines="1"/>
                            <text align="left" bold="true">VENTAS POR COMENSAL</text>
                            <separator char="-"/>
                            <total label="Numero de comensales" value="${COMENSALES}" align="right"/>
                            <total label="Promedio por comensal" value="${PROMEDIO}" align="right"/>
                            <feed lines="2"/>
                            <cut/>
                          </commands>
                        </saetickets>
                        """
                }
            };

            foreach (var t in templates)
            {
                cmd.CommandText = """
                    INSERT OR REPLACE INTO editor_templates (id, name, kind, icon, description, xml, created_at_utc, updated_at_utc)
                    VALUES ($id, $name, $kind, $icon, $desc, $xml, $created, $updated);
                    """;
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$id", t.Id);
                cmd.Parameters.AddWithValue("$name", t.Name);
                cmd.Parameters.AddWithValue("$kind", t.Kind);
                cmd.Parameters.AddWithValue("$icon", t.Icon);
                cmd.Parameters.AddWithValue("$desc", t.Description);
                cmd.Parameters.AddWithValue("$xml", t.Xml);
                cmd.Parameters.AddWithValue("$created", now);
                cmd.Parameters.AddWithValue("$updated", now);
                cmd.ExecuteNonQuery();
            }
        }
    }

        private class TemplateSeederItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Kind { get; set; }
            public string Icon { get; set; }
            public string Description { get; set; }
            public string Xml { get; set; }
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

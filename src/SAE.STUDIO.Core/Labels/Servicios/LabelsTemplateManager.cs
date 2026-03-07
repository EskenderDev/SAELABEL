using Microsoft.Extensions.Logging;
using SAE.STUDIO.Core.Labels.Modelos;
using SAE.STUDIO.Core.Labels.Caching;
using System.Text.Json;
using System.IO;

namespace SAE.STUDIO.Core.Labels.Servicios
{
    public class LabelsTemplateManager
    {
        private readonly string _basePath;
        private readonly TemplateCache _cache;
        private readonly ILogger<LabelsTemplateManager> _logger;
        private readonly SaeLabelsTemplateService _templateService;

        public LabelsTemplateManager(
            ILogger<LabelsTemplateManager> logger,
            TemplateCache cache,
            SaeLabelsTemplateService templateService)
        {
            _logger = logger;
            _cache = cache;
            _templateService = templateService;

            // Configurar ruta base en una carpeta "Etiquetas" junto al ejecutable
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Etiquetas");

            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                // Crear subcarpetas por defecto
                Directory.CreateDirectory(Path.Combine(_basePath, "Productos"));
                Directory.CreateDirectory(Path.Combine(_basePath, "Ubicaciones"));
            }
        }

        public async Task<List<PlantillaInfo>> GetTemplatesAsync(string subfolder = "")
        {
            var templates = new List<PlantillaInfo>();
            var searchPath = string.IsNullOrEmpty(subfolder) ? _basePath : Path.Combine(_basePath, subfolder);

            // Validar path traversal
            if (!Path.GetFullPath(searchPath).StartsWith(Path.GetFullPath(_basePath)))
                throw new ArgumentException("Ruta inválida");

            if (!Directory.Exists(searchPath))
                return templates;

            try
            {
                var files = Directory.GetFiles(searchPath, "*.xml");
                foreach (var file in files)
                {
                    try
                    {
                        var template = _templateService.LoadTemplate(file);
                        templates.Add(new PlantillaInfo
                        {
                            Nombre = Path.GetFileNameWithoutExtension(file),
                            RutaRelativa = Path.GetRelativePath(_basePath, file),
                            Brand = template.Brand,
                            Description = template.Description,
                            Size = template.Size,
                            LastModified = File.GetLastWriteTime(file)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error leyendo plantilla: {file}");
                        // Agregar como inválida o continuar
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listando plantillas");
            }

            return templates;
        }

        public async Task<SaeLabelsTemplate> LoadTemplateAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);

            // Validar seguridad
            if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_basePath)))
                throw new ArgumentException("Intento de acceso a ruta no permitida");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"No se encontró la plantilla: {relativePath}");

            return _templateService.LoadTemplate(fullPath);
        }

        public async Task SaveTemplateAsync(string relativePath, SaeLabelsTemplate template)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_basePath)))
                throw new ArgumentException("Ruta inválida");

            var xml = SaeLabelsTemplateXmlSerializer.Serialize(template);
            await File.WriteAllTextAsync(fullPath, xml);
            
            // Invalidar cache
            _cache.RemoveTemplate(fullPath);
        }

        public async Task SaveRawXmlAsync(string relativePath, string xmlContent)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_basePath)))
                throw new ArgumentException("Ruta inválida");

            await File.WriteAllTextAsync(fullPath, xmlContent);
            _cache.RemoveTemplate(fullPath);
        }

        public async Task<TemplateMapping?> GetMappingAsync(string templateRelativePath)
        {
            var jsonPath = Path.ChangeExtension(Path.Combine(_basePath, templateRelativePath), ".json");
            if (!File.Exists(jsonPath)) return null;

            try
            {
                var json = await File.ReadAllTextAsync(jsonPath);
                return JsonSerializer.Deserialize<TemplateMapping>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error leyendo mapeo: {jsonPath}");
                return null;
            }
        }

        public async Task SaveMappingAsync(string templateRelativePath, TemplateMapping mapping)
        {
            var jsonPath = Path.ChangeExtension(Path.Combine(_basePath, templateRelativePath), ".json");
            var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath, json);
        }

        public async Task DeleteTemplateAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);

            if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_basePath)))
                throw new ArgumentException("Ruta inválida");

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                // También borrar json asociado si existe
                var jsonPath = Path.ChangeExtension(fullPath, ".json");
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
            }
        }

        public List<string> GetSubdirectories()
        {
            return Directory.GetDirectories(_basePath)
                .Select(d => Path.GetFileName(d))
                .ToList();
        }

        public void CreateSubdirectory(string name)
        {
            // Validar nombre
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Nombre inválido");

            var path = Path.Combine(_basePath, name);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    public class PlantillaInfo
    {
        public string Nombre { get; set; } = string.Empty;
        public string RutaRelativa { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}


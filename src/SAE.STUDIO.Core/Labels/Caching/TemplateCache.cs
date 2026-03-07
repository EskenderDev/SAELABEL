using SAE.STUDIO.Core.Labels.Modelos;
using System.Collections.Concurrent;


namespace SAE.STUDIO.Core.Labels.Caching
{
    public class TemplateCache
    {
        private readonly ConcurrentDictionary<string, (SaeLabelsTemplate Template, DateTime LoadTime)> _cache
            = new ConcurrentDictionary<string, (SaeLabelsTemplate, DateTime)>();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public bool TryGetTemplate(string filePath, out SaeLabelsTemplate? template)
        {
            template = null;

            if (_cache.TryGetValue(filePath, out var cachedItem))
            {
                // Verificar si el archivo ha cambiado
                var fileModified = File.GetLastWriteTime(filePath);
                if (fileModified <= cachedItem.LoadTime &&
                    DateTime.Now - cachedItem.LoadTime < _cacheDuration)
                {
                    template = cachedItem.Template;
                    return true;
                }

                // Remover del cache si está expirado
                _cache.TryRemove(filePath, out _);
            }

            return false;
        }

        public void AddTemplate(string filePath, SaeLabelsTemplate template)
        {
            _cache[filePath] = (template, DateTime.Now);
        }

        public void RemoveTemplate(string filePath)
        {
            _cache.TryRemove(filePath, out _);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void RemoveExpired()
        {
            var now = DateTime.Now;
            foreach (var key in _cache.Keys.ToList())
            {
                if (_cache.TryGetValue(key, out var item) &&
                    now - item.LoadTime >= _cacheDuration)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }
}


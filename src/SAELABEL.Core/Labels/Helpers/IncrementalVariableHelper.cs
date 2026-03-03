using SAELABEL.Core.Labels.Modelos;

namespace SAELABEL.Core.Labels.Helpers
{
    public static class IncrementalVariableHelper
    {
        // Estado global para mantener los valores entre sesiones
        private static readonly Dictionary<string, int> _sessionCounters = new Dictionary<string, int>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Verifica si la plantilla tiene variables autoincrementales
        /// </summary>
        public static bool HasIncrementalVariables(GlabelsTemplate template)
        {
            return template?.Variables?.Any(v =>
                v.Increment != "never" &&
                v.StepSize != 0) ?? false;
        }

        /// <summary>
        /// Inicializa las variables autoincrementales de la plantilla
        /// </summary>
        public static void InitializeIncrementalVariables(GlabelsTemplate template)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                foreach (var variable in template.Variables)
                {
                    if (variable.Increment == "never") continue;

                    // Parsear el valor inicial
                    if (int.TryParse(variable.InitialValue, out int initialValue))
                    {
                        variable.CurrentValue = initialValue;
                    }
                    else
                    {
                        variable.CurrentValue = 0;
                    }

                    // Para per_session, verificar si ya existe un valor guardado
                    if (variable.Increment == "per_session")
                    {
                        var sessionKey = GetSessionKey(template, variable.Name);
                        if (_sessionCounters.TryGetValue(sessionKey, out int sessionValue))
                        {
                            variable.CurrentValue = sessionValue;
                        }
                        else
                        {
                            _sessionCounters[sessionKey] = variable.CurrentValue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Procesa las variables incrementales para una copia/ítem específico
        /// </summary>
        public static Dictionary<string, string> ProcessIncrementalVariables(
            GlabelsTemplate template,
            Dictionary<string, string> baseData,
            int currentCopy = 1,
            int currentItem = 1,
            int currentPage = 1)
        {
            if (template?.Variables == null || !template.Variables.Any())
                return baseData;

            var processedData = new Dictionary<string, string>(baseData);

            lock (_lock)
            {
                foreach (var variable in template.Variables)
                {
                    if (variable.Increment == "never") continue;

                    int valueToUse = variable.CurrentValue;

                    // Calcular el valor según el tipo de incremento
                    switch (variable.Increment)
                    {
                        case "per_copy":
                            // Incrementar por cada copia
                            valueToUse = variable.CurrentValue + (variable.StepSize * (currentCopy - 1));
                            break;

                        case "per_item":
                            // Incrementar por cada ítem (registro de datos diferente)
                            valueToUse = variable.CurrentValue + (variable.StepSize * (currentItem - 1));
                            break;

                        case "per_page":
                            // Incrementar por cada página
                            valueToUse = variable.CurrentValue + (variable.StepSize * (currentPage - 1));
                            break;

                        case "per_session":
                            // Usar el valor actual de sesión
                            valueToUse = variable.CurrentValue;
                            break;
                    }

                    // Reemplazar en los datos procesados
                    var varKey = $"${{{variable.Name}}}";
                    processedData[variable.Name] = valueToUse.ToString();

                    // También agregar con el formato ${nombre}
                    if (processedData.ContainsKey(varKey))
                    {
                        processedData[varKey] = valueToUse.ToString();
                    }
                }
            }

            return processedData;
        }

        /// <summary>
        /// Actualiza los contadores después de imprimir
        /// </summary>
        public static void UpdateIncrementalVariables(
            GlabelsTemplate template,
            int copiesPrinted,
            int itemsPrinted = 0,
            int pagesPrinted = 0)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                foreach (var variable in template.Variables)
                {
                    if (variable.Increment == "never" || variable.StepSize == 0)
                        continue;

                    int increment = 0;

                    switch (variable.Increment)
                    {
                        case "per_copy":
                            increment = variable.StepSize * copiesPrinted;
                            break;

                        case "per_item":
                            increment = variable.StepSize * (itemsPrinted > 0 ? itemsPrinted : copiesPrinted);
                            break;

                        case "per_page":
                            increment = variable.StepSize * (pagesPrinted > 0 ? pagesPrinted : copiesPrinted);
                            break;

                        case "per_session":
                            // Para per_session, incrementar según las copias totales impresas
                            increment = variable.StepSize * copiesPrinted;
                            break;
                    }

                    variable.CurrentValue += increment;

                    // Actualizar el contador de sesión si aplica
                    if (variable.Increment == "per_session")
                    {
                        var sessionKey = GetSessionKey(template, variable.Name);
                        _sessionCounters[sessionKey] = variable.CurrentValue;
                    }
                }
            }
        }

        /// <summary>
        /// Resetea los contadores de sesión (útil para reiniciar la aplicación)
        /// </summary>
        public static void ResetSessionCounters()
        {
            lock (_lock)
            {
                _sessionCounters.Clear();
            }
        }

        /// <summary>
        /// Resetea una variable específica a su valor inicial
        /// </summary>
        public static void ResetVariable(GlabelsTemplate template, string variableName)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                if (variable != null)
                {
                    if (int.TryParse(variable.InitialValue, out int initialValue))
                    {
                        variable.CurrentValue = initialValue;
                    }
                    else
                    {
                        variable.CurrentValue = 0;
                    }

                    // También actualizar sesión si aplica
                    if (variable.Increment == "per_session")
                    {
                        var sessionKey = GetSessionKey(template, variableName);
                        _sessionCounters[sessionKey] = variable.CurrentValue;
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene el valor actual de una variable
        /// </summary>
        public static int GetVariableValue(GlabelsTemplate template, string variableName)
        {
            if (template?.Variables == null) return 0;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                return variable?.CurrentValue ?? 0;
            }
        }

        /// <summary>
        /// Establece el valor de una variable
        /// </summary>
        public static void SetVariableValue(GlabelsTemplate template, string variableName, int value)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                if (variable != null)
                {
                    variable.CurrentValue = value;

                    if (variable.Increment == "per_session")
                    {
                        var sessionKey = GetSessionKey(template, variableName);
                        _sessionCounters[sessionKey] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Genera una clave única para el contador de sesión
        /// </summary>
        private static string GetSessionKey(GlabelsTemplate template, string variableName)
        {
            return $"{template.FilePath}_{variableName}";
        }

        /// <summary>
        /// Obtiene información de todas las variables de la plantilla
        /// </summary>
        public static List<VariableInfo> GetVariablesInfo(GlabelsTemplate template)
        {
            if (template?.Variables == null) return new List<VariableInfo>();

            lock (_lock)
            {
                return template.Variables.Select(v => new VariableInfo
                {
                    Name = v.Name,
                    Type = v.Type,
                    InitialValue = v.InitialValue,
                    CurrentValue = v.CurrentValue,
                    StepSize = v.StepSize,
                    Increment = v.Increment
                }).ToList();
            }
        }
    }

    /// <summary>
    /// Clase para obtener información de variables
    /// </summary>
    public class VariableInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string InitialValue { get; set; } = string.Empty;
        public int CurrentValue { get; set; }
        public int StepSize { get; set; }
        public string Increment { get; set; } = string.Empty;
    }
}


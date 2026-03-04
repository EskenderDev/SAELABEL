using SAE.STUDIO.Core.Labels.Modelos;
using System.Globalization;

namespace SAE.STUDIO.Core.Labels.Helpers
{
    public static class IncrementalVariableHelper
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// Verifica si la plantilla tiene variables autoincrementales
        /// </summary>
        public static bool HasIncrementalVariables(SaeLabelsTemplate template)
        {
            return template?.Variables?.Any(v =>
                v.Increment != "never" &&
                v.StepSize != 0) ?? false;
        }

        /// <summary>
        /// Inicializa las variables autoincrementales de la plantilla
        /// </summary>
        public static void InitializeIncrementalVariables(SaeLabelsTemplate template)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                foreach (var variable in template.Variables)
                {
                    if (variable.Increment == "never") continue;

                    variable.CurrentValue = ParseNumeric(variable.InitialValue);
                }
            }
        }

        /// <summary>
        /// Procesa las variables incrementales para una copia/ítem específico
        /// </summary>
        public static Dictionary<string, string> ProcessIncrementalVariables(
            SaeLabelsTemplate template,
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
                    // Si el diccionario de datos (Excel/Manual) ya proporciona un valor para esta variable,
                    // y la variable no es estrictamente de autoincremento (o aunque lo sea, le daremos prioridad al dato provisto),
                    // tomamos el valor provisto.
                    bool hasProvidedData = false;
                    string providedValue = "";
                    if (baseData.TryGetValue(variable.Name, out var val) || baseData.TryGetValue($"${{{variable.Name}}}", out val))
                    {
                        hasProvidedData = true;
                        providedValue = val;
                    }

                    string valueAsText;
                    if (hasProvidedData && variable.Increment == "never")
                    {
                        valueAsText = providedValue;
                    }
                    else
                    {
                        // Priorizar el valor proporcionado (e.g. desde el modal de impresión) como base para el incremento
                        double baseValue = hasProvidedData ? ParseNumeric(providedValue) : variable.CurrentValue;
                        double valueToUse = baseValue;

                        // Calcular el valor según el tipo de incremento
                        if (variable.Increment != "never" && variable.Increment != "nunca")
                        {
                            switch (variable.Increment)
                            {
                                case "per_copy":
                                case "por_copia":
                                    valueToUse = baseValue + (variable.StepSize * (currentCopy - 1));
                                    break;

                                case "per_item":
                                case "por_item":
                                case "por_elemento":
                                    valueToUse = baseValue + (variable.StepSize * (currentItem - 1));
                                    break;

                                case "per_page":
                                case "por_pagina":
                                    valueToUse = baseValue + (variable.StepSize * (currentPage - 1));
                                    break;
                            }
                        }

                        valueAsText = FormatByType(variable.Type, valueToUse, variable.InitialValue);
                    }

                    var varKey = $"${{{variable.Name}}}";
                    
                    // Aseguramos que el nombre limpio esté en el diccionario
                    processedData[variable.Name] = valueAsText;

                    // También aseguramos el formato ${Nombre} por si se usa así en la búsqueda
                    processedData[varKey] = valueAsText;
                }
            }

            return processedData;
        }

        /// <summary>
        /// Actualiza los contadores después de imprimir
        /// </summary>
        public static void UpdateIncrementalVariables(
            SaeLabelsTemplate template,
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

                    double increment = 0;

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

                    }

                    variable.CurrentValue += increment;
                }
            }
        }

        /// <summary>
        /// Reinicia las variables a sus valores iniciales.
        /// </summary>
        public static void ResetSessionCounters()
        {
            // No persistent session counters in SAE.STUDIO implementation.
        }

        /// <summary>
        /// Resetea una variable específica a su valor inicial
        /// </summary>
        public static void ResetVariable(SaeLabelsTemplate template, string variableName)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                if (variable != null)
                {
                    variable.CurrentValue = ParseNumeric(variable.InitialValue);
                }
            }
        }

        /// <summary>
        /// Obtiene el valor actual de una variable
        /// </summary>
        public static int GetVariableValue(SaeLabelsTemplate template, string variableName)
        {
            if (template?.Variables == null) return 0;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                return variable is null ? 0 : (int)Math.Round(variable.CurrentValue);
            }
        }

        /// <summary>
        /// Establece el valor de una variable
        /// </summary>
        public static void SetVariableValue(SaeLabelsTemplate template, string variableName, int value)
        {
            if (template?.Variables == null) return;

            lock (_lock)
            {
                var variable = template.Variables.FirstOrDefault(v => v.Name == variableName);
                if (variable != null)
                {
                    variable.CurrentValue = value;
                }
            }
        }

        /// <summary>
        /// Obtiene información de todas las variables de la plantilla
        /// </summary>
        public static List<VariableInfo> GetVariablesInfo(SaeLabelsTemplate template)
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

        private static double ParseNumeric(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        private static string FormatByType(string type, double value, string fallback)
        {
            var normalized = VariableTypeNormalizer.Normalize(type, VariableTypeNormalizer.String);
            return normalized switch
            {
                VariableTypeNormalizer.Integer => Math.Round(value).ToString("0", CultureInfo.InvariantCulture),
                VariableTypeNormalizer.FloatingPoint => value.ToString("0.###############", CultureInfo.InvariantCulture),
                _ => fallback
            };
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
        public double CurrentValue { get; set; }
        public double StepSize { get; set; }
        public string Increment { get; set; } = string.Empty;
    }
}

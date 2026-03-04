using System.Globalization;
using System.Text.RegularExpressions;

namespace SAE.STUDIO.Core.Labels.Helpers;

public static class VariableTypeNormalizer
{
    public const string Integer = "integer";
    public const string FloatingPoint = "floating_point";
    public const string String = "string";
    public const string Color = "color";
    public const string Boolean = "boolean";
    public const string Date = "date";
    public const string DateTime = "datetime";
    public const string Guid = "guid";

    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        { "integer", Integer },
        { "int", Integer },
        { "number", Integer },
        { "floating_point", FloatingPoint },
        { "float", FloatingPoint },
        { "double", FloatingPoint },
        { "decimal", FloatingPoint },
        { "string", String },
        { "text", String },
        { "color", Color },
        { "hex_color", Color },
        { "boolean", Boolean },
        { "bool", Boolean },
        { "date", Date },
        { "datetime", DateTime },
        { "date_time", DateTime },
        { "guid", Guid },
        { "uuid", Guid }
    };

    private static readonly Regex ColorRegex = new(
        "^#?(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string? value, string fallback = Integer)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var key = value.Trim();
        if (Allowed.TryGetValue(key, out var normalized))
        {
            return normalized;
        }

        throw new InvalidDataException(
            $"Tipo de variable inválido '{value}'. Permitidos: integer, floating_point, string, color, boolean, date, datetime, guid.");
    }

    public static void ValidateInitialValue(string type, string initialValue)
    {
        var normalizedType = Normalize(type);
        var value = initialValue?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        switch (normalizedType)
        {
            case Integer:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es integer válido.");
                }
                break;
            case FloatingPoint:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es floating_point válido.");
                }
                break;
            case Color:
                if (!ColorRegex.IsMatch(value))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es color válido (esperado #RRGGBB o #RRGGBBAA).");
                }
                break;
            case Boolean:
                if (!bool.TryParse(value, out _) && value != "0" && value != "1")
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es boolean válido (true/false/0/1).");
                }
                break;
            case Date:
                if (!System.DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es date válido.");
                }
                break;
            case DateTime:
                if (!System.DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es datetime válido.");
                }
                break;
            case Guid:
                if (!System.Guid.TryParse(value, out _))
                {
                    throw new InvalidDataException($"Valor inicial '{initialValue}' no es guid válido.");
                }
                break;
            case String:
            default:
                break;
        }
    }

    public static void ValidateIncrementCompatibility(string type, string increment)
    {
        var normalizedType = Normalize(type);
        if (increment.Equals("never", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (normalizedType != Integer && normalizedType != FloatingPoint)
        {
            throw new InvalidDataException(
                $"Incremento '{increment}' solo es compatible con tipos integer o floating_point.");
        }
    }

    public static void ValidateStepSize(string type, double stepSize, string increment)
    {
        if (increment.Equals("never", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var normalizedType = Normalize(type);
        if (normalizedType == Integer && Math.Abs(stepSize - Math.Round(stepSize)) > 0.0000001)
        {
            throw new InvalidDataException("StepSize debe ser entero para variables de tipo integer.");
        }
    }
}

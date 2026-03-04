namespace SAE.STUDIO.Core.Labels.Helpers;

public static class IncrementModeNormalizer
{
    public const string None = "never";
    public const string PerCopy = "per_copy";
    public const string PerItem = "per_item";
    public const string PerPage = "per_page";

    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        { "never", None },
        { "none", None },
        { "no", None },
        { "per_copy", PerCopy },
        { "copy", PerCopy },
        { "copia", PerCopy },
        { "por_copia", PerCopy },
        { "percopy", PerCopy },
        { "per_item", PerItem },
        { "item", PerItem },
        { "por_item", PerItem },
        { "peritem", PerItem },
        { "per_page", PerPage },
        { "page", PerPage },
        { "pagina", PerPage },
        { "página", PerPage },
        { "por_pagina", PerPage },
        { "por_página", PerPage },
        { "perpage", PerPage }
    };

    public static string Normalize(string? value, string fallback = None)
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
            $"Increment inválido '{value}'. Valores permitidos: item, copia, pagina.");
    }
}

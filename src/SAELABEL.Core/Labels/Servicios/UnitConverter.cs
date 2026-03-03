namespace SAELABEL.Core.Labels.Servicios
{
    public static class UnitConverter
    {
        private const double PointsPerInch = 72.0;
        private const double PointsPerMillimeter = 2.834645669;
        private const double PointsPerCentimeter = 28.34645669;

        public static float PointsToPixels(double points, float dpi = 72f)
            => (float)(points * dpi / PointsPerInch);

        public static double PointsToMillimeters(double points)
            => points / PointsPerMillimeter;

        public static double PointsToCentimeters(double points)
            => points / PointsPerCentimeter;

        public static double MillimetersToPoints(double millimeters)
            => millimeters * PointsPerMillimeter;

        public static double ParseMeasurement(string measurement, string defaultUnit = "pt")
        {
            if (string.IsNullOrWhiteSpace(measurement)) return 0;

            var match = System.Text.RegularExpressions.Regex.Match(measurement.Trim(), @"^([-+]?[0-9]*\.?[0-9]+)(.*)$");
            if (!match.Success) return 0;

            if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                return 0;
            }

            string unitPart = match.Groups[2].Value.Trim().ToLower();
            if (string.IsNullOrEmpty(unitPart)) unitPart = defaultUnit;

            double result;
            if (unitPart == "pt") result = value;
            else if (unitPart == "in") result = value * PointsPerInch;
            else if (unitPart == "mm") result = value * PointsPerMillimeter;
            else if (unitPart == "cm") result = value * PointsPerCentimeter;
            else if (unitPart == "px") result = value * PointsPerInch / 96.0;
            else result = value;

            return Math.Max(0, result);
        }
    }

}


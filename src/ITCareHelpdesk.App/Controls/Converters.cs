using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ITCareHelpdesk.App.Controls;

// Convertoare puse intr-o singura clasa statica accesibila prin {x:Static controls:Converters.X}.
// Alternativa cu IValueConverter per fisier ar fi explodat in 8 fisiere de 12 linii fiecare —
// aici tinem toate convertoarele "UI-only" intr-un singur loc, usor de scanat la review.
public static class Converters
{
    // ProgressToWidth: 0..1 -> px. ConverterParameter este lungimea totala in px (string sau double).
    // Folosit in splash si in barele de SLA din dashboard.
    public static readonly IValueConverter ProgressToWidth = new FuncConverter(
        static (value, parameter) =>
        {
            var progress = ToDouble(value);
            var total    = ToDouble(parameter, fallback: 100);
            return Math.Clamp(progress, 0, 1) * total;
        });

    // Daca stringul nu este gol/null returneaza true. Pentru a arata/ascunde sectiuni cu IsVisible.
    public static readonly IValueConverter StringNotEmpty = new FuncConverter(
        static (value, _) => value is string s && !string.IsNullOrWhiteSpace(s));

    // Inversul lui StringNotEmpty — folositor pentru "placeholder" overlays cand textbox-ul este gol.
    public static readonly IValueConverter StringIsEmpty = new FuncConverter(
        static (value, _) => value is null || (value is string s && string.IsNullOrWhiteSpace(s)));

    // Bool -> opacitate. Folosit ca sa "stingem" controale disabled fara IsEnabled (care taie hover).
    public static readonly IValueConverter BoolToOpacity = new FuncConverter(
        static (value, _) => value is true ? 1.0 : 0.45);

    // Prioritate -> SolidColorBrush. Tinem mapping-ul aici ca sa nu duplicam culorile in fiecare View.
    public static readonly IValueConverter PriorityToBrush = new FuncConverter(
        static (value, _) =>
        {
            var key = (value as string)?.ToUpperInvariant() ?? "";
            return key switch
            {
                "CRITICAL" => Hex("#FF3B5C"),
                "HIGH"     => Hex("#FF8A3D"),
                "MEDIUM"   => Hex("#FFB84D"),
                "LOW"      => Hex("#5BC0BE"),
                _          => Hex("#6B7388")
            };
        });

    // Status -> SolidColorBrush. Codeaza vizual ciclul de viata al tichetului.
    public static readonly IValueConverter StatusToBrush = new FuncConverter(
        static (value, _) =>
        {
            var key = (value as string)?.ToUpperInvariant() ?? "";
            return key switch
            {
                "OPEN"        => Hex("#43BAFF"),
                "IN_PROGRESS" => Hex("#FBBF24"),
                "PENDING"     => Hex("#A8B2C8"),
                "RESOLVED"    => Hex("#2DD4BF"),
                "CLOSED"      => Hex("#6B7388"),
                "CANCELLED"   => Hex("#FF3B5C"),
                _             => Hex("#6B7388")
            };
        });

    // Bool -> "Da"/"Nu" — util in detail panels unde Boolean direct arata urat (True/False)
    public static readonly IValueConverter BoolToYesNo = new FuncConverter(
        static (value, _) => value is true ? "Da" : "Nu");

    // SLA depasit -> brush rosu / verde. Foloseste alpha mic pentru ca pe rand intreg sa nu strige.
    public static readonly IValueConverter SlaToBrush = new FuncConverter(
        static (value, _) => value is true ? Hex("#33FF3B5C") : Hex("#332DD4BF"));

    // 5 stele rating -> string Unicode plin/gol. Mai poetic decat 5 imagini de PNG.
    public static readonly IValueConverter RatingToStars = new FuncConverter(
        static (value, _) =>
        {
            if (value is null) return "—";
            var d = ToDouble(value);
            var full = (int)Math.Round(d);
            return new string('★', full) + new string('☆', Math.Max(0, 5 - full));
        });

    // FirstInitial: returneaza prima litera a unui string (uppercased).
    // Folosit pentru avatars text — mai prietenos la compiled bindings decat indexer pe string.
    public static readonly IValueConverter FirstInitial = new FuncConverter(
        static (value, _) =>
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s)) return "?";
            return char.ToUpperInvariant(s.Trim()[0]).ToString();
        });

    // EnumEquals: compara .ToString-ul valorii cu ConverterParameter (string).
    // Folosit pentru a comuta IsVisible intre paneluri (enum -> bool) fara o gramada de
    // DataTemplate-uri. Pragmatic, nu academic — dar exact ce ne trebuie.
    public static readonly IValueConverter EnumEquals = new FuncConverter(
        static (value, parameter) =>
        {
            if (value is null || parameter is null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        });

    // Bool -> double pentru opacitate selectiva (item selectat = 1, restul = 0.55)
    public static readonly IValueConverter BoolToHalfOpacity = new FuncConverter(
        static (value, _) => value is true ? 1.0 : 0.55);

    // Numar -> "azi", "ieri" sau formatul scurt al datei. Folosit in liste de activitate.
    public static readonly IValueConverter DateToRelative = new FuncConverter(
        static (value, _) =>
        {
            if (value is not DateTime dt) return "";
            var diff = DateTime.Now - dt;
            return diff.TotalMinutes switch
            {
                < 1                    => "acum",
                < 60                   => $"acum {(int)diff.TotalMinutes} min",
                < 60 * 24              => $"acum {(int)diff.TotalHours} h",
                < 60 * 24 * 2          => "ieri",
                < 60 * 24 * 7          => $"acum {(int)diff.TotalDays} zile",
                _                      => dt.ToString("dd MMM yyyy")
            };
        });

    // ============================================================
    // Utilitare interne
    // ============================================================

    private static SolidColorBrush Hex(string hex) => new(Color.Parse(hex));

    private static double ToDouble(object? value, double fallback = 0)
    {
        if (value is null) return fallback;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); } catch { return fallback; }
    }

    // Adaptor minimalist Func -> IValueConverter ca sa nu scriem cate o clasa per conversie.
    private sealed class FuncConverter : IValueConverter
    {
        private readonly Func<object?, object?, object?> _convert;
        public FuncConverter(Func<object?, object?, object?> convert) => _convert = convert;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => _convert(value, parameter);

        // Niciunul din convertoarele noastre nu se foloseste in two-way binding, deci ConvertBack arunca.
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("Convertoarele acestea sunt one-way.");
    }
}

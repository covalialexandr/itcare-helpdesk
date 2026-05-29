using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Controls;

// DonutChart - control custom care deseneaza un chart tip "donut"
// pentru distributia statusurilor tichetelor.
//
// Implementarea este complet custom:
// - fara librarii externe
// - control total pe design
// - performanta foarte buna
// - integrare perfecta cu tema dark/cyan
public sealed class DonutChart : Control
{
    // Property bindabila care primeste datele chartului.
    // Lista contine perechi:
    // status + numar tichete.
    public static readonly StyledProperty<IEnumerable<StatusBucket>?> ValuesProperty =
        AvaloniaProperty.Register<DonutChart, IEnumerable<StatusBucket>?>(nameof(Values));

    // Grosimea inelului donut.
    // Cu cat valoarea este mai mare,
    // cu atat inelul devine mai gros.
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 22.0);

    // Cand valorile sau grosimea se modifica,
    // controlul trebuie redesenat automat.
    static DonutChart()
    {
        AffectsRender<DonutChart>(ValuesProperty, ThicknessProperty);
    }

    // Getter/setter pentru lista de valori.
    public IEnumerable<StatusBucket>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    // Getter/setter pentru grosimea donutului.
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    // Mapping intre status si culoarea folosita in chart.
    //
    // Folosim dictionar pentru acces rapid O(1).
    // Daca un status nu exista,
    // folosim fallback gri.
    private static readonly Dictionary<string, Color> StatusColors = new()
    {
        ["OPEN"]        = Color.Parse("#43BAFF"),
        ["IN_PROGRESS"] = Color.Parse("#FBBF24"),
        ["PENDING"]     = Color.Parse("#A8B2C8"),
        ["RESOLVED"]    = Color.Parse("#2DD4BF"),
        ["CLOSED"]      = Color.Parse("#6B7388"),
        ["CANCELLED"]   = Color.Parse("#FF3B5C"),
    };

    // Render() este apelat de Avalonia
    // ori de cate ori controlul trebuie desenat.
    public override void Render(DrawingContext context)
    {
        // Convertim IEnumerable in List
        // pentru acces mai eficient.
        var data = Values?.ToList();

        // Daca nu exista date,
        // nu avem nimic de desenat.
        if (data is null || data.Count == 0)
            return;

        // Calculam totalul tuturor segmentelor.
        var total = data.Sum(b => b.Count);

        // Daca totalul este 0,
        // chartul nu are sens.
        if (total == 0)
            return;

        // Folosim dimensiunea minima
        // pentru a mentine cercul perfect.
        var size = Math.Min(Bounds.Width, Bounds.Height);

        // Centrul chartului.
        var center = new Point(
            Bounds.Width / 2,
            Bounds.Height / 2);

        // Raza exterioara.
        // Lasam putin padding fata de margini.
        var outerR = (size / 2) - 8;

        // Raza interioara.
        // Diferenta dintre ele = grosimea inelului.
        var innerR = outerR - Thickness;

        // Daca raza interioara devine negativa,
        // inseamna ca Thickness este prea mare.
        if (innerR <= 0)
            return;

        // Desenam fundalul discret al inelului.
        //
        // Acest cerc gri ajuta segmentele mici
        // sa fie mai vizibile vizual.
        var bgPen = new Pen(
            new SolidColorBrush(Color.Parse("#1E2434")),
            Thickness);

        context.DrawEllipse(
            null,
            bgPen,
            center,
            outerR - Thickness / 2,
            outerR - Thickness / 2);

        // Pornim desenarea de la ora 12.
        //
        // -PI/2 = -90 grade.
        var startAngle = -Math.PI / 2;

        // Desenam fiecare segment.
        foreach (var bucket in data)
        {
            // Calculam cat ocupa segmentul
            // din cercul complet.
            var sweep =
                (bucket.Count / (double)total)
                * 2
                * Math.PI;

            // Ignoram segmentele aproape invizibile.
            if (sweep < 0.001)
                continue;

            // Unghi final pentru segment.
            var endAngle = startAngle + sweep;

            // Alegem culoarea statusului.
            var color =
                StatusColors.TryGetValue(
                    bucket.Status.ToUpperInvariant(),
                    out var c)
                    ? c
                    : Color.Parse("#6B7388");

            // StreamGeometry este foarte eficient
            // pentru desenarea formelor custom.
            var geom = new StreamGeometry();

            using (var ctx = geom.Open())
            {
                // Calculam punctele importante
                // pentru segmentul donutului.

                var p1Outer =
                    PolarToCart(center, outerR, startAngle);

                var p2Outer =
                    PolarToCart(center, outerR, endAngle);

                var p1Inner =
                    PolarToCart(center, innerR, endAngle);

                var p2Inner =
                    PolarToCart(center, innerR, startAngle);

                // Daca segmentul depaseste 180 grade,
                // trebuie marcat ca large arc.
                var isLargeArc = sweep > Math.PI;

                // Pornim figura.
                ctx.BeginFigure(p1Outer, true);

                // Arc exterior.
                ctx.ArcTo(
                    p2Outer,
                    new Size(outerR, outerR),
                    0,
                    isLargeArc,
                    SweepDirection.Clockwise);

                // Linie spre interior.
                ctx.LineTo(p1Inner);

                // Arc interior inapoi.
                ctx.ArcTo(
                    p2Inner,
                    new Size(innerR, innerR),
                    0,
                    isLargeArc,
                    SweepDirection.CounterClockwise);

                // Inchidem forma.
                ctx.LineTo(p1Outer);

                ctx.EndFigure(true);
            }

            // Umplem segmentul cu culoare.
            context.DrawGeometry(
                new SolidColorBrush(color),
                null,
                geom);

            // Urmatorul segment incepe
            // unde s-a terminat precedentul.
            startAngle = endAngle;
        }

        // Text central mare:
        // totalul tichetelor.
        var bigText = new FormattedText(
            total.ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                FontFamily.Default,
                FontStyle.Normal,
                FontWeight.Bold),
            32,
            new SolidColorBrush(Color.Parse("#F5F8FF")));

        // Text mic sub total.
        var smallText = new FormattedText(
            "tichete",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            11,
            new SolidColorBrush(Color.Parse("#6B7388")));

        // Pozitionam numarul central.
        context.DrawText(
            bigText,
            new Point(
                center.X - bigText.Width / 2,
                center.Y - bigText.Height / 2 - 6));

        // Pozitionam label-ul.
        context.DrawText(
            smallText,
            new Point(
                center.X - smallText.Width / 2,
                center.Y + bigText.Height / 2 - 4));
    }

    // Converteste coordonate polare
    // in coordonate carteziene.
    //
    // Formula matematica:
    // x = centerX + radius * cos(angle)
    // y = centerY + radius * sin(angle)
    //
    // Folosita pentru calcularea punctelor pe cerc.
    private static Point PolarToCart(
        Point center,
        double radius,
        double angleRad)
    {
        return new Point(
            center.X + radius * Math.Cos(angleRad),
            center.Y + radius * Math.Sin(angleRad));
    }
}
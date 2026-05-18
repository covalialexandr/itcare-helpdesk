using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Controls;

// DonutChart — desenam un inel segmentat (StatusBucket -> arc) folosind StreamGeometry.
// Alternativa cu o librarie de charts ar fi adus 5MB de dependenta pentru un singur chart;
// implementarea custom in ~80 linii e mai usor de mentinut si stilizat exact pe paleta noastra.
public sealed class DonutChart : Control
{
    public static readonly StyledProperty<IEnumerable<StatusBucket>?> ValuesProperty =
        AvaloniaProperty.Register<DonutChart, IEnumerable<StatusBucket>?>(nameof(Values));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 22.0);

    static DonutChart()
    {
        AffectsRender<DonutChart>(ValuesProperty, ThicknessProperty);
    }

    public IEnumerable<StatusBucket>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    // Mapping status -> culoare. Tinut aici (nu in convertor) pentru ca aici e singurul loc unde
    // avem nevoie de el ca brush real (nu ca brush din resurse).
    private static readonly Dictionary<string, Color> StatusColors = new()
    {
        ["OPEN"]        = Color.Parse("#43BAFF"),
        ["IN_PROGRESS"] = Color.Parse("#FBBF24"),
        ["PENDING"]     = Color.Parse("#A8B2C8"),
        ["RESOLVED"]    = Color.Parse("#2DD4BF"),
        ["CLOSED"]      = Color.Parse("#6B7388"),
        ["CANCELLED"]   = Color.Parse("#FF3B5C"),
    };

    public override void Render(DrawingContext context)
    {
        var data = Values?.ToList();
        if (data is null || data.Count == 0) return;

        var total = data.Sum(b => b.Count);
        if (total == 0) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var outerR = (size / 2) - 8;
        var innerR = outerR - Thickness;
        if (innerR <= 0) return;

        // Background-ul inelului — gri foarte subtle, ca segmentele mici sa "iasa in fata"
        var bgPen = new Pen(new SolidColorBrush(Color.Parse("#1E2434")), Thickness);
        context.DrawEllipse(null, bgPen, center, outerR - Thickness / 2, outerR - Thickness / 2);

        // Construim arcurile. Pornim de la -90deg (12 ceasul) si mergem in sensul acelor de ceasornic.
        var startAngle = -Math.PI / 2;
        foreach (var bucket in data)
        {
            var sweep = (bucket.Count / (double)total) * 2 * Math.PI;
            if (sweep < 0.001) continue;

            var endAngle = startAngle + sweep;
            var color = StatusColors.TryGetValue(bucket.Status.ToUpperInvariant(), out var c) ? c : Color.Parse("#6B7388");

            // Geometry pentru sector inel (donut slice)
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                var p1Outer = PolarToCart(center, outerR, startAngle);
                var p2Outer = PolarToCart(center, outerR, endAngle);
                var p1Inner = PolarToCart(center, innerR, endAngle);
                var p2Inner = PolarToCart(center, innerR, startAngle);

                var isLargeArc = sweep > Math.PI;

                ctx.BeginFigure(p1Outer, true);
                ctx.ArcTo(p2Outer, new Size(outerR, outerR), 0, isLargeArc, SweepDirection.Clockwise);
                ctx.LineTo(p1Inner);
                ctx.ArcTo(p2Inner, new Size(innerR, innerR), 0, isLargeArc, SweepDirection.CounterClockwise);
                ctx.LineTo(p1Outer);
                ctx.EndFigure(true);
            }

            context.DrawGeometry(new SolidColorBrush(color), null, geom);
            startAngle = endAngle;
        }

        // Text central — total absolut + label "tichete"
        var bigText = new FormattedText(
            total.ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            32,
            new SolidColorBrush(Color.Parse("#F5F8FF")));
        var smallText = new FormattedText(
            "tichete",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            11,
            new SolidColorBrush(Color.Parse("#6B7388")));

        context.DrawText(bigText, new Point(center.X - bigText.Width / 2, center.Y - bigText.Height / 2 - 6));
        context.DrawText(smallText, new Point(center.X - smallText.Width / 2, center.Y + bigText.Height / 2 - 4));
    }

    private static Point PolarToCart(Point center, double radius, double angleRad) =>
        new(center.X + radius * Math.Cos(angleRad),
            center.Y + radius * Math.Sin(angleRad));
}

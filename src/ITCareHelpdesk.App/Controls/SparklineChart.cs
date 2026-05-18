using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Controls;

// SparklineChart — desenam un line chart minimal direct prin Avalonia DrawingContext.
// Nu folosim Polyline din XAML pentru ca nu pot lega coordonatele dinamic la datele din VM
// fara un convertor complex; aici facem totul intr-un singur Render() ce rezista la resize.
public sealed class SparklineChart : Control
{
    public static readonly StyledProperty<IEnumerable<DailyResolved>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineChart, IEnumerable<DailyResolved>?>(nameof(Values));

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<SparklineChart, IBrush>(nameof(LineBrush), Brushes.DeepSkyBlue);

    public static readonly StyledProperty<IBrush> FillBrushProperty =
        AvaloniaProperty.Register<SparklineChart, IBrush>(nameof(FillBrush),
            new LinearGradientBrush { GradientStops = { new GradientStop(Color.Parse("#3043BAFF"), 0), new GradientStop(Color.Parse("#0043BAFF"), 1) }, StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(0, 1, RelativeUnit.Relative) });

    static SparklineChart()
    {
        // Cand se schimba datele, re-desenam. AffectsRender se ocupa de listener implicit.
        AffectsRender<SparklineChart>(ValuesProperty, LineBrushProperty, FillBrushProperty);
    }

    public IEnumerable<DailyResolved>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values?.ToList();
        if (values is null || values.Count < 2) return;

        var bounds = Bounds;
        var padLeft = 8.0;
        var padRight = 8.0;
        var padTop = 12.0;
        var padBottom = 18.0;

        var chartW = bounds.Width - padLeft - padRight;
        var chartH = bounds.Height - padTop - padBottom;
        if (chartW <= 0 || chartH <= 0) return;

        var maxVal = Math.Max(1, values.Max(v => v.Count));
        var stepX = chartW / (values.Count - 1);

        // Construim path-ul liniei
        var points = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = padLeft + i * stepX;
            // y este "inversat": 0 sus, max jos in coordonate ecran
            var y = padTop + chartH - (values[i].Count / (double)maxVal) * chartH;
            points[i] = new Point(x, y);
        }

        // Aria de sub linie (fill subtle cyan-to-transparent)
        var areaGeom = new StreamGeometry();
        using (var ctx = areaGeom.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, padTop + chartH), true);
            foreach (var p in points)
                ctx.LineTo(p);
            ctx.LineTo(new Point(points[^1].X, padTop + chartH));
            ctx.EndFigure(true);
        }
        context.DrawGeometry(FillBrush, null, areaGeom);

        // Linia propriu-zisa
        var linePen = new Pen(LineBrush, 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        for (var i = 0; i < points.Length - 1; i++)
            context.DrawLine(linePen, points[i], points[i + 1]);

        // Puncte la fiecare valoare — ajuta privirea sa "scaneze" trend-ul
        for (var i = 0; i < points.Length; i++)
        {
            // ultimul punct iese in fata cu cerc plin si halo, restul doar contur
            if (i == points.Length - 1)
            {
                context.DrawEllipse(LineBrush, null, points[i], 5, 5);
                context.DrawEllipse(null, new Pen(LineBrush, 1.5) { DashStyle = null }, points[i], 9, 9);
            }
            else
            {
                context.DrawEllipse(Brushes.Transparent, new Pen(LineBrush, 1.5), points[i], 3, 3);
            }
        }

        // Etichete dedesubt — 5 etichete max (la fiecare ~step) ca sa nu se suprapuna
        var labelStep = Math.Max(1, (int)Math.Ceiling(values.Count / 5.0));
        var labelTypeface = new Typeface(FontFamily.Default);
        for (var i = 0; i < values.Count; i += labelStep)
        {
            var text = new FormattedText(
                values[i].Eticheta,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                labelTypeface,
                10,
                new SolidColorBrush(Color.FromArgb(160, 168, 178, 200)));
            var lx = points[i].X - text.Width / 2;
            context.DrawText(text, new Point(lx, padTop + chartH + 4));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Controls;

// SparklineChart - line chart minimalist desenat manual.
// Folosit pentru trend-uri rapide:
// ex: tichete rezolvate pe ultimele zile.
//
// Avantajele implementarii custom:
// - control total pe stil
// - performanta foarte buna
// - resize fluid
// - fara dependente externe
// - integrare perfecta cu tema aplicatiei
//
// Chart-ul foloseste:
// - DrawingContext
// - StreamGeometry
// - desenare manuala a liniilor si punctelor
public sealed class SparklineChart : Control
{
    // Property bindabila pentru datele chartului.
    // Primeste o lista DailyResolved.
    public static readonly StyledProperty<IEnumerable<DailyResolved>?> ValuesProperty =
        AvaloniaProperty.Register<SparklineChart, IEnumerable<DailyResolved>?>(nameof(Values));

    // Brush pentru linia principala.
    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<SparklineChart, IBrush>(nameof(LineBrush), Brushes.DeepSkyBlue);

    // Brush pentru aria de sub linie.
    // Gradient cyan -> transparent.
    //
    // Creeaza efect modern de glow discret.
    public static readonly StyledProperty<IBrush> FillBrushProperty =
        AvaloniaProperty.Register<SparklineChart, IBrush>(nameof(FillBrush),
            new LinearGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.Parse("#3043BAFF"), 0),
                    new GradientStop(Color.Parse("#0043BAFF"), 1)
                },
                StartPoint = new(0, 0, RelativeUnit.Relative),
                EndPoint = new(0, 1, RelativeUnit.Relative)
            });

    // Cand se schimba datele sau brush-urile,
    // controlul trebuie redesenat.
    static SparklineChart()
    {
        AffectsRender<SparklineChart>(ValuesProperty, LineBrushProperty, FillBrushProperty);
    }

    // Getter/setter pentru valori.
    public IEnumerable<DailyResolved>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    // Getter/setter pentru brush-ul liniei.
    public IBrush LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    // Getter/setter pentru fill-ul de sub grafic.
    public IBrush FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    // Render() este apelat cand controlul trebuie desenat.
    public override void Render(DrawingContext context)
    {
        // Convertim IEnumerable -> List
        // pentru acces mai eficient.
        var values = Values?.ToList();

        // Avem nevoie de minim 2 puncte
        // pentru a desena o linie.
        if (values is null || values.Count < 2) return;

        // Dimensiunea controlului.
        var bounds = Bounds;

        // Padding intern.
        //
        // Lasam spatiu pentru:
        // - puncte
        // - labels
        // - efecte vizuale
        var padLeft = 8.0;
        var padRight = 8.0;
        var padTop = 12.0;
        var padBottom = 18.0;

        // Dimensiunea efectiva a chartului.
        var chartW = bounds.Width - padLeft - padRight;
        var chartH = bounds.Height - padTop - padBottom;

        // Daca nu avem spatiu,
        // nu desenam nimic.
        if (chartW <= 0 || chartH <= 0) return;

        // Gasim valoarea maxima.
        //
        // Folosim Math.Max(1, ...)
        // ca sa evitam impartirea la 0.
        var maxVal = Math.Max(1, values.Max(v => v.Count));

        // Distanta orizontala dintre puncte.
        var stepX = chartW / (values.Count - 1);

        // Construim punctele liniei.
        var points = new Point[values.Count];

        for (var i = 0; i < values.Count; i++)
        {
            // Pozitia X.
            var x = padLeft + i * stepX;

            // Pozitia Y.
            //
            // Coordonatele pe ecran sunt inversate:
            // 0 este sus.
            //
            // De aceea scadem din chartH.
            var y = padTop + chartH - (values[i].Count / (double)maxVal) * chartH;

            points[i] = new Point(x, y);
        }

        // Construim geometria pentru aria de sub linie.
        //
        // Aceasta creeaza efectul modern
        // cyan -> transparent.
        var areaGeom = new StreamGeometry();

        using (var ctx = areaGeom.Open())
        {
            // Pornim din coltul stanga-jos.
            ctx.BeginFigure(new Point(points[0].X, padTop + chartH), true);

            // Adaugam toate punctele liniei.
            foreach (var p in points)
                ctx.LineTo(p);

            // Inchidem figura jos.
            ctx.LineTo(new Point(points[^1].X, padTop + chartH));

            ctx.EndFigure(true);
        }

        // Desenam aria.
        context.DrawGeometry(FillBrush, null, areaGeom);

        // Pen pentru linia principala.
        //
        // Round caps/join arata mai modern
        // si elimina muchiile dure.
        var linePen = new Pen(LineBrush, 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        // Desenam fiecare segment al liniei.
        for (var i = 0; i < points.Length - 1; i++)
            context.DrawLine(linePen, points[i], points[i + 1]);

        // Desenam punctele.
        //
        // Ultimul punct este evidentiat
        // pentru a atrage atentia spre valoarea curenta.
        for (var i = 0; i < points.Length; i++)
        {
            // Ultimul punct are glow/halo.
            if (i == points.Length - 1)
            {
                // Cerc principal.
                context.DrawEllipse(LineBrush, null, points[i], 5, 5);

                // Halo exterior.
                context.DrawEllipse(null,
                    new Pen(LineBrush, 1.5) { DashStyle = null },
                    points[i],
                    9,
                    9);
            }
            else
            {
                // Restul punctelor sunt mai discrete.
                context.DrawEllipse(
                    Brushes.Transparent,
                    new Pen(LineBrush, 1.5),
                    points[i],
                    3,
                    3);
            }
        }

        // Afisam etichetele de jos.
        //
        // Limitam la aproximativ 5
        // ca sa nu se suprapuna.
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

            // Centram textul sub punct.
            var lx = points[i].X - text.Width / 2;

            context.DrawText(text, new Point(lx, padTop + chartH + 4));
        }
    }
}
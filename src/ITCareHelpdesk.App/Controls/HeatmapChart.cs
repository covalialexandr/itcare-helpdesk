using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Controls;

// HeatmapChart = control custom care deseneaza un heatmap.
// Heatmap-ul afiseaza activitatea pe zile si ore sub forma unei grile colorate.
// Cu cat culoarea este mai intensa, cu atat exista mai multe tichete.
//
// Este mult mai usor sa vezi perioadele aglomerate
// decat intr-un chart normal cu bare.
public sealed class HeatmapChart : Control
{
    // Proprietate bindabila care primeste datele heatmap-ului
    public static readonly StyledProperty<IEnumerable<HeatmapCell>?> ValuesProperty =
        AvaloniaProperty.Register<HeatmapChart, IEnumerable<HeatmapCell>?>(nameof(Values));

    // Cand Values se schimba controlul se redeseneaza automat
    static HeatmapChart()
    {
        AffectsRender<HeatmapChart>(ValuesProperty);
    }

    // Getter/setter pentru date
    public IEnumerable<HeatmapCell>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    // Numele zilelor afisate pe stanga
    // Ordinea depinde de configurarea SQL DATEFIRST
    private static readonly string[] DAYS =
    {
        "Dum",
        "Lun",
        "Mar",
        "Mie",
        "Joi",
        "Vin",
        "Sam"
    };

    // Render principal
    public override void Render(DrawingContext context)
    {
        // Convertim datele intr-o lista
        var data = Values?.ToList() ?? new List<HeatmapCell>();

        // Dimensiunile controlului
        var bounds = Bounds;

        // Spatiu rezervat pentru labels
        var labelLeft = 36.0;
        var labelBottom = 24.0;

        // Zona heatmap-ului
        var gridX = labelLeft;
        var gridY = 4.0;

        // Latime heatmap
        var gridW = bounds.Width - labelLeft - 8;

        // Inaltime heatmap
        var gridH = bounds.Height - labelBottom - gridY;

        // Daca spatiul este invalid iesim
        if (gridW <= 0 || gridH <= 0)
            return;

        // Dimensiunea unei celule
        var cellW = gridW / 24;
        var cellH = gridH / 7;

        // Cea mai mare valoare
        // folosita pentru normalizare
        var max = data.Count > 0
            ? data.Max(c => c.NrTichete)
            : 0;

        // Brush fundal
        var bgBrush = new SolidColorBrush(
            Color.Parse("#10141F"));

        // Desen fundal grila
        context.FillRectangle(
            bgBrush,
            new Rect(gridX, gridY, gridW, gridH));

        // Typeface pentru text
        var typeface = new Typeface(FontFamily.Default);

        // Brush pentru labels
        var mutedBrush = new SolidColorBrush(
            Color.FromArgb(180, 168, 178, 200));

        // Loop pe zile
        for (var zi = 1; zi <= 7; zi++)
        {
            // Loop pe ore
            for (var ora = 0; ora < 24; ora++)
            {
                // Pozitie X
                var x = gridX + (ora * cellW);

                // Pozitie Y
                var y = gridY + ((zi - 1) * cellH);

                // Rect-ul celulei
                var rect = new Rect(
                    x + 1,
                    y + 1,
                    cellW - 2,
                    cellH - 2);

                // Cautam celula corespunzatoare
                var cell = data.FirstOrDefault(
                    c => c.ZiSaptamana == zi &&
                         c.Ora == ora);

                // Numar tichete
                var count = cell?.NrTichete ?? 0;

                Color color;

                // Daca nu exista activitate
                if (max == 0 || count == 0)
                {
                    // Gri inchis
                    color = Color.Parse("#1E2434");
                }
                else
                {
                    // Intensitate intre 0 si 1
                    var intensity = count / (double)max;

                    // Alpha proportional cu activitatea
                    var alpha = (byte)(40 + intensity * 215);

                    // Cyan ITCare
                    color = Color.FromArgb(
                        alpha,
                        0x43,
                        0xBA,
                        0xFF);
                }

                // Desenam celula
                context.FillRectangle(
                    new SolidColorBrush(color),
                    rect);

                // Afisam numarul doar pe celulele importante
                if (count > 0 &&
                    max > 0 &&
                    count / (double)max > 0.4 &&
                    cellW > 16 &&
                    cellH > 14)
                {
                    // Text pentru numar
                    var ft = new FormattedText(
                        count.ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(
                            FontFamily.Default,
                            FontStyle.Normal,
                            FontWeight.SemiBold),
                        9,
                        Brushes.White);

                    // Centrare orizontala
                    var tx =
                        x +
                        (cellW - ft.Width) / 2;

                    // Centrare verticala
                    var ty =
                        y +
                        (cellH - ft.Height) / 2;

                    // Desen text
                    context.DrawText(
                        ft,
                        new Point(tx, ty));
                }
            }
        }

        // Labels pentru zile
        for (var zi = 1; zi <= 7; zi++)
        {
            var ft = new FormattedText(
                DAYS[zi - 1],
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    FontFamily.Default,
                    FontStyle.Normal,
                    FontWeight.SemiBold),
                10,
                mutedBrush);

            // Pozitie verticala
            var ty =
                gridY +
                ((zi - 1) * cellH) +
                (cellH - ft.Height) / 2;

            // Draw text
            context.DrawText(
                ft,
                new Point(4, ty));
        }

        // Labels pentru ore
        // Afisam doar din 3 in 3 ore
        for (var ora = 0; ora < 24; ora += 3)
        {
            var ft = new FormattedText(
                ora.ToString("D2") + "h",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                9,
                mutedBrush);

            // Centrare pe coloana
            var tx =
                gridX +
                (ora * cellW) +
                (cellW - ft.Width) / 2;

            // Draw text
            context.DrawText(
                ft,
                new Point(
                    tx,
                    gridY + gridH + 6));
        }
    }
}
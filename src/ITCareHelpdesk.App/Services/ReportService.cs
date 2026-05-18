using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ITCareHelpdesk.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITCareHelpdesk.App.Services;

// ReportService = generare DOCX + XLSX + PDF. Folosim OpenXML pentru Word (control 1:1 pe styling),
// ClosedXML pentru Excel (API prietenos) si QuestPDF pentru PDF (fluent API + layout engine puternic).
public sealed class ReportService
{
    // QuestPDF cere setarea licentei la initializare. "Community" e free pentru proiecte mici/non-commercial
    // si pentru practica de scoala — perfect pentru noi.
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> ExportTicketsToWordAsync(IEnumerable<Ticket> tickets, string outputPath)
    {
        await Task.Run(() =>
        {
            using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            // Qualifiem fully ca sa nu se ciocneasca cu QuestPDF.Fluent.Document — same name, alt namespace.
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Heading-ul raportului — folosim run-properties direct ca sa nu introducem un Style.xml
            AddHeading(body, "Raport Tichete IT Helpdesk — ITCare", 28);
            AddParagraph(body, $"Generat la: {System.DateTime.Now:dd.MM.yyyy HH:mm}", 10, italic: true);
            AddParagraph(body, $"Total tichete: {tickets.Count()}", 11);
            AddParagraph(body, "", 11);

            // Construim tabel cu coloane cheie — am ales doar coloanele "presentation grade"
            var table = new Table();
            var props = new TableProperties(
                new TableBorders(
                    new TopBorder    { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder   { Val = BorderValues.Single, Size = 4 },
                    new RightBorder  { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4 }));
            table.AppendChild(props);

            // Header
            table.AppendChild(BuildRow(new[] { "Numar", "Titlu", "Client", "Prioritate", "Status", "Tehnician", "Ore" }, header: true));

            foreach (var t in tickets)
            {
                table.AppendChild(BuildRow(new[]
                {
                    t.NumarTichet,
                    t.Titlu,
                    t.Client,
                    t.Prioritate,
                    t.Status,
                    t.Tehnician,
                    (t.OreLucrate ?? 0).ToString("0.00")
                }));
            }

            body.AppendChild(table);
            mainPart.Document.Save();
        });

        return outputPath;
    }

    public async Task<string> ExportTicketsToExcelAsync(IEnumerable<Ticket> tickets, string outputPath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Tichete");

            // Header row stylat
            var headers = new[] { "Numar", "Titlu", "Client", "Categorie", "Prioritate", "Status", "Tehnician", "Ore lucrate", "Data deschidere" };
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#10141F");
                cell.Style.Font.FontColor = XLColor.FromHtml("#43BAFF");
            }

            var row = 2;
            foreach (var t in tickets)
            {
                ws.Cell(row, 1).Value = t.NumarTichet;
                ws.Cell(row, 2).Value = t.Titlu;
                ws.Cell(row, 3).Value = t.Client;
                ws.Cell(row, 4).Value = t.Categorie;
                ws.Cell(row, 5).Value = t.Prioritate;
                ws.Cell(row, 6).Value = t.Status;
                ws.Cell(row, 7).Value = t.Tehnician;
                ws.Cell(row, 8).Value = (double)(t.OreLucrate ?? 0);
                ws.Cell(row, 9).Value = t.DataDeschidere;
                ws.Cell(row, 9).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";

                // Conditional formatting subtil — pe prioritate cu culoarea ITCare
                if (t.Prioritate == "CRITICAL")
                    ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#FF3B5C");
                else if (t.Prioritate == "HIGH")
                    ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#FF8A3D");

                row++;
            }

            ws.Columns().AdjustToContents();
            // Freeze prima linie ca utilizatorul sa vada header-ul cand scrolleaza tabele mari
            ws.SheetView.FreezeRows(1);
            workbook.SaveAs(outputPath);
        });

        return outputPath;
    }

    private static void AddHeading(Body body, string text, int sizeHalfPoints)
    {
        var p = new Paragraph();
        var r = new Run();
        r.AppendChild(new RunProperties(new Bold(), new FontSize { Val = sizeHalfPoints.ToString() }));
        r.AppendChild(new Text(text));
        p.AppendChild(r);
        body.AppendChild(p);
    }

    private static void AddParagraph(Body body, string text, int sizeHalfPoints, bool italic = false)
    {
        var p = new Paragraph();
        var r = new Run();
        var rp = new RunProperties(new FontSize { Val = (sizeHalfPoints * 2).ToString() });
        if (italic) rp.AppendChild(new Italic());
        r.AppendChild(rp);
        r.AppendChild(new Text(text));
        p.AppendChild(r);
        body.AppendChild(p);
    }

    private static TableRow BuildRow(string[] cells, bool header = false)
    {
        var row = new TableRow();
        foreach (var c in cells)
        {
            var cell = new TableCell();
            var p = new Paragraph();
            var r = new Run();
            var rp = new RunProperties(new FontSize { Val = "20" });
            if (header) rp.AppendChild(new Bold());
            r.AppendChild(rp);
            r.AppendChild(new Text(c) { Space = SpaceProcessingModeValues.Preserve });
            p.AppendChild(r);
            cell.AppendChild(p);
            row.AppendChild(cell);
        }
        return row;
    }

    // ============================================================
    // PDF EXPORT (QuestPDF fluent API)
    // ============================================================
    // Layout-ul matchuieste estetica aplicatiei: header negru-cyan cu logo text + pill versiune,
    // KPI strip (4 metrici), tabel cu randuri zebra subtle, prioritati cu pill colorat.
    // Genereaza un raport care arata "vandabil" — gata de print pentru sedinte de echipa.
    public async Task<string> ExportTicketsToPdfAsync(IEnumerable<Ticket> tickets, string outputPath)
    {
        var list = tickets.ToList();
        var generatedAt = DateTime.Now;
        var totalTickets   = list.Count;
        var openTickets    = list.Count(t => t.Status is "OPEN" or "IN_PROGRESS");
        var resolvedTickets= list.Count(t => t.Status is "RESOLVED" or "CLOSED");
        var criticalTickets= list.Count(t => t.Prioritate is "CRITICAL" or "HIGH");
        var totalHours     = list.Sum(t => t.OreLucrate ?? 0);

        await Task.Run(() =>
        {
            // Qualifiem fully — Document din QuestPDF intra in coliziune cu Document din OpenXml.
            QuestPDF.Fluent.Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor("#06070D");           // background "void" — matchuieste app-ul
                    page.DefaultTextStyle(t => t.FontFamily("Helvetica").FontColor("#F5F8FF"));

                    // ===== HEADER cu brand + meta =====
                    page.Header().Background("#0B0F1A").PaddingHorizontal(36).PaddingVertical(28).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ITCARE  HELPDESK").FontSize(10).LetterSpacing(2).FontColor("#43BAFF").SemiBold();
                            col.Item().PaddingTop(6).Text("Raport Tichete").FontSize(28).Bold();
                            col.Item().Text(t =>
                            {
                                t.Span("Generat ").FontColor("#6B7388").FontSize(10);
                                t.Span(generatedAt.ToString("dd MMMM yyyy 'la' HH:mm")).FontColor("#A8B2C8").FontSize(10);
                            });
                        });

                        row.ConstantItem(150).AlignRight().AlignMiddle().Column(col =>
                        {
                            col.Item().Background("#43BAFF").PaddingHorizontal(10).PaddingVertical(4)
                               .Text("PDF EXPORT").FontSize(9).FontColor("#06070D").Bold();
                            col.Item().PaddingTop(8).AlignRight()
                               .Text("v1.0 · ITCare 2026").FontSize(9).FontColor("#6B7388");
                        });
                    });

                    // ===== CONTENT =====
                    page.Content().PaddingHorizontal(36).PaddingVertical(24).Column(col =>
                    {
                        // -------- KPI strip (4 carduri) --------
                        col.Item().PaddingBottom(20).Row(r =>
                        {
                            r.RelativeItem().PaddingRight(8).Element(c => KpiCard(c, "TICHETE TOTAL", totalTickets.ToString(), "#43BAFF"));
                            r.RelativeItem().PaddingRight(8).Element(c => KpiCard(c, "DESCHISE",      openTickets.ToString(),  "#FBBF24"));
                            r.RelativeItem().PaddingRight(8).Element(c => KpiCard(c, "REZOLVATE",     resolvedTickets.ToString(),"#2DD4BF"));
                            r.RelativeItem().Element(c => KpiCard(c, "CRITICE",       criticalTickets.ToString(),"#FF3B5C"));
                        });

                        col.Item().PaddingBottom(6).Text("DETALII TICHETE").FontSize(9).LetterSpacing(2).FontColor("#6B7388");
                        col.Item().PaddingBottom(16).LineHorizontal(1).LineColor("#1E2434");

                        // -------- Tabel tichete --------
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(85);   // numar
                                c.RelativeColumn(3);    // titlu
                                c.RelativeColumn(2);    // client
                                c.ConstantColumn(70);   // prioritate
                                c.ConstantColumn(70);   // status
                                c.ConstantColumn(45);   // ore
                            });

                            // Header
                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("NR");
                                h.Cell().Element(HeaderCell).Text("TITLU");
                                h.Cell().Element(HeaderCell).Text("CLIENT");
                                h.Cell().Element(HeaderCell).Text("PRIORITATE");
                                h.Cell().Element(HeaderCell).Text("STATUS");
                                h.Cell().Element(HeaderCell).Text("ORE");
                            });

                            // Rows — alternam background-ul pentru "zebra" subtle
                            int i = 0;
                            foreach (var t in list)
                            {
                                var rowBg = i++ % 2 == 0 ? "#10141F" : "#0B0F1A";

                                table.Cell().Background(rowBg).Element(BodyCell)
                                     .Text(t.NumarTichet).FontFamily("Courier").FontSize(9).FontColor("#43BAFF");
                                table.Cell().Background(rowBg).Element(BodyCell).Text(t.Titlu).FontSize(9);
                                table.Cell().Background(rowBg).Element(BodyCell).Text(t.Client).FontSize(9).FontColor("#A8B2C8");

                                // Pill prioritate cu culoare
                                table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(8)
                                     .AlignLeft().AlignMiddle()
                                     .Background(PriorityBg(t.Prioritate))
                                     .Text(t.Prioritate).FontSize(8).Bold().FontColor(PriorityFg(t.Prioritate));

                                table.Cell().Background(rowBg).Element(BodyCell)
                                     .Text(t.Status).FontSize(8).FontColor(StatusColor(t.Status));
                                table.Cell().Background(rowBg).Element(BodyCell)
                                     .Text((t.OreLucrate ?? 0).ToString("0.0")).FontSize(9);
                            }
                        });

                        // -------- Footer summary --------
                        col.Item().PaddingTop(24).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Total ore lucrate: ").FontSize(10).FontColor("#6B7388");
                                t.Span(totalHours.ToString("0.00") + " h").FontSize(11).FontColor("#43BAFF").Bold();
                            });

                            r.ConstantItem(200).AlignRight().Text(t =>
                            {
                                t.Span("Raport generat automat de ").FontSize(9).FontColor("#6B7388");
                                t.Span("ITCare Helpdesk").FontSize(9).FontColor("#A8B2C8").Bold();
                            });
                        });
                    });

                    // ===== FOOTER cu paginare =====
                    page.Footer().Background("#0B0F1A").PaddingHorizontal(36).PaddingVertical(12).Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.Span("ITCare © 2026 · Confidential").FontSize(8).FontColor("#3F4658");
                        });
                        r.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Pagina ").FontSize(8).FontColor("#6B7388");
                            t.CurrentPageNumber().FontSize(8).FontColor("#43BAFF").Bold();
                            t.Span(" / ").FontSize(8).FontColor("#6B7388");
                            t.TotalPages().FontSize(8).FontColor("#A8B2C8");
                        });
                    });
                });
            })
            .GeneratePdf(outputPath);
        });

        return outputPath;
    }

    // Sub-componente reutilizabile pentru PDF
    private static IContainer HeaderCell(IContainer c) =>
        c.Background("#161B28").PaddingVertical(10).PaddingHorizontal(8);
    private static IContainer BodyCell(IContainer c) =>
        c.PaddingVertical(8).PaddingHorizontal(8);

    // KpiCard este folosit prin .Element(c => KpiCard(c, ...)) — Element accepta Action<IContainer>,
    // deci metoda nu trebuie sa returneze nimic. Apelul de Column() in fluent API returneaza void.
    private static void KpiCard(QuestPDF.Infrastructure.IContainer c,
        string label, string value, string accent)
    {
        c.Background("#10141F").Border(1).BorderColor("#1E2434").Padding(14).Column(col =>
        {
            col.Item().Text(label).FontSize(8).LetterSpacing(1.5f).FontColor("#6B7388");
            col.Item().PaddingTop(6).Text(value).FontSize(22).Bold().FontColor(accent);
        });
    }

    private static string PriorityBg(string p) => p switch
    {
        "CRITICAL" => "#33FF3B5C",
        "HIGH"     => "#33FF8A3D",
        "MEDIUM"   => "#33FFB84D",
        "LOW"      => "#335BC0BE",
        _          => "#1E2434"
    };

    private static string PriorityFg(string p) => p switch
    {
        "CRITICAL" => "#FF3B5C",
        "HIGH"     => "#FF8A3D",
        "MEDIUM"   => "#FFB84D",
        "LOW"      => "#5BC0BE",
        _          => "#A8B2C8"
    };

    private static string StatusColor(string s) => s switch
    {
        "OPEN"        => "#43BAFF",
        "IN_PROGRESS" => "#FBBF24",
        "PENDING"     => "#A8B2C8",
        "RESOLVED"    => "#2DD4BF",
        "CLOSED"      => "#6B7388",
        _             => "#A8B2C8"
    };
}

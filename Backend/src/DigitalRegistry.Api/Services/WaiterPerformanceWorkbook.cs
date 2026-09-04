using System.Globalization;
using ClosedXML.Excel;
using DigitalRegistry.Application.Features.Reports;

namespace DigitalRegistry.Api.Services;

/// <summary>
/// Turns the per-waiter report into the spreadsheet an owner asked for.
/// </summary>
/// <remarks>
/// It lives in the API layer rather than in Application on purpose: a file format is a detail of how
/// the report is delivered, not of what it says. The handler stays free of ClosedXML, and a second
/// delivery — a PDF, a CSV — would be another class beside this one rather than a change to the
/// query.
/// <para>
/// Everything is written as a number, not as pre-formatted text. The point of handing over a
/// workbook rather than a printout is that the person receiving it can sort it, total it and paste
/// it into their own sheet; a column of strings that look like money defeats that quietly.
/// </para>
/// </remarks>
public static class WaiterPerformanceWorkbook
{
    /// <summary>Serbian Latin, so month names and decimal separators match the till's screens.</summary>
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("sr-Latn-RS");

    private const string MoneyFormat = "#,##0 \"RSD\"";

    public static byte[] Build(WaiterPerformanceReportDto report, string venueName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Konobari");

        var period = $"{report.FromDate.ToString("dd.MM.yyyy.", Culture)} — " +
                     $"{report.ToDate.ToString("dd.MM.yyyy.", Culture)}";

        sheet.Cell("A1").Value = string.IsNullOrWhiteSpace(venueName) ? "Izveštaj po konobarima" : venueName;
        sheet.Cell("A1").Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Range("A1:G1").Merge();

        sheet.Cell("A2").Value = $"Učinak po konobaru, {period}";
        sheet.Cell("A2").Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        sheet.Range("A2:G2").Merge();

        var headerRow = 4;
        var headers = new[]
        {
            "Konobar",
            "Iznetih porudžbina",
            "Vrednost porudžbina",
            "Prosečno vreme usluge (min)",
            "Mereno rundi",
            "Sati rada",
            "Promet po satu"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            var cell = sheet.Cell(headerRow, column + 1);
            cell.Value = headers[column];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0E4F52"));
            cell.Style.Font.SetFontColor(XLColor.White);
            cell.Style.Alignment.SetWrapText();
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        var row = headerRow + 1;

        foreach (var waiter in report.Waiters)
        {
            sheet.Cell(row, 1).Value = waiter.Name;
            sheet.Cell(row, 2).Value = waiter.OrderCount;
            sheet.Cell(row, 3).Value = waiter.TotalValue;
            sheet.Cell(row, 3).Style.NumberFormat.Format = MoneyFormat;

            // Blank, not zero: a waiter none of whose rounds was ever timed has no average, and a
            // nought in that cell would read as instant service.
            if (waiter.AverageServiceMinutes is { } minutes)
            {
                sheet.Cell(row, 4).Value = minutes;
                sheet.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            }
            else
            {
                sheet.Cell(row, 4).Value = "—";
                sheet.Cell(row, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            }

            sheet.Cell(row, 5).Value = waiter.TimedOrderCount;
            sheet.Cell(row, 6).Value = waiter.HoursWorked;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 7).Value = waiter.ValuePerHour;
            sheet.Cell(row, 7).Style.NumberFormat.Format = MoneyFormat;

            row++;
        }

        if (report.Waiters.Count > 0)
        {
            var totals = row;

            sheet.Cell(totals, 1).Value = "Ukupno";
            sheet.Cell(totals, 2).Value = report.Waiters.Sum(waiter => waiter.OrderCount);
            sheet.Cell(totals, 3).Value = report.Waiters.Sum(waiter => waiter.TotalValue);
            sheet.Cell(totals, 3).Style.NumberFormat.Format = MoneyFormat;
            sheet.Cell(totals, 5).Value = report.Waiters.Sum(waiter => waiter.TimedOrderCount);
            sheet.Cell(totals, 6).Value = Math.Round(report.Waiters.Sum(waiter => waiter.HoursWorked), 2);
            sheet.Cell(totals, 6).Style.NumberFormat.Format = "0.00";

            // The average of the averages would be wrong — it weights a waiter who carried two rounds
            // the same as one who carried eighty. Weighted by the rounds each average rests on.
            var timed = report.Waiters.Sum(waiter => waiter.TimedOrderCount);

            if (timed > 0)
            {
                sheet.Cell(totals, 4).Value = Math.Round(
                    report.Waiters.Sum(waiter =>
                        (waiter.AverageServiceMinutes ?? 0) * waiter.TimedOrderCount) / timed,
                    1);
                sheet.Cell(totals, 4).Style.NumberFormat.Format = "0.0";
            }

            sheet.Range(totals, 1, totals, headers.Length).Style.Font.SetBold();
            sheet.Range(totals, 1, totals, headers.Length).Style.Border
                .SetTopBorder(XLBorderStyleValues.Thin);
        }
        else
        {
            sheet.Cell(row, 1).Value = "Nema podataka za izabrani period.";
            sheet.Cell(row, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        }

        var note = row + 2;
        sheet.Cell(note, 1).Value =
            "Sati rada su sati iz rasporeda smena, ne evidencija dolaska. " +
            "Vreme usluge se meri samo za runde poručene preko QR koda, od porudžbine do iznošenja.";
        sheet.Cell(note, 1).Style.Font.SetItalic().Font.SetFontSize(9).Font.SetFontColor(XLColor.Gray);
        sheet.Range(note, 1, note, headers.Length).Merge();

        sheet.Column(1).Width = 26;

        for (var column = 2; column <= headers.Length; column++)
        {
            sheet.Column(column).Width = 16;
        }

        sheet.Row(headerRow).Height = 32;
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    /// <summary>The name the browser saves it under, with the period in it.</summary>
    public static string FileName(WaiterPerformanceReportDto report) =>
        $"konobari_{report.FromDate:yyyy-MM-dd}_{report.ToDate:yyyy-MM-dd}.xlsx";
}

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var rows = Enumerable.Range(1, 11).Select(d => new {
    Date = new DateOnly(2026, 5, d),
    Sales = (decimal)(d * 300 + 500),
    Cash = (decimal)(d * 150),
    Card = (decimal)(d * 80),
    Upi = (decimal)(d * 50),
    Pending = d % 3 == 0 ? (decimal)(d * 40) : 0m,
    Expenses = d % 5 == 0 ? (decimal)(d * 20) : 0m,
    Txns = d + 2
}).ToList();

string Amt(decimal v) => $"Rs.{v:N0}";

var doc = Document.Create(c =>
{
    c.Page(page =>
    {
        page.Size(297, 210, Unit.Millimetre);
        page.Margin(1.5f, Unit.Centimetre);
        page.DefaultTextStyle(x => x.FontSize(8f).FontFamily("Arial"));
        page.Header().Text("TEST - Daily Table").FontSize(14).Bold();
        page.Content().PaddingTop(8).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2); cols.RelativeColumn(3); cols.RelativeColumn(2);
                    cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2);
                    cols.RelativeColumn(2); cols.RelativeColumn(1);
                });
                table.Header(h =>
                {
                    const string hb = "#1a56db";
                    h.Cell().Background(hb).Padding(4).Text("Date").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("Sales").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("Cash").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("Card").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("UPI").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("Pending").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("Expenses").FontSize(8).Bold().FontColor("#ffffff");
                    h.Cell().Background(hb).Padding(4).AlignRight().Text("#").FontSize(8).Bold().FontColor("#ffffff");
                });
                var ri = 0;
                foreach (var d in rows)
                {
                    var bg = ri++ % 2 == 0 ? "#ffffff" : "#f9fafb";
                    table.Cell().Background(bg).Padding(4).Text(d.Date.ToString("dd MMM")).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(Amt(d.Sales)).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(Amt(d.Cash)).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(Amt(d.Card)).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(Amt(d.Upi)).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(d.Pending > 0 ? Amt(d.Pending) : "-").FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(Amt(d.Expenses)).FontSize(7.5f);
                    table.Cell().Background(bg).Padding(4).AlignRight().Text(d.Txns.ToString()).FontSize(7.5f);
                }
            });
        });
    });
});

var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test_table.pdf");
doc.GeneratePdf(path);
Console.WriteLine($"OK: {path}");

using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using ServiceOps.Api.Enums;
using ServiceOps.Api.Models;
namespace ServiceOps.Api.Services.Reporting;

public sealed class ServiceOpsFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("ServiceOpsVera", bold, italic);
    public byte[] GetFont(string faceName)
    {
        using var stream = typeof(ServiceOpsFontResolver).Assembly.GetManifestResourceStream("ServiceOps.Api.Resources.Fonts.Vera.ttf")
            ?? throw new InvalidOperationException("Report font resource is unavailable.");
        using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
    }
}
public sealed class ServiceReportPdf
{
    public byte[] Create(WorkOrder work)
    {
        using var document = new PdfDocument(); document.Info.Title = $"ServiceOps Service Report SO-{work.WorkOrderId:000000}";
        XGraphics? graphics = null; PdfPage? page = null; double y = 0;
        var body = new XFont("ServiceOps", 10); var heading = new XFont("ServiceOps", 14, XFontStyleEx.Bold);
        var blue = new XSolidBrush(XColor.FromArgb(37, 99, 235));
        void NewPage()
        {
            graphics?.Dispose(); page = document.AddPage(); page.Size = PdfSharp.PageSize.A4; graphics = XGraphics.FromPdfPage(page);
            graphics.DrawString("ServiceOps", new XFont("ServiceOps", 22, XFontStyleEx.Bold), blue, new XPoint(42, 48));
            graphics.DrawString("FIELD SERVICE & MAINTENANCE", new XFont("ServiceOps", 8), XBrushes.Gray, new XPoint(42, 65));
            graphics.DrawLine(new XPen(XColors.LightGray), 42, 78, page.Width.Point - 42, 78);
            graphics.DrawString($"SO-{work.WorkOrderId:000000}  |  Service report", body, XBrushes.Gray, new XPoint(42, page.Height.Point - 30));
            graphics.DrawString($"Page {document.PageCount}", body, XBrushes.Gray, new XPoint(page.Width.Point - 100, page.Height.Point - 30)); y = 105;
        }
        void Line(string text, bool title = false)
        {
            if (y > page!.Height.Point - 70) NewPage();
            graphics!.DrawString(text, title ? heading : body, title ? blue : XBrushes.Black, new XPoint(42, y)); y += title ? 26 : 17;
        }
        IEnumerable<string> SplitWord(string word)
        {
            var chunk = "";
            var width = page!.Width.Point - 84;
            foreach (var rune in word.EnumerateRunes())
            {
                var character = rune.ToString();
                if (chunk.Length > 0 && graphics!.MeasureString(chunk + character, body).Width > width)
                {
                    yield return chunk;
                    chunk = "";
                }
                chunk += character;
            }
            if (chunk.Length > 0) yield return chunk;
        }
        void Text(string label, string? value)
        {
            if (y > page!.Height.Point - 120) NewPage();
            Line(label, true); var text = string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
            foreach (var paragraph in text.Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Break unusually long user-supplied words without losing data or clipping margins.
                    foreach (var chunk in SplitWord(word))
                    {
                        var candidate = line.Length == 0 ? chunk : line + " " + chunk;
                        if (graphics!.MeasureString(candidate, body).Width > page!.Width.Point - 84) { Line(line); line = chunk; } else line = candidate;
                    }
                }
                Line(line);
            }
            y += 9;
        }
        static string Date(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "Not recorded";
        var site = work.ServiceRequest.CustomerSite;
        var address = string.Join(", ", new[] { site.AddressLine, site.City, site.Province, site.PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
        NewPage(); Line($"Service report SO-{work.WorkOrderId:000000}", true);
        Text("Customer and site", $"{work.ServiceRequest.Customer.CustomerName}\n{work.ServiceRequest.Customer.User.Email} | {work.ServiceRequest.Customer.User.PhoneNumber ?? "Phone not recorded"}\n{work.ServiceRequest.CustomerSite.SiteName}\n{address}");
        Text("Equipment", work.ServiceRequest.Asset == null ? "No equipment linked (site-only service)" : $"{work.ServiceRequest.Asset.AssetName}\nSerial: {work.ServiceRequest.Asset.SerialNumber ?? "Not recorded"}\nCategory: {work.ServiceRequest.Asset.Category ?? "Not recorded"}\nInstalled: {Date(work.ServiceRequest.Asset.InstallationDate)}\nWarranty: {work.ServiceRequest.Asset.WarrantyInformation ?? "Not recorded"}");
        Text($"Service request #{work.ServiceRequestId}", $"Priority: {work.ServiceRequest.Priority} | Category: {work.ServiceRequest.Category}\n{work.ServiceRequest.IssueDescription}");
        Text("Technician and service dates", $"{work.Technician.User.FullName} | {work.Technician.Skill}\nScheduled: {Date(work.ScheduledVisit)}\nStarted: {Date(work.StartTime)}\nCompleted: {Date(work.EndTime)}");
        Text("Diagnosis", work.Diagnosis); Text("Work performed", work.WorkPerformed);
        var parts = work.InventoryTransactions.GroupBy(t => new { t.SparePartId, t.SparePart.PartName })
            .Select(g => $"{g.Key.PartName}: issued {g.Where(t => t.TransactionType == InventoryTransactionType.PartIssue).Sum(t => t.Quantity)}, consumed {g.Where(t => t.TransactionType == InventoryTransactionType.PartConsumption).Sum(t => t.Quantity)}, returned {g.Where(t => t.TransactionType == InventoryTransactionType.StockAddition).Sum(t => t.Quantity)}");
        Text("Spare parts", string.Join("\n", parts.DefaultIfEmpty("No parts recorded")));
        Text("Completion", $"Job status: {work.Status}\nCustomer confirmation: {(work.CustomerConfirmation?.IsConfirmed == true ? Date(work.CustomerConfirmation.ConfirmedAt) : "Awaiting confirmation")}\nGenerated: {Date(DateTime.UtcNow)}");
        graphics?.Dispose(); using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }
}

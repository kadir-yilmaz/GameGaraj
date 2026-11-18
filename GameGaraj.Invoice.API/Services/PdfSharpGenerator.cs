using System.Globalization;
using GameGaraj.Invoice.API.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace GameGaraj.Invoice.API.Services;

/// <summary>
/// Builds invoice PDF documents using PDFsharp.
/// </summary>
public sealed class PdfSharpGenerator : IPdfGenerator
{
    private const double Margin = 50;
    private const double RowHeight = 28;
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public byte[] GenerateInvoicePdf(InvoiceData invoice)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"GameGaraj Fatura #{invoice.OrderId}";

        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using var gfx = XGraphics.FromPdfPage(page);
        var styles = new PdfStyles();

        var left = Margin;
        var right = page.Width.Point - Margin;
        var width = page.Width.Point - (Margin * 2);

        DrawHeader(gfx, styles, invoice, left, right);
        DrawParties(gfx, styles, invoice, left);

        var tableBottomY = DrawItemsTable(gfx, styles, invoice.Items, left, right, width);
        DrawTotalBox(gfx, styles, invoice.TotalPrice, right, tableBottomY + 15);
        DrawFooter(gfx, styles, left, right, page.Height.Point - Margin);

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void DrawHeader(XGraphics gfx, PdfStyles s, InvoiceData invoice, double left, double right)
    {
        gfx.DrawString("GAMEGARAJ", s.LogoFont, s.BrandBlueBrush, new XPoint(left, 55));
        gfx.DrawString("Performansın Adresi", s.FooterFont, s.MutedBrush, new XPoint(left, 70));

        gfx.DrawString("E-FATURA", s.TitleFont, s.DarkBrush, new XRect(right - 200, 35, 200, 20), XStringFormats.TopRight);
        gfx.DrawString($"Fatura No: {invoice.InvoiceNumber}", s.MetaFont, s.TextBrush, new XRect(right - 200, 58, 200, 15), XStringFormats.TopRight);
        gfx.DrawString($"Tarih: {invoice.OrderDate:dd.MM.yyyy HH:mm}", s.MetaFont, s.TextBrush, new XRect(right - 200, 71, 200, 15), XStringFormats.TopRight);

        gfx.DrawLine(s.BlueAccentPen, left, 90, right, 90);
    }

    private static void DrawParties(XGraphics gfx, PdfStyles s, InvoiceData invoice, double left)
    {
        const double y = 110;
        var rightCol = left + 260;

        gfx.DrawString("FATURA ALICISI", s.SectionFont, s.DarkBrush, new XPoint(left, y));
        gfx.DrawString(invoice.CustomerName, s.BoldFont, s.TextBrush, new XPoint(left, y + 18));
        gfx.DrawString(invoice.CustomerEmail, s.RegularFont, s.TextBrush, new XPoint(left, y + 32));

        gfx.DrawString("FİRMA BİLGİLERİ", s.SectionFont, s.DarkBrush, new XPoint(rightCol, y));
        gfx.DrawString("GameGaraj Teknoloji A.Ş.", s.BoldFont, s.TextBrush, new XPoint(rightCol, y + 18));
        gfx.DrawString("info@gamegaraj.com | 0850 222 5555", s.RegularFont, s.TextBrush, new XPoint(rightCol, y + 32));
    }

    private static double DrawItemsTable(XGraphics gfx, PdfStyles s, IReadOnlyList<InvoiceItem> items, double left, double right, double width)
    {
        double y = 175;

        gfx.DrawRectangle(s.DarkBrush, left, y, width, 24);
        gfx.DrawString("Ürün Açıklaması", s.BoldFont, XBrushes.White, new XPoint(left + 10, y + 15));
        gfx.DrawString("Fiyat", s.BoldFont, XBrushes.White, new XRect(right - 130, y, 120, 24), XStringFormats.CenterRight);
        y += 24;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (i % 2 == 1)
            {
                gfx.DrawRectangle(s.LightBgBrush, left, y, width, RowHeight);
            }

            gfx.DrawLine(s.BorderPen, left, y + RowHeight, right, y + RowHeight);
            DrawProductName(gfx, s, item.ProductName, left + 10, y);
            gfx.DrawString(FormatTl(item.Price), s.RegularFont, s.TextBrush, new XRect(right - 130, y, 120, RowHeight), XStringFormats.CenterRight);

            y += RowHeight;
        }

        return y;
    }

    private static void DrawProductName(XGraphics gfx, PdfStyles s, string name, double x, double y)
    {
        if (name.Length <= 55)
        {
            gfx.DrawString(name, s.RegularFont, s.TextBrush, new XPoint(x, y + 17));
            return;
        }

        var line1 = name[..55];
        var line2 = name.Length > 110 ? $"{name[55..107]}..." : name[55..];
        gfx.DrawString(line1, s.RegularFont, s.TextBrush, new XPoint(x, y + 11));
        gfx.DrawString(line2, s.RegularFont, s.MutedBrush, new XPoint(x, y + 22));
    }

    private static void DrawTotalBox(XGraphics gfx, PdfStyles s, decimal totalPrice, double right, double y)
    {
        const double boxWidth = 220;
        const double boxHeight = 40;
        var boxX = right - boxWidth;

        gfx.DrawRectangle(s.AccentBgBrush, boxX, y, boxWidth, boxHeight);
        gfx.DrawRectangle(s.BlueBorderPen, boxX, y, boxWidth, boxHeight);
        gfx.DrawString("TOPLAM TUTAR:", s.BoldFont, s.TextBrush, new XRect(boxX + 15, y, 100, boxHeight), XStringFormats.CenterLeft);
        gfx.DrawString(FormatTl(totalPrice), s.TitleFont, s.BrandBlueBrush, new XRect(right - 130, y, 120, boxHeight), XStringFormats.CenterRight);
    }

    private static void DrawFooter(XGraphics gfx, PdfStyles s, double left, double right, double y)
    {
        gfx.DrawLine(s.BorderPen, left, y, right, y);
        gfx.DrawString("Bu fatura 5070 sayılı Elektronik İmza Kanunu uyarınca e-imza ile imzalanmıştır.", s.FooterFont, s.MutedBrush, new XPoint(left, y + 15));
        gfx.DrawString("© 2026 GameGaraj - www.gamegaraj.com", s.FooterFont, s.MutedBrush, new XRect(right - 200, y + 15, 200, 15), XStringFormats.TopRight);
    }

    private static string FormatTl(decimal amount) => $"{amount.ToString("N2", TrCulture)} TL";

    private sealed class PdfStyles
    {
        private static readonly XColor BrandBlue = XColor.FromArgb(13, 110, 253);

        public XSolidBrush BrandBlueBrush { get; } = new(BrandBlue);
        public XSolidBrush DarkBrush { get; } = new(XColor.FromArgb(28, 28, 28));
        public XSolidBrush TextBrush { get; } = new(XColor.FromArgb(33, 37, 41));
        public XSolidBrush MutedBrush { get; } = new(XColor.FromArgb(108, 117, 125));
        public XSolidBrush LightBgBrush { get; } = new(XColor.FromArgb(248, 249, 250));
        public XSolidBrush AccentBgBrush { get; } = new(XColor.FromArgb(240, 244, 255));

        public XPen BorderPen { get; } = new(XColor.FromArgb(233, 236, 239), 1);
        public XPen BlueBorderPen { get; } = new(BrandBlue, 1);
        public XPen BlueAccentPen { get; } = new(BrandBlue, 3);

        public XFont LogoFont { get; } = new("Arial", 22, XFontStyleEx.Bold);
        public XFont TitleFont { get; } = new("Arial", 16, XFontStyleEx.Bold);
        public XFont SectionFont { get; } = new("Arial", 11, XFontStyleEx.Bold);
        public XFont BoldFont { get; } = new("Arial", 9, XFontStyleEx.Bold);
        public XFont RegularFont { get; } = new("Arial", 9, XFontStyleEx.Regular);
        public XFont FooterFont { get; } = new("Arial", 8, XFontStyleEx.Italic);
        public XFont MetaFont { get; } = new("Arial", 8, XFontStyleEx.Regular);
    }
}

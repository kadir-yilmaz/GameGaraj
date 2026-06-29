using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using System;
using System.IO;
using GameGaraj.Invoice.API.Models;

namespace GameGaraj.Invoice.API.Services
{
    public interface IPdfGenerator
    {
        byte[] GenerateInvoicePdf(InvoiceData invoice);
    }

    public class PdfSharpGenerator : IPdfGenerator
    {
        private static bool _fontResolverRegistered = false;
        private static readonly object _lock = new object();

        public PdfSharpGenerator()
        {
            if (!_fontResolverRegistered)
            {
                lock (_lock)
                {
                    if (!_fontResolverRegistered)
                    {
                        try
                        {
                            GlobalFontSettings.FontResolver = new FileFontResolver();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[PdfSharpGenerator] Font resolver registration error: {ex.Message}");
                        }
                        _fontResolverRegistered = true;
                    }
                }
            }
        }

        public byte[] GenerateInvoicePdf(InvoiceData invoice)
        {
            // Register encoding provider for code pages (required by PDFsharp on .NET Core)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // Create document
            using var document = new PdfDocument();
            document.Info.Title = $"GameGaraj Fatura #{invoice.OrderId}";

            // Add page
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;

            // Get graphics for drawing
            using var gfx = XGraphics.FromPdfPage(page);

            // Define fonts (will be resolved via our FileFontResolver to DejaVuSans)
            var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
            var headerFont = new XFont("Arial", 12, XFontStyleEx.Bold);
            var boldFont = new XFont("Arial", 10, XFontStyleEx.Bold);
            var regularFont = new XFont("Arial", 10, XFontStyleEx.Regular);
            var footerFont = new XFont("Arial", 8, XFontStyleEx.Italic);

            // Define pens and brushes
            var darkBlueBrush = new XSolidBrush(XColor.FromArgb(13, 110, 253)); // #0d6efd
            var blackBrush = XBrushes.Black;
            var grayBrush = XBrushes.DarkGray;
            var lightGrayBrush = new XSolidBrush(XColor.FromArgb(240, 240, 240));
            var borderPen = new XPen(XColor.FromArgb(224, 224, 224), 1);
            var blackPen = new XPen(XColors.Black, 1);

            double y = 40;

            // 1. Draw Header Bar
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(28, 28, 28)), 0, 0, page.Width.Point, 80);
            gfx.DrawString("GAMEGARAJ", titleFont, darkBlueBrush, new XPoint(40, 48));
            gfx.DrawString("FATURA", boldFont, XBrushes.White, new XPoint(page.Width.Point - 100, 45));

            y = 110;

            // 2. Invoice Details (Left: Customer, Right: Order Info)
            gfx.DrawString("Fatura Alıcısı:", boldFont, blackBrush, new XPoint(40, y));
            gfx.DrawString("Fatura Detayları:", boldFont, blackBrush, new XPoint(page.Width.Point - 240, y));
            
            y += 15;
            gfx.DrawString(invoice.CustomerName, regularFont, blackBrush, new XPoint(40, y));
            gfx.DrawString($"Fatura No: {invoice.InvoiceNumber}", regularFont, blackBrush, new XPoint(page.Width.Point - 240, y));

            y += 15;
            gfx.DrawString(invoice.CustomerEmail, regularFont, blackBrush, new XPoint(40, y));
            gfx.DrawString($"Sipariş Tarihi: {invoice.OrderDate:dd MMMM yyyy HH:mm}", regularFont, blackBrush, new XPoint(page.Width.Point - 240, y));

            y += 15;
            gfx.DrawString($"Sipariş ID: #{invoice.OrderId}", regularFont, blackBrush, new XPoint(page.Width.Point - 240, y));

            y += 30;

            // 3. Draw Items Table Header
            gfx.DrawRectangle(lightGrayBrush, 40, y, page.Width.Point - 80, 25);
            gfx.DrawString("Ürün Adı", boldFont, blackBrush, new XPoint(50, y + 17));
            gfx.DrawString("Fiyat", boldFont, blackBrush, new XPoint(page.Width.Point - 120, y + 17));

            y += 25;

            // Draw Items
            foreach (var item in invoice.Items)
            {
                gfx.DrawLine(borderPen, 40, y, page.Width.Point - 40, y);
                gfx.DrawString(item.ProductName, regularFont, blackBrush, new XPoint(50, y + 17));
                gfx.DrawString($"{item.Price:C}", regularFont, blackBrush, new XPoint(page.Width.Point - 120, y + 17));
                y += 25;
            }

            gfx.DrawLine(blackPen, 40, y, page.Width.Point - 40, y);

            // Draw Total
            y += 20;
            gfx.DrawString("TOPLAM TUTAR:", boldFont, blackBrush, new XPoint(page.Width.Point - 240, y));
            gfx.DrawString($"{invoice.TotalPrice:C}", boldFont, darkBlueBrush, new XPoint(page.Width.Point - 120, y));

            // Draw Footer
            double footerY = page.Height.Point - 50;
            gfx.DrawLine(borderPen, 40, footerY, page.Width.Point - 40, footerY);
            gfx.DrawString("Bu fatura elektronik olarak üretilmiştir.", footerFont, grayBrush, new XPoint(50, footerY + 15));
            gfx.DrawString("© 2026 GameGaraj - Tüm hakları saklıdır.", footerFont, grayBrush, new XPoint(page.Width.Point - 220, footerY + 15));

            using var memoryStream = new MemoryStream();
            document.Save(memoryStream);
            return memoryStream.ToArray();
        }
    }

    public class FileFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            // Map all fonts to DejaVuSans which is guaranteed to be installed in the Docker container
            string fontName = bold ? "DejaVuSans-Bold.ttf" : "DejaVuSans.ttf";
            return new FontResolverInfo(fontName);
        }

        public byte[]? GetFont(string faceName)
        {
            string[] searchPaths = new[]
            {
                $"/usr/share/fonts/truetype/dejavu/{faceName}",
                $"/usr/share/fonts/dejavu/{faceName}",
                $"/usr/share/fonts/truetype/freefont/{faceName}",
                $"/usr/share/fonts/{faceName}",
                faceName
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllBytes(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FileFontResolver] Error reading font from {path}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"[FileFontResolver] Font NOT found: {faceName}");
            return null;
        }
    }
}

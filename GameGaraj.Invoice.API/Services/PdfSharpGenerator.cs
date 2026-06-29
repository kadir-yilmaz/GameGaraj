using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using System;
using System.IO;
using System.Globalization;
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

        private string FormatTurkishLira(decimal amount)
        {
            return amount.ToString("N2", new CultureInfo("tr-TR")) + " TL";
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

            // Define premium color palette
            var brandBlue = XColor.FromArgb(13, 110, 253);     // #0d6efd (GameGaraj Blue)
            var darkGray = XColor.FromArgb(28, 28, 28);        // #1c1c1c (Header / Brand Gray)
            var textDark = XColor.FromArgb(33, 37, 41);        // #212529 (Main Text Dark)
            var textMuted = XColor.FromArgb(108, 117, 125);    // #6c757d (Subtitles / Labels)
            var lightBg = XColor.FromArgb(248, 249, 250);      // #f8f9fa (Zebra stripe bg)
            var accentBg = XColor.FromArgb(240, 244, 255);     // Light blue bg for total box
            var borderColor = XColor.FromArgb(233, 236, 239);  // #e9ecef

            var brandBlueBrush = new XSolidBrush(brandBlue);
            var darkGrayBrush = new XSolidBrush(darkGray);
            var textDarkBrush = new XSolidBrush(textDark);
            var textMutedBrush = new XSolidBrush(textMuted);
            var lightBgBrush = new XSolidBrush(lightBg);
            var accentBgBrush = new XSolidBrush(accentBg);

            var borderPen = new XPen(borderColor, 1);
            var blueAccentPen = new XPen(brandBlue, 3); // Thick accent line

            // Define fonts (will be resolved via our FileFontResolver to DejaVuSans)
            var logoFont = new XFont("Arial", 22, XFontStyleEx.Bold);
            var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
            var sectionHeaderFont = new XFont("Arial", 11, XFontStyleEx.Bold);
            var boldFont = new XFont("Arial", 9, XFontStyleEx.Bold);
            var regularFont = new XFont("Arial", 9, XFontStyleEx.Regular);
            var footerFont = new XFont("Arial", 8, XFontStyleEx.Italic);

            double leftMargin = 50;
            double rightMargin = page.Width.Point - 50;
            double contentWidth = page.Width.Point - 100;

            // 1. HEADER SECTION
            // Logo / Brand
            gfx.DrawString("GAMEGARAJ", logoFont, brandBlueBrush, new XPoint(leftMargin, 55));
            gfx.DrawString("Performansın Adresi", footerFont, textMutedBrush, new XPoint(leftMargin, 70));

            // Document Title & Metadata (Right side)
            var titleRect = new XRect(rightMargin - 200, 35, 200, 20);
            gfx.DrawString("E-FATURA", titleFont, darkGrayBrush, titleRect, XStringFormats.TopRight);

            var metaFont = new XFont("Arial", 8, XFontStyleEx.Regular);
            var metaRect1 = new XRect(rightMargin - 200, 58, 200, 15);
            var metaRect2 = new XRect(rightMargin - 200, 71, 200, 15);
            gfx.DrawString($"Fatura No: {invoice.InvoiceNumber}", metaFont, textDarkBrush, metaRect1, XStringFormats.TopRight);
            gfx.DrawString($"Tarih: {invoice.OrderDate:dd.MM.yyyy HH:mm}", metaFont, textDarkBrush, metaRect2, XStringFormats.TopRight);

            // Accent Divider Line
            gfx.DrawLine(blueAccentPen, leftMargin, 90, rightMargin, 90);

            // 2. BILLING & COMPANY DETAILS (Two Columns)
            double detailsY = 110;
            
            // Left Column: Bill To
            gfx.DrawString("FATURA ALICISI", sectionHeaderFont, darkGrayBrush, new XPoint(leftMargin, detailsY));
            gfx.DrawString(invoice.CustomerName, boldFont, textDarkBrush, new XPoint(leftMargin, detailsY + 18));
            gfx.DrawString(invoice.CustomerEmail, regularFont, textDarkBrush, new XPoint(leftMargin, detailsY + 32));

            // Right Column: Seller Info
            double rightColX = leftMargin + 260;
            gfx.DrawString("FİRMA BİLGİLERİ", sectionHeaderFont, darkGrayBrush, new XPoint(rightColX, detailsY));
            gfx.DrawString("GameGaraj Teknoloji A.Ş.", boldFont, textDarkBrush, new XPoint(rightColX, detailsY + 18));
            gfx.DrawString("info@gamegaraj.com | 0850 222 5555", regularFont, textDarkBrush, new XPoint(rightColX, detailsY + 32));

            // 3. ITEMS TABLE
            double y = 175;
            double rowHeight = 28;

            // Table Header Bar (Dark background)
            gfx.DrawRectangle(darkGrayBrush, leftMargin, y, contentWidth, 24);
            
            // Header Labels
            gfx.DrawString("Ürün Açıklaması", boldFont, XBrushes.White, new XPoint(leftMargin + 10, y + 15));
            var headerPriceRect = new XRect(rightMargin - 130, y, 120, 24);
            gfx.DrawString("Fiyat", boldFont, XBrushes.White, headerPriceRect, XStringFormats.CenterRight);

            y += 24;

            // Draw Items
            int index = 0;
            foreach (var item in invoice.Items)
            {
                // Zebra striping background
                if (index % 2 == 1)
                {
                    gfx.DrawRectangle(lightBgBrush, leftMargin, y, contentWidth, rowHeight);
                }
                
                // Border bottom
                gfx.DrawLine(borderPen, leftMargin, y + rowHeight, rightMargin, y + rowHeight);

                // Handle product name wrapping / truncation to avoid overlapping
                string name = item.ProductName;
                double maxNameWidth = contentWidth - 140; // Allow 130pt for price column
                
                // Measure string width and wrap to 2 lines if needed
                if (name.Length > 55)
                {
                    string line1 = name.Substring(0, 55);
                    string line2 = name.Substring(55);
                    if (line2.Length > 55) line2 = line2.Substring(0, 52) + "...";

                    gfx.DrawString(line1, regularFont, textDarkBrush, new XPoint(leftMargin + 10, y + 11));
                    gfx.DrawString(line2, regularFont, textMutedBrush, new XPoint(leftMargin + 10, y + 22));
                }
                else
                {
                    gfx.DrawString(name, regularFont, textDarkBrush, new XPoint(leftMargin + 10, y + 17));
                }

                // Price (Right Aligned using XRect)
                var itemPriceRect = new XRect(rightMargin - 130, y, 120, rowHeight);
                gfx.DrawString(FormatTurkishLira(item.Price), regularFont, textDarkBrush, itemPriceRect, XStringFormats.CenterRight);

                y += rowHeight;
                index++;
            }

            // 4. SUMMARY BOX (TOTAL)
            y += 15;
            double totalBoxWidth = 220;
            double totalBoxHeight = 40;
            double totalBoxX = rightMargin - totalBoxWidth;

            // Draw total box background & border
            gfx.DrawRectangle(accentBgBrush, totalBoxX, y, totalBoxWidth, totalBoxHeight);
            gfx.DrawRectangle(new XPen(brandBlue, 1), totalBoxX, y, totalBoxWidth, totalBoxHeight);

            // Total Text
            var totalLabelRect = new XRect(totalBoxX + 15, y, 100, totalBoxHeight);
            var totalPriceRect = new XRect(rightMargin - 130, y, 120, totalBoxHeight);
            
            gfx.DrawString("TOPLAM TUTAR:", boldFont, textDarkBrush, totalLabelRect, XStringFormats.CenterLeft);
            gfx.DrawString(FormatTurkishLira(invoice.TotalPrice), titleFont, brandBlueBrush, totalPriceRect, XStringFormats.CenterRight);

            // 5. FOOTER SECTION
            double footerY = page.Height.Point - 50;
            gfx.DrawLine(borderPen, leftMargin, footerY, rightMargin, footerY);
            
            gfx.DrawString("Bu fatura 5070 sayılı Elektronik İmza Kanunu uyarınca e-imza ile imzalanmıştır.", footerFont, textMutedBrush, new XPoint(leftMargin, footerY + 15));
            gfx.DrawString("© 2026 GameGaraj - www.gamegaraj.com", footerFont, textMutedBrush, new XRect(rightMargin - 200, footerY + 15, 200, 15), XStringFormats.TopRight);

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

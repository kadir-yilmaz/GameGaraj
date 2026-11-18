using GameGaraj.Invoice.API.Models;

namespace GameGaraj.Invoice.API.Services;

/// <summary>
/// Generates invoice PDF documents.
/// </summary>
public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceData invoice);
}

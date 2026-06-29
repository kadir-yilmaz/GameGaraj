namespace GameGaraj.Shared.Events
{
    /// <summary>
    /// Go Notification Service tarafından tüketilecek genel bildirim event modeli.
    /// Farklı mikroservisler e-posta veya SMS göndermek için bu eventi yayınlar.
    /// </summary>
    public class SendNotification
    {
        public string Recipient { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Email" veya "SMS"
        public string Title { get; set; } = string.Empty; // E-posta Konusu
        public string Body { get; set; } = string.Empty; // HTML veya Düz Metin
        public string? AttachmentPath { get; set; } // MinIO üzerindeki dosya yolu (örn: invoices/INV-1.pdf)
        public string? AttachmentName { get; set; } // E-postaya eklenecek dosya adı
    }
}

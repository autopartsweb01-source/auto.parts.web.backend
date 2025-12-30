namespace AutoParts.Api.Domain;

public class OrderTimeline
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public string Action { get; set; }   // Approved, Delivered, PaymentPaid etc.
    public string? Notes { get; set; }
    public int? PerformedByUserId { get; set; } // Admin or User
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

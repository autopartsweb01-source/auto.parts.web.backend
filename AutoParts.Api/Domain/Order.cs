using OfficeOpenXml.Export.HtmlExport.StyleCollectors.StyleContracts;

namespace AutoParts.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    
    // Snapshot fields
    public required string CustomerName { get; set; }
    public required string CustomerPhone { get; set; }

    public decimal Total { get; set; }
    public required string PaymentMethod { get; set; }   // COD | Card | UPI
    public required string PaymentStatus { get; set; } // Success | COD
    public required string Status { get; set; }   // Placed | OutForDelivery | Completed
    
    public string? DeliveryOtp { get; set; }

    public required string Address { get; set; }
    
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
    public string? UpiTxnRef { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
}

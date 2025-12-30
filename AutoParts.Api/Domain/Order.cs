using OfficeOpenXml.Export.HtmlExport.StyleCollectors.StyleContracts;

namespace AutoParts.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMode { get; set; }   // COD | UPI_INTENT | RAZORPAY_UPI
    public string PaymentStatus { get; set; } // Pending | Paid | Failed
    public string OrderStatus { get; set; }   // Placed | Approved | OutForDelivery | Delivered
    public string Address { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
    public string? UpiTxnRef { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
}

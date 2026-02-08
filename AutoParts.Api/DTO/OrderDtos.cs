namespace AutoParts.Api.DTO;

public class PlaceOrderRequest
{
    public string CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerPhone { get; set; }
    public string PaymentMethod { get; set; }
    public decimal Total { get; set; }
    public List<PlaceOrderItemDto> Items { get; set; }
}

public class PlaceOrderItemDto
{
    public string ProductId { get; set; } // String to match frontend, try parse to int
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdateOrderStatusRequest
{
    public string Status { get; set; }
    public string? Otp { get; set; }
}

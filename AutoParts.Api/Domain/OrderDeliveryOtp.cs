namespace AutoParts.Api.Domain;

public class OrderDeliveryOtp
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public string OtpHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsVerified { get; set; }
}

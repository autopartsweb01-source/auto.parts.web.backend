namespace AutoParts.Api.Services;

public interface IAdminOrderService
{
    Task<object> GetAllOrders(string? status, int page, int size);
    Task<object> Approve(int orderId);
    Task<object> MarkOutForDelivery(int orderId);
    Task<object> GenerateDeliveryOtp(int orderId);
    Task<object> VerifyDeliveryOtp(int orderId, string otp);
    Task<object> MarkDelivered(int orderId);
    Task<object> CancelOrder(int orderId, string reason);
}


using AutoParts.Api.DTO;

namespace AutoParts.Api.Services;

public interface IOrderService
{
    Task<object> Checkout(int userId, CheckoutRequest req);

    Task<object> ConfirmRazorpayPayment(int userId, RazorpayConfirmRequest req);

    Task<object> ConfirmUpiIntent(int userId, UpiIntentConfirmRequest req);

    Task<object> GetMyOrders(int userId, int page, int size);

    Task<object> GetOrderDetails(int userId, int orderId);
}

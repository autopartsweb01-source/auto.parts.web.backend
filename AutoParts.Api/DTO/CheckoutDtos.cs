public record CheckoutRequest(string Address, string PaymentMode);
public record RazorpayConfirmRequest(int OrderId, string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);
public record UpiIntentConfirmRequest(int OrderId, string TxnRef);

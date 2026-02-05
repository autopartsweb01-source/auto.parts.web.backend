namespace AutoParts.Api.DTO;

public record RazorpayConfirmRequest(int OrderId, string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);
public record UpiIntentConfirmRequest(int OrderId, string TxnRef);

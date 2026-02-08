using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;

namespace AutoParts.Api.Services;

public class RazorpayService
{
    private readonly string _key;
    private readonly string _secret;

    public RazorpayService(IConfiguration config)
    {
        _key = config["Razorpay:Key"] ?? throw new InvalidOperationException("Razorpay:Key is missing in configuration.");
        _secret = config["Razorpay:Secret"] ?? throw new InvalidOperationException("Razorpay:Secret is missing in configuration.");
    }

    public Order CreateOrder(decimal amount, string receipt)
    {
        var client = new RazorpayClient(_key, _secret);

        var options = new Dictionary<string, object>
        {
            { "amount", (int)(amount * 100) },
            { "currency", "INR" },
            { "receipt", receipt },
            { "payment_capture", 1 }
        };

        return client.Order.Create(options);
    }

    public bool VerifySignature(string orderId, string paymentId, string signature)
    {
        var payload = $"{orderId}|{paymentId}";
        var secretBytes = Encoding.UTF8.GetBytes(_secret);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var generated = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return generated == signature.ToLower();
    }
}

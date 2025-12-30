using AutoParts.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("payments/webhook")]
public class PaymentsWebhookController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsWebhookController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        var json = JObject.Parse(body);
        var eventType = json["event"]?.ToString();

        if (eventType == "payment.captured")
        {
            var orderId = json["payload"]["payment"]["entity"]["order_id"]?.ToString();
            var paymentId = json["payload"]["payment"]["entity"]["id"]?.ToString();

            var order = _db.Orders.FirstOrDefault(x => x.GatewayOrderId == orderId);
            if (order != null)
            {
                order.PaymentStatus = "Paid";
                order.GatewayPaymentId = paymentId;
                await _db.SaveChangesAsync();
            }
        }

        return Ok();
    }
}


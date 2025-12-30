using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
[Authorize(Roles = "User")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }
    private int UserId() =>
        int.Parse(User.Claims.First(x => x.Type == "id").Value);

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest req)
        => Ok(await _orders.Checkout(UserId(), req));

    [HttpPost("razorpay/confirm")]
    public async Task<IActionResult> ConfirmRazorpay(RazorpayConfirmRequest req)
        => Ok(await _orders.ConfirmRazorpayPayment(UserId(), req));

    [HttpPost("upi-intent/confirm")]
    public async Task<IActionResult> ConfirmUpiIntent(UpiIntentConfirmRequest req)
        => Ok(await _orders.ConfirmUpiIntent(UserId(), req));

    [HttpGet("my")]
    public async Task<IActionResult> MyOrders(int page = 1, int size = 10)
        => Ok(await _orders.GetMyOrders(UserId(), page, size));

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(int id)
        => Ok(await _orders.GetOrderDetails(UserId(), id));
}

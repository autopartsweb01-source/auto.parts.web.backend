using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly ICartService _cartService;
    private readonly IAdminOrderService _adminOrderService;

    public OrdersController(IOrderService orders, ICartService cartService, IAdminOrderService adminOrderService)
    {
        _orders = orders;
        _cartService = cartService;
        _adminOrderService = adminOrderService;
    }
    private int UserId()
    {
        // Try standard "sub" or "nameidentifier"
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                 ?? User.FindFirst("id");
                 
        if (claim != null && int.TryParse(claim.Value, out int id))
            return id;
            
        return 0;
    }

    // ---------- PLACE ORDER (Direct) ----------
    // Maps to /api/order/place in frontend (via proxy or direct)
    // BUT frontend says `${API_BASE_URL}/order/place` -> http://localhost:5221/order/place
    // So we need [Route("api/order")] or [Route("order")] on the controller or specific route here.
    // Existing controller has [Route("orders")] -> /orders
    // Let's add specific route to match frontend expectation exactly.
    [HttpPost("/order/place")]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest req)
    {
        var userId = UserId();
        if (userId == 0) return Unauthorized();

        // 1. Clear existing cart
        await _cartService.ClearCart(userId);

        // 2. Add items to cart
        var cartItems = new List<(int productId, int qty)>();
        foreach (var item in req.Items)
        {
             // Frontend sends ProductId as string, try parse
            if (int.TryParse(item.ProductId, out int pid))
            {
                cartItems.Add((pid, item.Qty));
            }
        }

        if (!cartItems.Any())
            return BadRequest(new { success = false, message = "No valid products in order" });

        await _cartService.BulkUpdate(userId, cartItems);

        // 3. Checkout
        try
        {
            // Pass null for address so OrderService uses user's profile address
            var result = await _orders.Checkout(userId, new CheckoutRequest(null!, req.PaymentMethod));
            
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ---------- UPDATE STATUS (Delivery/Admin) ----------
    // Frontend: `${API_BASE_URL}/api/order/${orderId}/status`
    [HttpPut("/api/order/{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest req)
    {
        try
        {
            object result = null;
            switch (req.Status)
            {
                case "OutForDelivery":
                    await _adminOrderService.MarkOutForDelivery(id);
                    result = await _adminOrderService.GenerateDeliveryOtp(id);
                    break;
                case "Completed":
                case "Delivered":
                    if (!string.IsNullOrEmpty(req.Otp))
                        result = await _adminOrderService.VerifyDeliveryOtp(id, req.Otp);
                    else
                        result = await _adminOrderService.MarkDelivered(id);
                    break;
                default:
                    return BadRequest(new { message = "Invalid status" });
            }
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }


    // [HttpPost("place")] // Removed duplicate placeholder


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

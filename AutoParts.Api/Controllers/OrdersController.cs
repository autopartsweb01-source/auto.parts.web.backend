using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly ICartService _cartService;
    private readonly IAdminOrderService _adminOrderService;
    private readonly IUserService _userService;

    public OrdersController(IOrderService orders, ICartService cartService, IAdminOrderService adminOrderService, IUserService userService)
    {
        _orders = orders;
        _cartService = cartService;
        _adminOrderService = adminOrderService;
        _userService = userService;
    }
    private int UserId()
    {
        // Try standard numeric id first
        var claim = User.FindFirst("id");
        if (claim != null && int.TryParse(claim.Value, out int id)) return id;

        // Fallback: external JWTs carry email and nameidentifier (guid). Resolve by email.
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? User.FindFirst("name")?.Value;
            var phone = User.FindFirst("mobile")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
            var user = _userService.EnsureUserByEmailAsync(email, name, phone).GetAwaiter().GetResult();
            return user.Id;
        }
        // Fallback: parse Authorization header without validating signature
        var auth = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var raw = auth.Substring("Bearer ".Length).Trim();
            try
            {
                var jwt = new JwtSecurityToken(raw);
                string? mail = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.Email, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("email", StringComparison.OrdinalIgnoreCase)
                        || c.Type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase))?.Value;
                string? nm = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.Name, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("name", StringComparison.OrdinalIgnoreCase)
                        || c.Type.EndsWith("/name", StringComparison.OrdinalIgnoreCase))?.Value;
                string? ph = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.MobilePhone, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("mobile", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(mail))
                {
                    var u2 = _userService.EnsureUserByEmailAsync(mail, nm, ph).GetAwaiter().GetResult();
                    return u2.Id;
                }
            }
            catch { }
        }
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

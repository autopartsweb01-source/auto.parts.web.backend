using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static AutoParts.Api.DTO.CartDtos;
using System.IdentityModel.Tokens.Jwt;

[ApiController]
[Route("cart")]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    private readonly IUserService _users;

    public CartController(ICartService cart, IUserService users)
    {
        _cart = cart;
        _users = users;
    }

    private int GetUserId()
    {
        var idClaim = User.Claims.FirstOrDefault(x => x.Type == "id")?.Value;
        if (int.TryParse(idClaim, out var uid)) return uid;

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? User.FindFirst("name")?.Value;
            var phone = User.FindFirst("mobile")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
            var u = _users.EnsureUserByEmailAsync(email, name, phone).GetAwaiter().GetResult();
            return u.Id;
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
                    var u2 = _users.EnsureUserByEmailAsync(mail, nm, ph).GetAwaiter().GetResult();
                    return u2.Id;
                }
            }
            catch { }
        }
        return 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart() =>
        Ok(await _cart.GetCartSummary(GetUserId()));

    [HttpPost("add")]
    public async Task<IActionResult> Add(AddToCartRequest r) =>
        Ok(await _cart.AddToCart(GetUserId(), r.ProductId, r.Qty));

    [HttpPost("decrease/{productId}")]
    public async Task<IActionResult> Decrease(int productId) =>
        Ok(await _cart.DecreaseQty(GetUserId(), productId));

    [HttpPut("update")]
    public async Task<IActionResult> BulkUpdate(BulkUpdateRequest r) =>
        Ok(await _cart.BulkUpdate(GetUserId(),
            r.Items.Select(x => (x.ProductId, x.Qty)).ToList()));

    [HttpDelete("remove/{productId}")]
    public async Task<IActionResult> Remove(int productId) =>
        Ok(await _cart.RemoveItem(GetUserId(), productId));

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear() =>
        Ok(await _cart.ClearCart(GetUserId()));
}

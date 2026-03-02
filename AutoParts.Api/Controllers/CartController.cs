using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static AutoParts.Api.DTO.CartDtos;

[ApiController]
[Route("cart")]
[Authorize]
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
            var u = _users.GetUserByEmailAsync(email).GetAwaiter().GetResult();
            if (u != null) return u.Id;
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

using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

[ApiController]
[Route("vendor/checkout")]
public class VendorController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IUserService _users;
    public VendorController(IOrderService orders, IUserService users)
    {
        _orders = orders;
        _users = users;
    }

    private int UserId()
    {
        var claim = User.FindFirst("id");
        if (claim != null && int.TryParse(claim.Value, out int id)) return id;
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

    [HttpPost("init")]
    public async Task<IActionResult> Init(VendorCheckoutInitRequest req)
        => Ok(await _orders.VendorInitCheckout(UserId(), req));

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(VendorVerifyOtpRequest req)
        => Ok(await _orders.VendorVerifyOtp(UserId(), req));
}

using AutoParts.Api.Auth;
using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly TwilioOtpService _otp;

    public AuthController(AppDbContext db, JwtTokenService jwt, TwilioOtpService otp)
    {
        _db = db;
        _jwt = jwt;
        _otp = otp;
    }

    private static string NormalizePhone(string phone)
    {
        phone = phone.Trim();
        return phone.StartsWith("+91") ? phone : $"+91{phone}";
    }

    // ---------- SEND OTP ----------
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        var phone = NormalizePhone(request.Phone);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone)
                   ?? new User
                   {
                       PhoneNumber = phone,
                       Name = "User",
                       Role = "User"
                   };

        var otp = new Random().Next(100000, 999999).ToString();

        user.OtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);

        if (user.Id == 0)
            _db.Users.Add(user);

        await _db.SaveChangesAsync();

        await _otp.SendSmsOtpAsync(phone, otp);

        return Ok(new { message = "OTP sent successfully" });
    }

    // ---------- VERIFY OTP ----------
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var phone = NormalizePhone(request.Phone);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        if (user == null)
            return Unauthorized("User not found");

        if (user.OtpExpiry == null || user.OtpExpiry < DateTime.UtcNow)
            return Unauthorized("OTP expired");

        if (!BCrypt.Net.BCrypt.Verify(request.Code, user.OtpHash))
            return Unauthorized("Invalid OTP");

        var token = _jwt.Generate(user);

        user.OtpHash = null;
        user.OtpExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            token,
            user = new { user.Id, user.PhoneNumber, user.Role },
            message = "OTP verified successfully"
        });
    }
}

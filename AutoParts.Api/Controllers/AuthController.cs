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
    private readonly EmailService _email;

    public AuthController(AppDbContext db, JwtTokenService jwt, TwilioOtpService otp, EmailService email)
    {
        _db = db;
        _jwt = jwt;
        _otp = otp;
        _email = email;
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

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Phone == phone);

        if (user == null)
        {
            user = new User
            {
                Phone = phone,
                FullName = "User",
                Role = "customer",
                Email = request.Email ?? ""
            };
            _db.Users.Add(user);
        }
        else if (!string.IsNullOrEmpty(request.Email))
        {
            // Update email if provided
            user.Email = request.Email;
        }

        var otp = new Random().Next(100000, 999999).ToString();

        user.OtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);

        await _db.SaveChangesAsync();

        // Send SMS
        await _otp.SendSmsOtpAsync(phone, otp);

        // Send Email (if available)
        if (!string.IsNullOrEmpty(user.Email))
        {
            await _email.SendOtpEmailAsync(user.Email, otp);
        }

        return Ok(new { message = "OTP sent successfully" });
    }

    // ---------- VERIFY OTP ----------
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var phone = NormalizePhone(request.Phone);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Phone == phone);
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
            user = new { user.Id, mobile = user.Phone, user.Role, name = user.FullName, user.Email, user.Location },
            message = "OTP verified successfully"
        });
    }

    // ---------- REGISTER (EMAIL/PASSWORD) ----------
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest("Email already exists");

        var normalizedPhone = NormalizePhone(request.Mobile);
        if (await _db.Users.AnyAsync(u => u.Phone == normalizedPhone))
            return BadRequest("Phone already exists");

        var user = new User
        {
            FullName = request.Name,
            Email = request.Email,
            Phone = normalizedPhone,
            Location = request.Location,
            Role = "customer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.Generate(user);

        return Ok(new
        {
            token,
            user = new { user.Id, mobile = user.Phone, user.Role, name = user.FullName, user.Email, user.Location },
            message = "Registration successful"
        });
    }

    // ---------- LOGIN (EMAIL/PHONE + PASSWORD) ----------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Try to find by Email OR Phone
        // Note: request.Email in DTO can carry either Email or Phone string
        // We might want to rename DTO property to 'Identifier', but for now we reuse 'Email' field
        
        var identifier = request.Email; 
        var normalizedPhone = NormalizePhone(identifier); 

        var user = await _db.Users.FirstOrDefaultAsync(u => 
            u.Email == identifier || u.Phone == normalizedPhone || u.Phone == identifier
        );

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var token = _jwt.Generate(user);

        return Ok(new
        {
            token,
            user = new { user.Id, mobile = user.Phone, user.Role, name = user.FullName, user.Email, user.Location },
            message = "Login successful"
        });
    }

    // ---------- FORGOT PASSWORD ----------
    [HttpPost("forgot-password")]
    [HttpPost("/forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return Ok(new { message = "If email exists, reset link sent" }); // Security: don't reveal existence

        var token = Guid.NewGuid().ToString("N");
        user.ResetToken = token;
        user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var resetLink = $"http://localhost:4200/reset-password?email={user.Email}&token={token}";
        await _email.SendResetPasswordEmailAsync(user.Email, resetLink);

        return Ok(new { message = "If email exists, reset link sent" });
    }

    // ---------- RESET PASSWORD ----------
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || user.ResetToken != request.Token || user.ResetTokenExpiry < DateTime.UtcNow)
            return BadRequest("Invalid or expired token");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successful" });
    }
}

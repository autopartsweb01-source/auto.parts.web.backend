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
        var normalizedPhone = NormalizePhone(request.Mobile);

        var existingByEmail = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingByEmail != null)
        {
            if (existingByEmail.IsEmailConfirmed)
                return BadRequest("Email already exists");

            var resendToken = Guid.NewGuid().ToString("N");
            existingByEmail.EmailConfirmationToken = resendToken;
            existingByEmail.EmailConfirmationExpiry = DateTime.UtcNow.AddDays(1);

            await _db.SaveChangesAsync();

            var resendLink = $"http://localhost:5221/auth/confirm-email?token={resendToken}";
            await _email.SendRegistrationConfirmationEmailAsync(existingByEmail.Email, existingByEmail.FullName, resendLink);

            return Ok(new { message = "Account already registered but not confirmed. Confirmation email resent." });
        }

        var existingByPhone = await _db.Users.FirstOrDefaultAsync(u => u.Phone == normalizedPhone);
        if (existingByPhone != null)
        {
            if (existingByPhone.IsEmailConfirmed)
                return BadRequest("Phone already exists");

            existingByPhone.Email = request.Email;
            existingByPhone.FullName = request.Name;
            existingByPhone.Location = request.Location;
            existingByPhone.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var resendToken = Guid.NewGuid().ToString("N");
            existingByPhone.EmailConfirmationToken = resendToken;
            existingByPhone.EmailConfirmationExpiry = DateTime.UtcNow.AddDays(1);

            await _db.SaveChangesAsync();

            var resendLink = $"http://localhost:5221/auth/confirm-email?token={resendToken}";
            await _email.SendRegistrationConfirmationEmailAsync(existingByPhone.Email, existingByPhone.FullName, resendLink);

            return Ok(new { message = "Account already registered but not confirmed. Confirmation email resent." });
        }

        var token = Guid.NewGuid().ToString("N");

        var user = new User
        {
            FullName = request.Name,
            Email = request.Email,
            Phone = normalizedPhone,
            Location = request.Location,
            Role = "customer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailConfirmed = false,
            EmailConfirmationToken = token,
            EmailConfirmationExpiry = DateTime.UtcNow.AddDays(1)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var confirmLink = $"http://localhost:5221/auth/confirm-email?token={token}";
        await _email.SendRegistrationConfirmationEmailAsync(user.Email, user.FullName, confirmLink);

        return Ok(new
        {
            message = "Registration successful, please confirm your email"
        });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid token");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);
        if (user == null)
            return BadRequest("Invalid token");

        if (user.EmailConfirmationExpiry != null && user.EmailConfirmationExpiry < DateTime.UtcNow)
            return BadRequest("Token expired");

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationExpiry = null;

        await _db.SaveChangesAsync();

        var html = @"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"" />
  <title>Email confirmed</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background-color: #0f172a; margin: 0; padding: 0; color: #e5e7eb; }
    .wrapper { width: 100%; min-height: 100vh; display: flex; align-items: center; justify-content: center; }
    .card { max-width: 420px; width: 100%; background: #020617; border-radius: 16px; padding: 32px 28px; box-shadow: 0 10px 30px rgba(15,23,42,.7); text-align: center; border: 1px solid #1e293b; }
    .logo { font-size: 22px; font-weight: 700; color: #38bdf8; letter-spacing: .08em; text-transform: uppercase; margin-bottom: 10px; }
    .title { font-size: 20px; font-weight: 600; margin-bottom: 10px; }
    .text { font-size: 14px; color: #9ca3af; margin-bottom: 4px; }
  </style>
</head>
<body>
  <div class=""wrapper"">
    <div class=""card"">
      <div class=""logo"">Radhe Shyam Medical</div>
      <div class=""title"">Email verified</div>
      <p class=""text"">Your email has been confirmed successfully.</p>
      <p class=""text"">You can now close this tab and log in from the app.</p>
    </div>
  </div>
</body>
</html>";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = 200
        };
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

        if (!user.IsEmailConfirmed)
        {
            if (!string.IsNullOrEmpty(user.Email))
            {
                var needsNewToken = string.IsNullOrWhiteSpace(user.EmailConfirmationToken)
                    || user.EmailConfirmationExpiry == null
                    || user.EmailConfirmationExpiry < DateTime.UtcNow;

                if (needsNewToken)
                {
                    var confirmToken = Guid.NewGuid().ToString("N");
                    user.EmailConfirmationToken = confirmToken;
                    user.EmailConfirmationExpiry = DateTime.UtcNow.AddDays(1);
                    await _db.SaveChangesAsync();
                }

                var confirmLink = $"http://localhost:5221/auth/confirm-email?token={user.EmailConfirmationToken}";
                await _email.SendRegistrationConfirmationEmailAsync(user.Email, user.FullName, confirmLink);
            }

            return Unauthorized("Please confirm your email. We have sent a confirmation link to your email address.");
        }

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

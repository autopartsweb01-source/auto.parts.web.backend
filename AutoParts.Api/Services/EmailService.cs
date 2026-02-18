using System.Net;
using System.Net.Mail;

namespace AutoParts.Api.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendDeliveryOtpEmailAsync(string toEmail, string otp, int orderId)
    {
        var host = _config["Smtp:Host"];
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var fromEmail = _config["Smtp:Email"];
        var password = _config["Smtp:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[EmailService] SMTP not configured. Skipping email.");
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Auto Parts"),
                Subject = $"Delivery OTP for Order #{orderId}",
                Body = $"Your order #{orderId} is out for delivery. \n\nPlease provide this OTP to the delivery agent: {otp}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Delivery OTP email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send delivery OTP email to {toEmail}");
        }
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        var host = _config["Smtp:Host"];
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var fromEmail = _config["Smtp:Email"];
        var password = _config["Smtp:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[EmailService] SMTP not configured. Skipping email.");
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Auto Parts"),
                Subject = "Auto Parts Login OTP",
                Body = $"Your OTP is: {otp}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"OTP email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {toEmail}");
            _logger.LogWarning($"[DEV FALLBACK] OTP for {toEmail} is: {otp}");
        }
    }

    public async Task SendResetPasswordEmailAsync(string toEmail, string resetLink)
    {
        var host = _config["Smtp:Host"];
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var fromEmail = _config["Smtp:Email"];
        var password = _config["Smtp:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[EmailService] SMTP not configured. Skipping email.");
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Auto Parts"),
                Subject = "Reset Your Password",
                Body = $"Click the link to reset your password: {resetLink}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Reset password email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send reset email to {toEmail}");
            _logger.LogWarning($"[DEV FALLBACK] Reset link for {toEmail} is: {resetLink}");
        }
    }

    public async Task SendRegistrationConfirmationEmailAsync(string toEmail, string name, string confirmationLink)
    {
        var host = _config["Smtp:Host"];
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var fromEmail = _config["Smtp:Email"];
        var password = _config["Smtp:Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[EmailService] SMTP not configured. Skipping email.");
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            var html = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"" />
  <title>Confirm your email</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background-color: #f5f7fb; margin: 0; padding: 0; }}
    .wrapper {{ width: 100%; padding: 24px 0; }}
    .card {{ max-width: 480px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px 28px; box-shadow: 0 8px 24px rgba(15, 23, 42, 0.08); }}
    .logo {{ font-size: 22px; font-weight: 700; color: #2563eb; letter-spacing: 0.04em; }}
    .title {{ margin-top: 24px; font-size: 20px; font-weight: 600; color: #111827; }}
    .text {{ margin-top: 12px; font-size: 14px; line-height: 1.6; color: #4b5563; }}
    .button {{ display: inline-block; margin-top: 24px; padding: 12px 24px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 999px; font-size: 14px; font-weight: 600; }}
    .button:hover {{ background: #1d4ed8; }}
    .footer {{ margin-top: 24px; font-size: 12px; color: #9ca3af; }}
  </style>
</head>
<body>
  <div class=""wrapper"">
    <div class=""card"">
      <div class=""logo"">Radhe Shyam Medical</div>
      <div class=""title"">Confirm your email</div>
      <p class=""text"">Hi {name},</p>
      <p class=""text"">Thank you for registering. Tap the button below to confirm your email and activate your account.</p>
      <a href=""{confirmationLink}"" class=""button"">Confirm email</a>
      <p class=""footer"">If you did not request this, you can safely ignore this email.</p>
    </div>
  </div>
</body>
</html>";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Radhe Shyam Medical"),
                Subject = "Confirm your email",
                Body = html,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Registration confirmation email sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send registration confirmation email to {toEmail}");
        }
    }
}

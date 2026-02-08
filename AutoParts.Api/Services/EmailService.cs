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
}

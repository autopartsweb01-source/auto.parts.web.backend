using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.Extensions.Logging;

namespace AutoParts.Api.Services;

public class TwilioOtpService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioOtpService> _logger;

    public TwilioOtpService(IConfiguration config, ILogger<TwilioOtpService> logger)
    {
        _config = config;
        _logger = logger;
        
        var sid = _config["Twilio:AccountSid"];
        var token = _config["Twilio:AuthToken"];

        if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(token))
        {
            try
            {
                TwilioClient.Init(sid, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Twilio client");
            }
        }
    }

    public async Task SendSmsOtpAsync(string phone, string otp)
    {
        var from = _config["Twilio:FromNumber"];
        var sid = _config["Twilio:AccountSid"];

        // Fallback for development/invalid credentials
        if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(from))
        {
            _logger.LogWarning($"[DEV MODE] Twilio not configured. OTP for {phone}: {otp}");
            return;
        }

        try
        {
            await MessageResource.CreateAsync(
                new CreateMessageOptions(new PhoneNumber(phone))
                {
                    From = new PhoneNumber(from),
                    Body = $"[AutoParts] Your OTP is {otp}"
                });
            
            _logger.LogInformation($"OTP sent to {phone}");
        }
        catch (Exception ex)
        {
            // Log the OTP so development can continue even if SMS fails
            _logger.LogError(ex, $"Failed to send SMS. [DEV FALLBACK] OTP for {phone} is: {otp}");
        }
    }
}


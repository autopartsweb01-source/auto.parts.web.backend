using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace AutoParts.Api.Services;

public class TwilioOtpService
{
    private readonly IConfiguration _config;

    public TwilioOtpService(IConfiguration config)
    {
        _config = config;
        TwilioClient.Init(
            _config["Twilio:AccountSid"],
            _config["Twilio:AuthToken"]);
    }

    public async Task SendSmsOtpAsync(string phone, string otp)
    {
        var from = _config["Twilio:FromNumber"];

        await MessageResource.CreateAsync(
            new CreateMessageOptions(new PhoneNumber(phone))
            {
                From = new PhoneNumber(from),
                Body = $"[AutoParts] Your OTP is {otp}"
            });
    }
}


using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("api/Otp")]
public class OtpController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TwilioOtpService _otpService;
    private readonly EmailService _emailService;

    public OtpController(AppDbContext db, TwilioOtpService otpService, EmailService emailService)
    {
        _db = db;
        _otpService = otpService;
        _emailService = emailService;
    }

    [HttpPost("verify")]
    public async Task<ActionResult<ApiResponse<object>>> Verify([FromBody] VerifyOtpRequestV2 request)
    {
        // Try to parse PrescriptionNo as OrderId
        if (!int.TryParse(request.PrescriptionNo?.Trim(), out int orderId))
        {
             return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid PrescriptionNo" });
        }

        // Check if it's an Order Delivery OTP
        var orderOtp = await _db.OrderOtps
            .Include(o => o.Order)
            .FirstOrDefaultAsync(o => o.OrderId == orderId && !o.IsVerified);

        if (orderOtp != null)
        {
            if (BCrypt.Net.BCrypt.Verify(request.Otp, orderOtp.OtpHash))
            {
                if (orderOtp.ExpiresAt < DateTime.UtcNow)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "OTP expired" });
                }

                orderOtp.IsVerified = true;
                await _db.SaveChangesAsync();
                return Ok(new ApiResponse<object> { Success = true, Message = "OTP verified successfully" });
            }
        }
        
        // If not found in OrderOtps, maybe it's a general User OTP (though spec implies Prescription)
        // For now, assuming Order OTP is the main use case for "PrescriptionNo".
        
        return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid OTP" });
    }

    [HttpPost("resend")]
    public async Task<ActionResult<ApiResponse<object>>> Resend([FromBody] ResendOtpRequestV2 request)
    {
        var orderId = (int)request.PrescriptionNo;
        
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
             return BadRequest(new ApiResponse<object> { Success = false, Message = "Prescription/Order not found" });
        }

        // Generate new OTP
        var otp = new Random().Next(100000, 999999).ToString();
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

        // Update or Create OrderOtp
        var existingOtp = await _db.OrderOtps.FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (existingOtp != null)
        {
            existingOtp.OtpHash = otpHash;
            existingOtp.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
            existingOtp.IsVerified = false;
        }
        else
        {
            _db.OrderOtps.Add(new OrderDeliveryOtp
            {
                OrderId = orderId,
                OtpHash = otpHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsVerified = false
            });
        }
        
        await _db.SaveChangesAsync();

        // Send SMS
        await _otpService.SendSmsOtpAsync(order.CustomerPhone, otp);

        return Ok(new ApiResponse<object> { Success = true, Message = "OTP resent successfully" });
    }
}

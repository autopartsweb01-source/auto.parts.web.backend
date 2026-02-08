using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class AdminOrderService : IAdminOrderService
{
    private readonly AppDbContext _db;
    private readonly EmailService _emailService;

    public AdminOrderService(AppDbContext db, EmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    // ---------- TIMELINE LOG ----------
    private async Task LogTimeline(int orderId, string action, string? notes = null)
    {
        _db.OrderTimelines.Add(new OrderTimeline
        {
            OrderId = orderId,
            Action = action,
            Notes = notes,
            PerformedByUserId = null // admin actions
        });

        await _db.SaveChangesAsync();
    }

    // ---------- LIST ORDERS ----------
    public async Task<object> GetAllOrders(string? status, int page, int size)
    {
        var q = _db.Orders.Include(x => x.Items)
            .OrderByDescending(x => x.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync();

        var items = await q.Skip((page - 1) * size).Take(size)
            .Select(o => new
            {
                o.Id,
                o.UserId,
                o.CustomerName,
                o.CustomerPhone,
                o.Address,
                o.Total,
                o.PaymentMethod,
                o.PaymentStatus,
                o.Status,
                o.CreatedAt
            }).ToListAsync();

        return new { page, size, total, orders = items };
    }

    // ---------- APPROVE ----------
    public async Task<object> Approve(int orderId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        if (o == null) throw new Exception("Order not found");
        o.Status = "Approved";
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Order Approved");
        return new { message = "Order approved" };
    }

    // ---------- OUT FOR DELIVERY ----------
    public async Task<object> MarkOutForDelivery(int orderId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        if (o == null) throw new Exception("Order not found");
        o.Status = "OutForDelivery";
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Out For Delivery");
        return new { message = "Order out for delivery" };
    }

    // ---------- GENERATE DELIVERY OTP ----------
    public async Task<object> GenerateDeliveryOtp(int orderId)
    {
        var otp = new Random().Next(100000, 999999).ToString();

        // Save plain OTP to Order for User display
        var order = await _db.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.DeliveryOtp = otp;
        }

        var entry = new OrderDeliveryOtp
        {
            OrderId = orderId,
            OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsVerified = false
        };

        _db.OrderOtps.Add(entry);
        await _db.SaveChangesAsync();

        Console.WriteLine($"Delivery OTP {otp} for order {orderId}");

        // Fetch user email to send OTP
        var user = await _db.Users.FindAsync(order.UserId);
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            await _emailService.SendDeliveryOtpEmailAsync(user.Email, otp, orderId);
        }

        await LogTimeline(orderId, "Delivery OTP Generated");
        return new { message = "OTP generated & sent via email" };
    }

    // ---------- VERIFY DELIVERY OTP ----------
    public async Task<object> VerifyDeliveryOtp(int orderId, string otp)
    {
        var row = await _db.OrderOtps
            .Where(x => x.OrderId == orderId && !x.IsVerified)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (row == null || row.ExpiresAt < DateTime.UtcNow)
            throw new Exception("OTP expired");

        if (!BCrypt.Net.BCrypt.Verify(otp, row.OtpHash))
            throw new Exception("Invalid OTP");

        row.IsVerified = true;
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Delivery OTP Verified");
        return new { message = "OTP verified" };
    }

    // ---------- DELIVERED ----------
    public async Task<object> MarkDelivered(int orderId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        if (o == null) throw new Exception("Order not found");
        o.Status = "Delivered";
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Order Delivered");
        return new { message = "Order delivered" };
    }

    // ---------- CANCEL + STOCK RESTORE ----------
    public async Task<object> CancelOrder(int orderId, string reason)
    {
        var o = await _db.Orders
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .FirstAsync(x => x.Id == orderId);

        o.IsCancelled = true;
        o.CancelReason = reason;
        o.Status = "Cancelled";

        foreach (var i in o.Items)
            i.Product.Quantity += i.Qty;

        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Order Cancelled", reason);
        return new { message = "Order cancelled & stock restored" };
    }
}

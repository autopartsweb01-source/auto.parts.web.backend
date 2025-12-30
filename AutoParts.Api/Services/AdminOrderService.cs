using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class AdminOrderService : IAdminOrderService
{
    private readonly AppDbContext _db;

    public AdminOrderService(AppDbContext db)
    {
        _db = db;
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
            q = q.Where(x => x.OrderStatus == status);

        var total = await q.CountAsync();

        var items = await q.Skip((page - 1) * size).Take(size)
            .Select(o => new
            {
                o.Id,
                o.UserId,
                o.TotalAmount,
                o.PaymentStatus,
                o.OrderStatus,
                o.CreatedAt
            }).ToListAsync();

        return new { page, size, total, orders = items };
    }

    // ---------- APPROVE ----------
    public async Task<object> Approve(int orderId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        o.OrderStatus = "Approved";
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Order Approved");
        return new { message = "Order approved" };
    }

    // ---------- OUT FOR DELIVERY ----------
    public async Task<object> MarkOutForDelivery(int orderId)
    {
        var o = await _db.Orders.FindAsync(orderId);
        o.OrderStatus = "OutForDelivery";
        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Out For Delivery");
        return new { message = "Order out for delivery" };
    }

    // ---------- GENERATE DELIVERY OTP ----------
    public async Task<object> GenerateDeliveryOtp(int orderId)
    {
        var otp = new Random().Next(100000, 999999).ToString();

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

        await LogTimeline(orderId, "Delivery OTP Generated");
        return new { message = "OTP generated & sent" };
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
        o.OrderStatus = "Delivered";
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
        o.OrderStatus = "Cancelled";

        foreach (var i in o.Items)
            i.Product.StockQty += i.Qty;

        await _db.SaveChangesAsync();

        await LogTimeline(orderId, "Order Cancelled", reason);
        return new { message = "Order cancelled & stock restored" };
    }
}

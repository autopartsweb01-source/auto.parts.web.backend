using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ICartService _cart;
    private readonly RazorpayService _razorpay;

    public OrderService(AppDbContext db, ICartService cart, RazorpayService razorpay)
    {
        _db = db;
        _cart = cart;
        _razorpay = razorpay;
    }

    // ---------- TIMELINE LOG ----------
    private async Task LogTimeline(int orderId, string action, string? notes = null, int? userId = null)
    {
        _db.OrderTimelines.Add(new OrderTimeline
        {
            OrderId = orderId,
            Action = action,
            Notes = notes,
            PerformedByUserId = userId
        });

        await _db.SaveChangesAsync();
    }

    // ---------- CHECKOUT ----------
    public async Task<object> Checkout(int userId, CheckoutRequest req)
    {
        var cart = await _cart.GetOrCreateCart(userId);
        var user = await _db.Users.FindAsync(userId);

        if (!cart.Items.Any())
            throw new Exception("Cart is empty");

        foreach (var i in cart.Items)
            if (i.Qty > i.Product.Quantity)
                throw new Exception($"Insufficient stock for {i.Product.Title}");

        var total = cart.Items.Sum(x => x.UnitPrice * x.Qty);

        var order = new Order
        {
            UserId = userId,
            CustomerName = user?.FullName ?? "Unknown",
            CustomerPhone = user?.Phone ?? "Unknown",
            Address = !string.IsNullOrEmpty(req.Address) ? req.Address : (user?.Address ?? user?.Location ?? "Unknown Address"),
            Total = total,
            Status = "Placed",
            PaymentStatus = "Pending",
            PaymentMethod = req.PaymentMethod
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await LogTimeline(order.Id, "Order Placed", null, userId);

        foreach (var i in cart.Items)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = i.ProductId,
                Qty = i.Qty,
                Price = i.UnitPrice
            });

            i.Product.Quantity -= i.Qty;
        }

        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync();

        string? upiIntent = null;

        if (req.PaymentMethod == "UPI_INTENT")
        {
            upiIntent =
                $"upi://pay?pa=merchant@upi&pn=AutoParts&am={total}&cu=INR&tn=Order%20{order.Id}";
            await LogTimeline(order.Id, "UPI Intent Generated");
        }

        if (req.PaymentMethod == "RAZORPAY_UPI")
        {
            var rp = _razorpay.CreateOrder(total, $"ORD-{order.Id}");
            order.GatewayOrderId = rp["id"];
            await _db.SaveChangesAsync();
            await LogTimeline(order.Id, "Razorpay Order Created", order.GatewayOrderId);
        }

        return new
        {
            order.Id,
            order.Total,
            order.PaymentMethod,
            order.PaymentStatus,
            order.Status,
            razorpayOrderId = order.GatewayOrderId,
            upiIntent
        };
    }

    // ---------- RAZORPAY PAYMENT CONFIRM ----------
    public async Task<object> ConfirmRazorpayPayment(int userId, RazorpayConfirmRequest r)
    {
        var order = await _db.Orders
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstAsync(x => x.Id == r.OrderId && x.UserId == userId);

        if (!_razorpay.VerifySignature(r.RazorpayOrderId, r.RazorpayPaymentId, r.RazorpaySignature))
        {
            // rollback stock on failure
            foreach (var i in order.Items)
                i.Product.Quantity += i.Qty;

            order.PaymentStatus = "Failed";
            await _db.SaveChangesAsync();

            await LogTimeline(order.Id, "Payment Failed (Razorpay)");
            throw new Exception("Payment verification failed");
        }

        order.PaymentStatus = "Success";
        order.GatewayPaymentId = r.RazorpayPaymentId;
        order.GatewaySignature = r.RazorpaySignature;

        await _db.SaveChangesAsync();
        await LogTimeline(order.Id, "Payment Paid (Razorpay)", order.GatewayPaymentId, userId);

        return new { message = "Payment verified", order.Id };
    }

    // ---------- UPI INTENT CONFIRM ----------
    public async Task<object> ConfirmUpiIntent(int userId, UpiIntentConfirmRequest r)
    {
        var order = await _db.Orders
            .FirstAsync(x => x.Id == r.OrderId && x.UserId == userId);

        order.UpiTxnRef = r.TxnRef;
        order.PaymentStatus = "Success";

        await _db.SaveChangesAsync();
        await LogTimeline(order.Id, "Payment Paid (UPI Intent)", r.TxnRef, userId);

        return new { message = "UPI payment recorded", order.Id };
    }

    // ---------- USER ORDERS LIST ----------
    public async Task<object> GetMyOrders(int userId, int page, int size)
    {
        var q = _db.Orders
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id);

        var total = await q.CountAsync();

        var list = await q.Skip((page - 1) * size)
            .Take(size)
            .Select(o => new
            {
                o.Id,
                o.Total,
                o.PaymentStatus,
                o.Status,
                o.CreatedAt
            })
            .ToListAsync();

        return new { page, size, total, orders = list };
    }

    // ---------- ORDER DETAILS ----------
    public async Task<object> GetOrderDetails(int userId, int orderId)
    {
        var order = await _db.Orders
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId);

        if (order == null)
            throw new Exception("Order not found");

        return new
        {
            order.Id,
            order.Address,
            order.PaymentMethod,
            order.PaymentStatus,
            order.Status,
            order.Total,
            deliveryOtp = order.Status == "OutForDelivery" ? order.DeliveryOtp : null,
            items = order.Items.Select(i => new
            {
                i.ProductId,
                i.Product.Title,
                i.Qty,
                i.Price,
                lineTotal = i.Price * i.Qty
            })
        };
    }
}

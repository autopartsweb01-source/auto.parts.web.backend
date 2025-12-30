using Microsoft.EntityFrameworkCore;
using AutoParts.Api.Domain;

namespace AutoParts.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users { get; set; }
    public DbSet<PartType> PartTypes { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderDeliveryOtp> OrderOtps { get; set; }
    public DbSet<OrderTimeline> OrderTimelines { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
}

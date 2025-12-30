using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Repositories;

public class ProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;
    public IQueryable<Product> Query() => _db.Products.AsQueryable();
    public async Task Add(Product p) { _db.Products.Add(p); await _db.SaveChangesAsync(); }
}

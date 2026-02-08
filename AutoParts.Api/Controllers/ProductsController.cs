using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using AutoParts.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    // Public list + paging
    [HttpGet]
    public async Task<IActionResult> Get(int? typeId, string? search, int page = 1, int size = 20)
    {
        var q = _db.Products.AsQueryable();

        if (typeId.HasValue) q = q.Where(x => x.PartTypeId == typeId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.Title.Contains(search));

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();

        return Ok(new { items, total, page, size });
    }

    // Public get by id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _db.Products.FindAsync(id));

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Import([FromBody] List<Product> products)
    {
        if (products == null || !products.Any())
            return BadRequest("No products to import");

        await _db.Products.AddRangeAsync(products);
        await _db.SaveChangesAsync();

        return Ok(new { count = products.Count, message = "Products imported successfully" });
    }

    // ---------- DELETE PRODUCT ----------
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Product deleted" });
    }

    // ---------- BULK DELETE ----------
    [HttpPost("delete-bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBulk([FromBody] List<int> ids)
    {
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        if (!products.Any()) return NotFound();
        
        _db.Products.RemoveRange(products);
        await _db.SaveChangesAsync();
        return Ok(new { message = $"Deleted {products.Count} products" });
    }

    // ---------- UPDATE PRODUCT ----------
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
    {
         var product = await _db.Products.FindAsync(id);
         if (product == null) return NotFound();
         
         if (dto.Quantity.HasValue) product.Quantity = dto.Quantity.Value;
         if (!string.IsNullOrEmpty(dto.ImageDataUrl)) product.ImageDataUrl = dto.ImageDataUrl;
         
         await _db.SaveChangesAsync();
         return Ok(new { message = "Product updated", product });
    }
}

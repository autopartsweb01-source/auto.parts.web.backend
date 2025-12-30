using AutoParts.Api.Data;
using AutoParts.Api.Domain;
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
            q = q.Where(x => x.Name.Contains(search));

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();

        return Ok(new { items, total, page, size });
    }

    // Public get by id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _db.Products.FindAsync(id));

    // Admin only create (excel upload will be added next)
    [Authorize(Roles = "User")]
    [HttpPost("import")]
    public async Task<IActionResult> ImportProducts(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Excel file is required.");

        int inserted = 0, skipped = 0;
        var skippedRows = new List<object>();

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var package = new ExcelPackage(stream);

        var sheet = package.Workbook.Worksheets[0];
        int rowCount = sheet.Dimension.Rows;

        for (int row = 2; row <= rowCount; row++)
        {
            string name = sheet.Cells[row, 1].Text?.Trim();
            string sku = sheet.Cells[row, 2].Text?.Trim();
            string priceStr = sheet.Cells[row, 3].Text?.Trim();
            string stockStr = sheet.Cells[row, 4].Text?.Trim();
            string typeName = sheet.Cells[row, 5].Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                skipped++;
                skippedRows.Add(new { row, sku, reason = "Missing Product Name" });
                continue;
            }

            if (string.IsNullOrWhiteSpace(typeName))
            {
                skipped++;
                skippedRows.Add(new { row, name, sku, reason = "Missing Part Type" });
                continue;
            }

            bool exists = await _db.Products
                .AnyAsync(x => x.Name.ToLower() == name.ToLower() || x.SKU == sku);

            if (exists)
            {
                skipped++;
                skippedRows.Add(new { row, name, sku, reason = "Duplicate Product" });
                continue;
            }

            var type = await _db.PartTypes
                .FirstOrDefaultAsync(x => x.Name.ToLower() == typeName.ToLower());

            if (type == null)
            {
                type = new PartType { Name = typeName };
                _db.PartTypes.Add(type);
                await _db.SaveChangesAsync();
            }

            var product = new Product
            {
                Name = name,
                SKU = sku,
                Price = decimal.TryParse(priceStr, out var pr) ? pr : 0,
                StockQty = int.TryParse(stockStr, out var sq) ? sq : 0,
                PartTypeId = type.Id,
                ImageSource = "/images/no-image.png"
            };

            _db.Products.Add(product);
            inserted++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Import completed",
            inserted,
            skipped,
            skippedRows
        });
    }

}

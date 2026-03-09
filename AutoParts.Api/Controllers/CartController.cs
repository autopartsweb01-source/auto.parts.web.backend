using AutoParts.Api.Services;
using AutoParts.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static AutoParts.Api.DTO.CartDtos;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("cart")]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    private readonly IUserService _users;
    private readonly AppDbContext _db;
    private readonly IOcrService _ocr;

    public CartController(ICartService cart, IUserService users, AppDbContext db, IOcrService ocr)
    {
        _cart = cart;
        _users = users;
        _db = db;
        _ocr = ocr;
    }

    private int GetUserId()
    {
        var idClaim = User.Claims.FirstOrDefault(x => x.Type == "id")?.Value;
        if (int.TryParse(idClaim, out var uid)) return uid;

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? User.FindFirst("name")?.Value;
            var phone = User.FindFirst("mobile")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
            var u = _users.EnsureUserByEmailAsync(email, name, phone).GetAwaiter().GetResult();
            return u.Id;
        }

        // Fallback: parse Authorization header without validating signature
        var auth = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var raw = auth.Substring("Bearer ".Length).Trim();
            try
            {
                var jwt = new JwtSecurityToken(raw);
                string? mail = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.Email, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("email", StringComparison.OrdinalIgnoreCase)
                        || c.Type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase))?.Value;
                string? nm = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.Name, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("name", StringComparison.OrdinalIgnoreCase)
                        || c.Type.EndsWith("/name", StringComparison.OrdinalIgnoreCase))?.Value;
                string? ph = jwt.Claims.FirstOrDefault(c =>
                        c.Type.Equals(System.Security.Claims.ClaimTypes.MobilePhone, StringComparison.OrdinalIgnoreCase)
                        || c.Type.Equals("mobile", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrWhiteSpace(mail))
                {
                    var u2 = _users.EnsureUserByEmailAsync(mail, nm, ph).GetAwaiter().GetResult();
                    return u2.Id;
                }
            }
            catch { }
        }
        return 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart() =>
        Ok(await _cart.GetCartSummary(GetUserId()));

    [HttpPost("add")]
    public async Task<IActionResult> Add(AddToCartRequest r) =>
        Ok(await _cart.AddToCart(GetUserId(), r.ProductId, r.Qty));

    [HttpPost("decrease/{productId}")]
    public async Task<IActionResult> Decrease(int productId) =>
        Ok(await _cart.DecreaseQty(GetUserId(), productId));

    [HttpPut("update")]
    public async Task<IActionResult> BulkUpdate(BulkUpdateRequest r) =>
        Ok(await _cart.BulkUpdate(GetUserId(),
            r.Items.Select(x => (x.ProductId, x.Qty)).ToList()));

    [HttpDelete("remove/{productId}")]
    public async Task<IActionResult> Remove(int productId) =>
        Ok(await _cart.RemoveItem(GetUserId(), productId));

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear() =>
        Ok(await _cart.ClearCart(GetUserId()));

    // ---------- OCR: match image/pdf or provided text to products ----------
    [HttpPost("ocr-match")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> OcrMatch(IFormFile file)
    {
        string? corpus = null;
        try
        {
            corpus = await _ocr.ExtractTextAsync(file, HttpContext.RequestAborted);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(415, new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(corpus))
            return BadRequest(new { message = "Upload a valid PDF or image file." });

        // Normalize and split into lines
        var lines = corpus.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        // Load products into memory for simple matching
        var products = await _db.Products.AsNoTracking().ToListAsync();

        var preview = new List<OcrMatchPreviewItem>();
        var notFound = new List<OcrNotFoundItem>();

        foreach (var line in lines)
        {
            var qty = ExtractQty(line) ?? 1;
            var namePart = StripQty(line);
            var matches = FindMatches(products, namePart);
            if (matches.Count > 0)
            {
                foreach (var m in matches)
                {
                    preview.Add(new OcrMatchPreviewItem(
                        m.Id,
                        m.Title,
                        namePart,
                        1.0,
                        qty,
                        m.Quantity,
                        m.Price
                    ));
                }
            }
            else
            {
                notFound.Add(new OcrNotFoundItem(namePart, qty));
            }
        }

        return Ok(new OcrMatchResponse(preview, notFound));
    }

    // ---------- OCR: add matched items to cart ----------
    [HttpPost("ocr-add")]
    public async Task<IActionResult> OcrAdd(OcrAddRequest req)
    {
        var userId = GetUserId();
        return Ok(await _cart.BulkUpdate(userId, req.Items.Select(i => (i.ProductId, i.Qty)).ToList()));
    }

    private static int? ExtractQty(string s)
    {
        // Patterns: "x 2", "qty 2", "2 nos", trailing " 2", "#2"
        var lower = s.ToLowerInvariant();
        var tokens = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if ((tokens[i] == "x" || tokens[i] == "qty" || tokens[i] == "q" || tokens[i] == "nos" || tokens[i] == "no.") && i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var q1))
                return q1;
            if (tokens[i].StartsWith("x") && int.TryParse(tokens[i].AsSpan(1), out var q2)) return q2;
            if (int.TryParse(tokens[i], out var q3))
            {
                // if number is present and next token like 'nos','tab','pcs' present, accept
                if (i + 1 < tokens.Length && ("nos|tabs|tab|pcs|pc|no|qty|x".Contains(tokens[i + 1])))
                    return q3;
            }
        }
        // Trailing number
        var trimmed = s.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace >= 0 && int.TryParse(trimmed.AsSpan(lastSpace + 1), out var qlast)) return qlast;
        return null;
    }

    private static string StripQty(string s)
    {
        // Remove obvious qty indicators to leave the medicine name part
        var lower = s.ToLowerInvariant();
        var cleaned = System.Text.RegularExpressions.Regex.Replace(lower, @"\b(qty|quantity|nos|no\.?|tabs?|pcs?|x)\b\s*\d+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\d+\s*(nos|no\.?|tabs?|pcs?)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\bx\d+\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        return cleaned.Length > 0 ? cleaned : s.Trim();
    }

    private static List<AutoParts.Api.Domain.Product> FindMatches(IEnumerable<AutoParts.Api.Domain.Product> products, string namePart)
    {
        string norm(string x) => new string(x.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)).ToArray()).Trim();
        var target = norm(namePart);
        var targetTokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var result = new List<AutoParts.Api.Domain.Product>();
        foreach (var p in products)
        {
            var title = norm(p.Title ?? "");
            var titleTokens = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool titleContained = titleTokens.All(t => targetTokens.Contains(t)) || target.Contains(title);
            bool tagContained = false;
            if (!string.IsNullOrWhiteSpace(p.Tag))
            {
                var tagNorm = norm(p.Tag);
                var tagTokens = tagNorm.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                tagContained = tagTokens.Any(t => targetTokens.Contains(t));
            }
            if (titleContained || tagContained)
            {
                result.Add(p);
            }
        }
        return result;
    }

    private static double Similarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        // Jaccard over word sets as simple measure
        var aw = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var bw = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (aw.Count == 0 || bw.Count == 0) return 0;
        var inter = aw.Intersect(bw).Count();
        var union = aw.Union(bw).Count();
        return (double)inter / union;
    }
}

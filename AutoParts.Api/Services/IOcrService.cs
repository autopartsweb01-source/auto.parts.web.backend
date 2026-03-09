using Microsoft.AspNetCore.Http;

namespace AutoParts.Api.Services
{
    public interface IOcrService
    {
        Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default);
    }
}

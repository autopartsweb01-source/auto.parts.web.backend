using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Tesseract;

namespace AutoParts.Api.Services
{
    public class OcrService : IOcrService
    {
        private readonly IConfiguration _config;
        public OcrService(IConfiguration config)
        {
            _config = config;
        }
        public async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) return string.Empty;
            var contentType = file.ContentType?.ToLowerInvariant() ?? "";
            if (contentType == "application/pdf" || await IsPdfAsync(file, ct))
            {
                return await ExtractPdfTextAsync(file, ct);
            }
            if (contentType.StartsWith("image/"))
            {
                return await ExtractImageTextAsync(file, ct);
            }
            throw new NotSupportedException("Unsupported file type");
        }

        private async Task<bool> IsPdfAsync(IFormFile file, CancellationToken ct)
        {
            using var s = file.OpenReadStream();
            var header = new byte[5];
            var read = await s.ReadAsync(header, 0, header.Length, ct);
            return read == 5 && header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F' && header[4] == (byte)'-';
        }

        private async Task<string> ExtractPdfTextAsync(IFormFile file, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            using var doc = PdfDocument.Open(ms);
            var sb = new System.Text.StringBuilder();
            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords();
                if (words != null && words.Any())
                {
                    sb.AppendLine(string.Join(" ", words.Select(w => w.Text)));
                }
                else
                {
                    sb.AppendLine(page.Text);
                }
            }
            return sb.ToString();
        }

        private async Task<string> ExtractImageTextAsync(IFormFile file, CancellationToken ct)
        {
            string? tessPath = _config["Ocr:TesseractDataPath"];
            if (string.IsNullOrWhiteSpace(tessPath))
                tessPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
            if (string.IsNullOrWhiteSpace(tessPath))
                throw new NotSupportedException("Tesseract not available. Set TESSDATA_PREFIX to tessdata path.");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            using var pix = Pix.LoadFromMemory(bytes);
            using var engine = new TesseractEngine(tessPath, "eng", EngineMode.Default);
            using var page = engine.Process(pix);
            var text = page.GetText();
            return text ?? string.Empty;
        }
    }
}

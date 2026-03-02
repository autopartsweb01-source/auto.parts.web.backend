using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace AutoParts.Api.Infrastructure.Http;

public static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage Clone(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            request.Content.CopyToAsync(ms).GetAwaiter().GetResult();
            ms.Position = 0;
            var content = new StreamContent(ms);
            if (request.Content.Headers != null)
            {
                foreach (var h in request.Content.Headers)
                    content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            clone.Content = content;
        }
        return clone;
    }
}

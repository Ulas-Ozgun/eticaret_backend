using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace bitirme_projesi.Services
{
    public class HuggingFaceEmbeddingService
    {
        private static readonly Uri InferenceEndpoint = new("http://127.0.0.1:8000/embed");

        private readonly HttpClient _httpClient;
        private readonly IHostEnvironment _env;
        private readonly ILogger<HuggingFaceEmbeddingService> _logger;

        public HuggingFaceEmbeddingService(
            HttpClient httpClient,
            IHostEnvironment env,
            ILogger<HuggingFaceEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _env = env;
            _logger = logger;
        }

        public async Task<Vector?> GetImageEmbeddingAsync(
            IFormFile imageFile,
            CancellationToken cancellationToken = default)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("EmbeddingService: boş veya null IFormFile.");
                return null;
            }

            byte[] bytes;
            try
            {
                await using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms, cancellationToken);
                bytes = ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingService: dosya okunamadı.");
                return null;
            }

            var contentType = string.IsNullOrWhiteSpace(imageFile.ContentType)
                ? "application/octet-stream"
                : imageFile.ContentType;

            return await SendImageBytesToInferenceAsync(bytes, contentType, cancellationToken);
        }

        public async Task<Vector?> GetImageEmbeddingFromUrlAsync(
            string imageUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _logger.LogWarning("EmbeddingService: imageUrl boş.");
                return null;
            }

            imageUrl = imageUrl.Trim();

            byte[] bytes;
            string contentType;

            try
            {
                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute)
                    && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                {
                    using var getReq = new HttpRequestMessage(HttpMethod.Get, absolute);
                    using var getRes = await _httpClient.SendAsync(getReq, cancellationToken);
                    if (!getRes.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Görsel indirilemedi: {Url} Status={Status}",
                            imageUrl,
                            (int)getRes.StatusCode);
                        return null;
                    }

                    bytes = await getRes.Content.ReadAsByteArrayAsync(cancellationToken);
                    contentType = getRes.Content.Headers.ContentType?.MediaType
                        ?? GuessMimeFromUrl(imageUrl);
                }
                else
                {
                    if (imageUrl.Contains("..", StringComparison.Ordinal))
                    {
                        _logger.LogWarning("EmbeddingService: geçersiz göreli yol (..).");
                        return null;
                    }

                    var webRoot = _env is IWebHostEnvironment w && !string.IsNullOrEmpty(w.WebRootPath)
                        ? w.WebRootPath
                        : Path.Combine(_env.ContentRootPath, "wwwroot");

                    var relative = imageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                    var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));

                    if (!fullPath.StartsWith(Path.GetFullPath(webRoot), StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("EmbeddingService: yol web kökünün dışında.");
                        return null;
                    }

                    if (!File.Exists(fullPath))
                    {
                        _logger.LogWarning("EmbeddingService: dosya yok: {Path}", fullPath);
                        return null;
                    }

                    bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                    contentType = GuessMimeFromPath(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingService: görsel okunamadı/indirilemedi. {Url}", imageUrl);
                return null;
            }

            if (bytes.Length == 0)
                return null;

            return await SendImageBytesToInferenceAsync(bytes, contentType, cancellationToken);
        }

        private async Task<Vector?> SendImageBytesToInferenceAsync(
            byte[] bytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            var imageMediaType = NormalizeImageContentTypeForInference(contentType);

            try
            {
                using var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue(imageMediaType);

                using var request = new HttpRequestMessage(HttpMethod.Post, InferenceEndpoint)
                {
                    Content = content
                };
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var snippet = body.Length > 800 ? body[..800] + "..." : body;
                    _logger.LogWarning(
                        "Embedding servisi başarısız: {StatusCode}. Gövde: {Body}",
                        (int)response.StatusCode,
                        snippet);
                    return null;
                }

                var floats = ParseEmbeddingResponse(body);
                if (floats == null || floats.Length == 0)
                {
                    _logger.LogWarning("EmbeddingService: yanıttan embedding çıkarılamadı.");
                    return null;
                }

                return new Vector(floats);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "EmbeddingService: HTTP isteği hatası.");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "EmbeddingService: iptal veya zaman aşımı.");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "EmbeddingService: JSON çözümlenemedi.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingService: beklenmeyen hata.");
                return null;
            }
        }

        private static float[]? ParseEmbeddingResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("error", out _))
                return null;

            if (!root.TryGetProperty("embedding", out var emb) || emb.ValueKind != JsonValueKind.Array)
                return null;

            return ToFloatArray(emb);
        }

        private static string NormalizeImageContentTypeForInference(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return "image/jpeg";

            string media;
            try
            {
                media = MediaTypeHeaderValue.Parse(contentType).MediaType?.ToLowerInvariant() ?? "";
            }
            catch
            {
                media = contentType.Trim().Split(';')[0].Trim().ToLowerInvariant();
            }

            return media switch
            {
                "image/png" => "image/png",
                "image/jpeg" => "image/jpeg",
                "image/jpg" => "image/jpeg",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                "image/bmp" => "image/bmp",
                "image/tiff" => "image/tiff",
                "image/x-image" => "image/x-image",
                "application/octet-stream" => "image/jpeg",
                _ => media.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? media : "image/jpeg"
            };
        }

        private static string GuessMimeFromPath(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        private static string GuessMimeFromUrl(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                return GuessMimeFromPath(path);
            }
            catch
            {
                return "application/octet-stream";
            }
        }

        private static float[] ToFloatArray(JsonElement arrayElement)
        {
            var n = arrayElement.GetArrayLength();
            var arr = new float[n];
            var i = 0;
            foreach (var el in arrayElement.EnumerateArray())
            {
                arr[i++] = el.ValueKind == JsonValueKind.Number ? (float)el.GetDouble() : 0f;
            }

            return arr;
        }
    }
}

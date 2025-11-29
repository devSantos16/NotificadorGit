using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Service
{
    internal class GeminiService : IIAService
    {
        private readonly HttpClient _httpClient;
        private readonly IAOpcoes _options;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(HttpClient httpClient, IOptions<IAOpcoes> options, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool IsTrue, string message)> GerarPrompt(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt não pode ser vazio.", nameof(prompt));

            var requestBody = new
            {
                model = _options.Modelo,
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Url}/v1beta/models/{_options.Modelo}:generateContent")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(_options.ChaveApi))
            {
                if (!request.Headers.Contains("X-goog-api-key"))
                    request.Headers.Add("X-goog-api-key", _options.ChaveApi);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(result);
            return GerarDocumentoJson(doc);
        }

        private static (bool IsTrue, string Message) GerarDocumentoJson(JsonDocument doc)
        {
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                            candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];

                if (first.TryGetProperty("content", out var contentProp) &&
                    contentProp.TryGetProperty("parts", out var partsProp) &&
                    partsProp.ValueKind == JsonValueKind.Array && partsProp.GetArrayLength() > 0)
                {
                    var part = partsProp[0];

                    if (part.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                    {
                        var text = textProp.GetString();

                        if (!string.IsNullOrEmpty(text))
                        {
                            text = text.Replace("```json", "").Replace("```", "").Trim();

                            using var innerDoc = JsonDocument.Parse(text);
                            var message = innerDoc.RootElement.GetProperty("message").GetString();
                            var isTrue = innerDoc.RootElement.GetProperty("isTrue").GetBoolean();

                            return (isTrue , message ?? "");
                        }
                    }
                }
            }

            return (false, "Não foi possivel análisar o documento final");
        }
    }
}
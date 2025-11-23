using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class GeminiController
    {
        private readonly IGeminiService _geminiService;
        private readonly ILogger<GeminiController> _logger;

        public GeminiController(IGeminiService geminiService, ILogger<GeminiController> logger)
        {
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool IsTrue, string Message)> GerarPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt não pode ser vazio.", nameof(prompt));

            _logger.LogInformation("Enviando prompt para Gemini.");
            try
            {
                var resposta = await _geminiService.GerarPrompt(prompt, cancellationToken);
                _logger.LogInformation("Resposta recebida da Gemini.");
                return resposta;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operação Gemini cancelada.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar conteúdo com Gemini.");
                throw;
            }
        }
    }
}
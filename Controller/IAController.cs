using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class IAController
    {
        private readonly IIAService _IAService;
        private readonly ILogger<IAController> _logger;

        public IAController(IIAService IAService, ILogger<IAController> logger)
        {
            _IAService = IAService ?? throw new ArgumentNullException(nameof(IAService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool IsTrue, string Message)> GerarPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt não pode ser vazio.", nameof(prompt));

            _logger.LogInformation("Enviando prompt para a IA.");
            try
            {
                var resposta = await _IAService.GerarPrompt(prompt, cancellationToken);
                _logger.LogInformation("Resposta recebida da IA.");
                return resposta;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operação da IA cancelada.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar conteúdo com a IA.");
                throw;
            }
        }
    }
}
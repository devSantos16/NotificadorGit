using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class RepositoryController
    {
        private readonly IGitRepositoryService _gitService;
        private readonly GeminiController _geminiController;
        private readonly ILogger<RepositoryController> _logger;

        public RepositoryController(IGitRepositoryService gitService, GeminiController geminiController,  ILogger<RepositoryController> logger)
        {
            _gitService = gitService;
            _geminiController = geminiController;
            _logger = logger;
        }

        public async Task<List<Branch>> ListarBranchesComConflito(CancellationToken cancellationToken = default)
        {
            Task<List<Branch>> branches =  _gitService.ListarBranchesComConflitoAsync(cancellationToken);

            foreach (var branch in await branches)
            {
                foreach (var commit in branch.Commits)
                {
                    foreach (var conflito in commit.Conflitos)
                    {
                        (bool, string) resposta = await _geminiController.GerarPromptAsync(
                            $"Responda em JSON com os campos 'message' e 'isTrue'. Pergunta: " +
                            $"Sabendo que o diff de partida é {conflito.DiffPartida}" +
                            $"O Autor {commit.Autor} do commit {commit.Sha}: {commit.Mensagem} que possui o seguinte diff: {conflito.DiffRemoto}" +
                            $"conflitou com as alterações da diff local {conflito.DiffLocal}?" +
                            $"Se sim, Explique as diferenças entre e considere dá dicas pra resolver o conflito", cancellationToken);
                        conflito.HaConflitoComJustificativa = resposta;
                    }
                }
            }
            return await branches;
        }
            
    }
}
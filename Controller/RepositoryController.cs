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

        public RepositoryController(
            IGitRepositoryService gitService,
            GeminiController geminiController,
            ILogger<RepositoryController> logger)
        {
            _gitService = gitService;
            _geminiController = geminiController;
            _logger = logger;
        }

        public async Task<List<Branch>> ListarBranchesComConflito(
            CancellationToken cancellationToken = default)
        {
            var branchesTask = _gitService.ListarBranchesComConflitoAsync(cancellationToken);

            var branches = await branchesTask;

            foreach (var branch in branches)
            {
                foreach (var commit in branch.Commits)
                {
                    foreach (var conflito in commit.Conflitos)
                    {
                        string pergunta =
                            "Responda em JSON no formato { \"message\": string, \"isTrue\": bool }. " +
                            "Avalie se há conflito entre os diffs abaixo.\n\n" +

                            "[DIFF DE PARTIDA]\n" +
                            $"{conflito.DiffPartida}\n\n" +

                            "Informações do commit remoto:\n" +
                            $"- Autor: {commit.Autor}\n" +
                            $"- Hash: {commit.Sha}\n" +
                            $"- Branch: {branch.NomeBranch}\n" +
                            $"- Mensagem: {commit.Mensagem}\n" +
                            
                            "[DIFF REMOTO]\n" +
                            $"{conflito.DiffRemoto}\n\n" +

                            "[DIFF LOCAL]\n" +
                            $"{conflito.DiffLocal}\n\n" +

                            "Pergunta: O diff remoto conflita com o diff local em relação ao diff de partida?\n" +
                            "Se 'isTrue' for true, explique detalhadamente em 'message' quais partes conflitam, " +
                            "quais linhas sobrepõem alterações e dê dicas práticas para resolver o conflito.";

                        (bool, string) resposta =
                            await _geminiController.GerarPromptAsync(pergunta, cancellationToken);

                        conflito.HaConflitoComJustificativa = resposta;
                    }
                }
            }

            return branches;
        }
    }
}

using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class GitRepositorioController
    {
        private readonly IGitRepositorioService _gitService;
        private readonly IAController _IAController;
        private readonly ILogger<GitRepositorioController> _logger;

        public GitRepositorioController(
            IGitRepositorioService gitService,
            IAController geminiController,
            ILogger<GitRepositorioController> logger)
        {
            _gitService = gitService;
            _IAController = geminiController;
            _logger = logger;
        }

        public async Task<List<Branch>> ListarBranchesComConflito(
            CancellationToken cancellationToken = default)
        {
            var branches = await _gitService.ListarBranchesComConflitoAsync(cancellationToken);

            foreach (var branch in branches)
            {
                foreach (var commit in branch.Commits)
                {
                    foreach (var arquivo in commit.Arquivos)
                    {
                        string pergunta =
                            "Responda em JSON no formato { \"message\": string, \"isTrue\": bool }. " +
                            "Avalie se há conflito entre os diffs abaixo.\n\n" +

                            "[DIFF DE PARTIDA]\n" +
                            $"{arquivo.DiffPartida}\n\n" +

                            "Informações do commit remoto:\n" +
                            $"- Autor: {commit.Autor}\n" +
                            $"- Hash: {commit.Sha}\n" +
                            $"- Branch: {branch.NomeBranch}\n" +
                            $"- Mensagem: {commit.Mensagem}\n" +
                            
                            "[DIFF REMOTO]\n" +
                            $"{arquivo.DiffRemoto}\n\n" +

                            "[DIFF LOCAL]\n" +
                            $"{arquivo.DiffLocal}\n\n" +

                            "Pergunta: O diff remoto conflita com o diff local em relação ao diff de partida?\n" +
                            "Se 'isTrue' for true, explique detalhadamente em 'message' quais partes conflitam, " +
                            "quais linhas sobrepõem alterações e dê dicas práticas para resolver o conflito.";

                        (bool, string) resposta =
                            await _IAController.GerarPromptAsync(pergunta, cancellationToken);

                        arquivo.HaConflitoComJustificativa = resposta;
                    }
                }
            }

            return branches;
        }
    }
}

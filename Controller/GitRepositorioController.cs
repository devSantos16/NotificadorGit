using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
            _logger.LogInformation("Iniciando ListarBranchesComConflito.");

            try
            {
                var branches = await _gitService.ListarBranchesComConflitoAsync(cancellationToken);

                if (branches == null || branches.Count == 0)
                {
                    _logger.LogInformation("Nenhuma branch com possível conflito encontrada.");
                    return branches;
                }

                _logger.LogInformation("Encontradas {BranchCount} branches com possível conflito.", branches.Count);

                foreach (var branch in branches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (branch == null)
                    {
                        _logger.LogWarning("Lista de branches contém item nulo.");
                        continue;
                    }

                    var commits = branch.Commits ?? new List<Commit>();
                    _logger.LogInformation("Processando branch {Branch} com {CommitCount} commits.", branch.NomeBranch, commits.Count);

                    foreach (var commit in commits)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (commit == null)
                        {
                            _logger.LogWarning("Encontrado commit nulo na branch {Branch}.", branch.NomeBranch);
                            continue;
                        }

                        _logger.LogDebug("Processando commit {Sha} (Autor: {Author}) na branch {Branch}.", commit.Sha, commit.Autor, branch.NomeBranch);

                        var arquivos = commit.Arquivos ?? new List<Arquivo>();
                        foreach (var arquivo in arquivos)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (arquivo == null)
                            {
                                _logger.LogWarning("Encontrado arquivo nulo no commit {Sha}.", commit.Sha);
                                continue;
                            }

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

                            var identificadorArquivo = arquivo.DiffRemoto;
                            if (string.IsNullOrEmpty(identificadorArquivo))
                                identificadorArquivo = arquivo.DiffLocal ?? "<sem-diff>";
                            if (identificadorArquivo.Length > 80)
                                identificadorArquivo = identificadorArquivo.Substring(0, 80) + "...";

                            try
                            {
                                _logger.LogDebug("Enviando prompt para IA. Branch={Branch} Commit={Sha} ArquivoSnippet={Snippet}",
                                    branch.NomeBranch, commit.Sha, identificadorArquivo);

                                (bool, string) resposta =
                                    await _IAController.GerarPromptAsync(pergunta, cancellationToken);

                                arquivo.HaConflitoComJustificativa = resposta;

                                _logger.LogInformation(
                                    "Resposta IA obtida. Branch={Branch} Commit={Sha} isTrue={IsTrue}",
                                    branch.NomeBranch, commit.Sha, resposta.Item1);

                                if (!string.IsNullOrEmpty(resposta.Item2))
                                {
                                    var msgTruncada = resposta.Item2.Length > 200 ? resposta.Item2.Substring(0, 200) + "..." : resposta.Item2;
                                    _logger.LogDebug("Resposta IA (truncada): {Message}", msgTruncada);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogInformation("Operação cancelada durante chamada à IA. Commit={Sha} Branch={Branch}", commit.Sha, branch.NomeBranch);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao chamar IA para Commit={Sha} Branch={Branch}", commit.Sha, branch.NomeBranch);
                                arquivo.HaConflitoComJustificativa = (false, $"Erro ao avaliar conflito: {ex.Message}");
                            }
                        }
                    }
                }

                _logger.LogInformation("Finalizado processamento de branches com conflito.");
                return branches;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ListarBranchesComConflito cancelado.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado em ListarBranchesComConflito.");
                throw;
            }
        }
    }
}

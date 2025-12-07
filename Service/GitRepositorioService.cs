using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Service
{
    internal class GitRepositorioService : IGitRepositorioService
    {
        private readonly GitOpcoes _opcoes;
        private readonly ILogger<GitRepositorioService> _logger;

        public GitRepositorioService(IOptions<GitOpcoes> options, ILogger<GitRepositorioService> logger)
        {
            _opcoes = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }   

        public Task<List<Model.Branch>> ListarBranchesComConflitoAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ListarCommitsInternal(cancellationToken), cancellationToken);
        }

        private List<Model.Branch> ListarCommitsInternal(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Iniciando ListarBranchesComConflito. Repo={Repo} BranchPrincipal={BranchPrincipal} Remota={Remota}",
                _opcoes.CaminhoRepositorio, _opcoes.Branch, _opcoes.Remota);

            try
            {
                using var repo = new Repository(_opcoes.CaminhoRepositorio);

                var remota = repo.Network.Remotes[_opcoes.Remota];
                if (remota == null)
                {
                    _logger.LogWarning("Remota não encontrada: {Remota}", _opcoes.Remota);
                }
                else
                {
                    try
                    {
                        _logger.LogDebug("Executando fetch em remote {Remote}", remota.Name);
                        Commands.Fetch(repo, remota.Name, remota.FetchRefSpecs.Select(rs => rs.Specification), null, null);
                        _logger.LogInformation("Fetch concluído para remota {Remote}", remota.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao executar fetch em {Remote}. Continuando com refs locais.", _opcoes.Remota);
                    }
                }

                // Obtém todas as branches remotas
                var branchesRemota = repo.Branches
                    .Where(b => b.IsRemote && b.FriendlyName.StartsWith($"{_opcoes.Remota}/"))
                    .ToList();

                _logger.LogInformation("Branches remotas encontradas: {Count}", branchesRemota.Count);

                // Obtém a branch principal remota
                var branchPrincipal = repo.Branches[$"{_opcoes.Remota}/{_opcoes.Branch}"];
                if (branchPrincipal == null)
                {
                    _logger.LogWarning("Branch principal remota não encontrada: {RemoteBranch}", $"{_opcoes.Remota}/{_opcoes.Branch}");
                }
                else
                {
                    _logger.LogDebug("Branch principal remota: {RemoteBranch} (tip: {Tip})", branchPrincipal.FriendlyName, branchPrincipal.Tip?.Sha);
                }

                // Obtém as branches filhas da principal remota
                var branchesFilhaRemota = branchesRemota
                    .Where(b => b != branchPrincipal && repo.ObjectDatabase
                    .CalculateHistoryDivergence(branchPrincipal?.Tip, b.Tip)?.CommonAncestor == branchPrincipal?.Tip)
                    .ToList();

                _logger.LogInformation("Branches filhas da principal selecionadas: {Count}", branchesFilhaRemota.Count);

                // Obtem a branch local
                var branchLocal = repo.Branches[_opcoes.Branch];
                if (branchLocal == null)
                {
                    _logger.LogWarning("Branch local principal não encontrada: {BranchLocal}", _opcoes.Branch);
                }

                // Cria uma lista com a branch principal e as filhas para processar
                var branches = new List<LibGit2Sharp.Branch>();

                if (branchPrincipal != null)
                {
                    // Adiciona a branch principal à lista de processamento
                    branches.Add(branchPrincipal);
                }

                // Adiciona as branches filhas à lista de processamento
                branches.AddRange(branchesFilhaRemota);

                // pega todos os arquivo locais do repositorio
                var arquivosLocais = repo.RetrieveStatus()
                    .Select(s => s.FilePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _logger.LogInformation("Arquivos locais detectados: {LocalFilesCount}", arquivosLocais.Count);

                // Cria uma lista de branches
                var branchesParaProcessar = new List<Model.Branch>();

                foreach (var branch in branches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation("Processando branch remota {Branch}", branch.FriendlyName);

                    var filtroBranchParaExcluir = branch == branchPrincipal
                        ? new[] { branchLocal }
                        : new[] { branchLocal, branchPrincipal };

                    ICommitLog commitsRemoto = repo.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = branch,
                        ExcludeReachableFrom = filtroBranchParaExcluir
                    });

                    var commitCountEstimate = commitsRemoto.Count();
                    _logger.LogDebug("Commits remotos a analisar para {Branch}: {Count}", branch.FriendlyName, commitCountEstimate);

                    // instancia uma lista de commits
                    var commits = obterCommits(repo, commitsRemoto, arquivosLocais, cancellationToken, branchLocal);

                    _logger.LogInformation("Commits conflitados encontrados em {Branch}: {Count}", branch.FriendlyName, commits.Count);

                    if (commits.Any())
                    {
                        branchesParaProcessar.Add(new Model.Branch
                        {
                            Commits = commits,
                            NomeBranch = branch.FriendlyName.Replace($"{_opcoes.Remota}/", string.Empty)
                        });
                    }
                }

                sw.Stop();
                _logger.LogInformation("Finalizado ListarBranchesComConflito. Duração={Elapsed}ms. BranchesProcessadas={Count}",
                    sw.ElapsedMilliseconds, branchesParaProcessar.Count);

                return branchesParaProcessar;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operação cancelada ao listar commits.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar commits com conflito.");
                return new List<Model.Branch>();
            }
        }

        private List<Model.Commit> obterCommits(Repository repo, ICommitLog commitsRemoto, HashSet<string> arquivosLocais, CancellationToken cancellationToken, LibGit2Sharp.Branch localBranch)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("Iniciando obterCommits para análise de commits remotos.");

            List<Model.Commit> commits = new List<Model.Commit>();

            ICommitLog commitBranchLocal = null;
            try
            {
                if (localBranch != null)
                {
                    commitBranchLocal = repo.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = localBranch
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao obter histórico da branch local.");
            }

            int processedCommits = 0;
            foreach (var commit in commitsRemoto)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedCommits++;

                var parentes = commit.Parents.FirstOrDefault();
                if (parentes == null)
                {
                    _logger.LogDebug("Pulando commit {Sha} sem parent.", commit.Sha);
                    continue;
                }

                _logger.LogDebug("Analisando commit {Sha} (Autor={Author})", commit.Sha, commit.Author?.Name);

                var modificacoes = repo.Diff.Compare<TreeChanges>(parentes.Tree, commit.Tree);
                var patch = repo.Diff.Compare<Patch>(parentes.Tree, commit.Tree);
                List<Arquivo> arquivos = new List<Arquivo>();

                foreach (var modificacao in modificacoes)
                {
                    if (!arquivosLocais.Contains(modificacao.Path))
                    {
                        _logger.LogTrace("Arquivo {Path} do commit {Sha} não está modificado localmente — ignorando.", modificacao.Path, commit.Sha);
                        continue;
                    }

                    if (commits.Any(x => x.Arquivos.Any(c => string.Equals(c.NomeArquivo, modificacao.Path, StringComparison.OrdinalIgnoreCase)))
                    )
                    {
                        _logger.LogTrace("Arquivo {Path} já contabilizado em outro commit — ignorando duplicata.", modificacao.Path);
                        continue;
                    }

                    try
                    {
                        var entry = patch[modificacao.Path];
                        var blob = commit[modificacao.Path]?.Target as Blob;
                        var blobPartida = commitBranchLocal?.FirstOrDefault()[modificacao.Path]?.Target as Blob;
                        string conteudoPartida = null;
                        string conteudoRemota = null;
                        string conteudoLocal = null;

                        try
                        {
                            conteudoPartida = blobPartida?.GetContentText();
                        }
                        catch (Exception pe)
                        {
                            _logger.LogWarning(pe, "Falha ao ler blob de partida para {Path} no commit {Sha}.", modificacao.Path, commit.Sha);
                        }

                        try
                        {
                            conteudoRemota = blob?.GetContentText();
                        }
                        catch (Exception re)
                        {
                            _logger.LogWarning(re, "Falha ao ler blob remoto para {Path} no commit {Sha}.", modificacao.Path, commit.Sha);
                        }

                        try
                        {
                            var fullPath = Path.Combine(repo.Info.WorkingDirectory ?? string.Empty, modificacao.Path);
                            if (File.Exists(fullPath))
                            {
                                conteudoLocal = File.ReadAllText(fullPath);
                            }
                            else
                            {
                                _logger.LogWarning("Arquivo local não encontrado: {FullPath}", fullPath);
                            }
                        }
                        catch (Exception fe)
                        {
                            _logger.LogWarning(fe, "Falha ao ler arquivo local {Path} para commit {Sha}.", modificacao.Path, commit.Sha);
                        }

                        arquivos.Add(new Arquivo
                        {
                            NomeArquivo = modificacao.Path,
                            DiffPartida = conteudoPartida,
                            DiffLocal = conteudoLocal,
                            DiffRemoto = conteudoRemota,
                        });

                        _logger.LogDebug("Arquivo adicionado para análise: {Path} (Commit={Sha})", modificacao.Path, commit.Sha);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar modificação {Path} no commit {Sha}", modificacao.Path, commit.Sha);
                    }
                }

                if (arquivos.Count != 0)
                {
                    commits.Add(new Model.Commit
                    {
                        Sha = commit.Sha,
                        Autor = commit.Author.Name,
                        Email = commit.Author.Email,
                        Mensagem = commit.Message,
                        Data = commit.Author.When,
                        Arquivos = arquivos
                    });

                    _logger.LogInformation("Commit com conflitos detectado: {Sha} (arquivos={Count})", commit.Sha, arquivos.Count);
                }

            }

            sw.Stop();
            _logger.LogDebug("Finalizado obterCommits. Commits analisados={Processed} TempoMs={Elapsed}", processedCommits, sw.ElapsedMilliseconds);

            return commits;
        }
    }
}

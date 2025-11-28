using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            try
            {
                _logger.LogInformation("Listando commits com conflito em {RepositoryPath} na branch {Branch} da remota {Remote}.", _opcoes.CaminhoRepositorio, _opcoes.Branch, _opcoes.Remota);
                
                using var repo = new Repository(_opcoes.CaminhoRepositorio);
                var remota = repo.Network.Remotes[_opcoes.Remota];

                // Faz o Fetch pra poder analisar o repositorio remoto
                Commands.Fetch(repo, remota.Name, remota.FetchRefSpecs.Select(rs => rs.Specification), null, null );

                // Obtém todas as branches remotas
                var branchesRemota = repo.Branches
                    .Where(b => b.IsRemote && b.FriendlyName.StartsWith($"{_opcoes.Remota}/"))
                    .ToList();

                // Obtém a branch principal remota
                var branchPrincipal = repo.Branches[$"{_opcoes.Remota}/{_opcoes.Branch}"];

                // Obtém as branches filhas da principal remota
                var branchesFilhaRemota = branchesRemota
                    .Where(b => b != branchPrincipal && repo.ObjectDatabase
                    .CalculateHistoryDivergence(branchPrincipal.Tip, b.Tip)?.CommonAncestor == branchPrincipal.Tip)
                    .ToList();

                // Obtem a branch local
                var branchLocal = repo.Branches[_opcoes.Branch];

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

                // Cria uma lista de branches
                var branchesParaProcessar = new List<Model.Branch>();

                foreach (var branch in branches)
                {
                    var filtroBranchParaExcluir = branch == branchPrincipal
                        ? new[] { branchLocal }
                        : new[] { branchLocal, branchPrincipal };

                    ICommitLog commitsRemoto = repo.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = branch,
                        ExcludeReachableFrom = filtroBranchParaExcluir
                    });

                    // instancia uma lista de commits
                    var commits = obterCommits(repo, commitsRemoto, arquivosLocais, cancellationToken, branchLocal);

                    if (commits.Any())
                    {
                        branchesParaProcessar.Add(new Model.Branch 
                        { 
                            Commits = commits,
                            NomeBranch = branch.FriendlyName.Replace($"{_opcoes.Remota}/", string.Empty)
                        });
                    }
                }

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

        private static List<Model.Commit> obterCommits(Repository repo, ICommitLog commitsRemoto, HashSet<string> arquivosLocais, CancellationToken cancellationToken, LibGit2Sharp.Branch localBranch)
        {
            List<Model.Commit> commits = new List<Model.Commit>();

            ICommitLog commitBranchLocal = repo.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = localBranch
            });

            foreach (var commit in commitsRemoto)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parentes = commit.Parents.FirstOrDefault();
                if (parentes == null) continue;

                var modificacoes = repo.Diff.Compare<TreeChanges>(parentes.Tree, commit.Tree);
                var patch = repo.Diff.Compare<Patch>(parentes.Tree, commit.Tree);
                List<Arquivo> arquivos = new List<Arquivo>();
                
                foreach (var modificacao in modificacoes)
                {
                    if (!arquivosLocais.Contains(modificacao.Path)) continue;
                    if (commits.Any(x => x.Arquivos.Any(c => string.Equals(c.NomeArquivo, modificacao.Path, StringComparison.OrdinalIgnoreCase)))) continue;

                    var entry = patch[modificacao.Path];
                    var blob = commit[modificacao.Path]?.Target as Blob;
                    var blobPartida = commitBranchLocal.FirstOrDefault()[modificacao.Path]?.Target as Blob;
                    string conteudoPartida = blobPartida?.GetContentText();
                    string conteudoRemota = blob?.GetContentText();
                    string conteudoLocal = File.ReadAllText(Path.Combine(repo.Info.WorkingDirectory, modificacao.Path));

                    arquivos.Add(new Arquivo
                    {
                        NomeArquivo = modificacao.Path,
                        DiffPartida = conteudoPartida,
                        DiffLocal = conteudoLocal,
                        DiffRemoto = conteudoRemota,
                    });
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
                }
            }

            return commits;
        }
    }
}

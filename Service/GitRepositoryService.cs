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
    internal class GitRepositoryService : IGitRepositoryService
    {
        private readonly GitOptions _options;
        private readonly ILogger<GitRepositoryService> _logger;

        public GitRepositoryService(IOptions<GitOptions> options, ILogger<GitRepositoryService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
                _logger.LogInformation("Listando commits com conflito em {RepositoryPath} na branch {Branch} da remota {Remote}.", _options.CaminhoRepositorio, _options.Branch, _options.Remota);
                
                using var repo = new Repository(_options.CaminhoRepositorio);
                var remote = repo.Network.Remotes[_options.Remota];

                // Faz o Fetch pra poder analisar o repositorio remoto
                Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(rs => rs.Specification), null, null );

                // Obtém todas as branches remotas
                var remoteBranches = repo.Branches
                    .Where(b => b.IsRemote && b.FriendlyName.StartsWith($"{_options.Remota}/"))
                    .ToList();

                // Obtém a branch principal remota
                var mainBranch = repo.Branches[$"{_options.Remota}/{_options.Branch}"];

                // Obtém as branches filhas da principal remota
                var childBranches = remoteBranches
                    .Where(b => b != mainBranch && repo.ObjectDatabase.CalculateHistoryDivergence(mainBranch.Tip, b.Tip)?.CommonAncestor != null)
                    .ToList();

                // Obtem a branch local
                var localBranch = repo.Branches[_options.Branch];

                // Cria uma lista com a branch principal e as filhas para processar
                var branchesParaProcessar = new List<LibGit2Sharp.Branch>();
                
                if (mainBranch != null)
                {
                    // Adiciona a branch principal à lista de processamento
                    branchesParaProcessar.Add(mainBranch);
                }

                // Adiciona as branches filhas à lista de processamento
                branchesParaProcessar.AddRange(childBranches);

                // pega todos os arquivo locais do repositorio
                var arquivosLocais = repo.RetrieveStatus()
                    .Select(s => s.FilePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Cria uma lista de branches
                var branches = new List<Model.Branch>();

                foreach (var remoteBranch in branchesParaProcessar)
                {
                    Model.Branch branch = new Model.Branch();

                    var exclude = remoteBranch == mainBranch
                        ? new[] { localBranch }
                        : new[] { localBranch, mainBranch };

                    ICommitLog commitsRemoto = repo.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = remoteBranch,
                        ExcludeReachableFrom = exclude
                    });

                    // instancia uma lista de commits
                    var commits = obterCommits(repo, commitsRemoto, arquivosLocais, cancellationToken);

                    if (commits.Any())
                    {
                        branches.Add(new Model.Branch { Commits = commits });
                    }
                }

                return branches;
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

        private static List<Model.Commit> obterCommits(Repository repo, ICommitLog commitsRemoto, HashSet<string> arquivosLocais, CancellationToken cancellationToken)
        {
            List<Model.Commit> commits = new List<Model.Commit>();

            foreach (var commit in commitsRemoto)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parent = commit.Parents.FirstOrDefault();

                if (parent == null)
                {
                    continue;
                }

                var modificacoes = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                List<Conflito> conflitos = new List<Conflito>();

                foreach (var modificacao in modificacoes)
                {
                    if (!arquivosLocais.Contains(modificacao.Path))
                    {
                        continue;
                    }

                    if (commits.Any(x => x.Conflitos.Any(c => string.Equals(c.Arquivo, modificacao.Path, StringComparison.OrdinalIgnoreCase))))
                    {
                        continue;
                    }

                    conflitos.Add(new Conflito
                    {
                        Arquivo = modificacao.Path
                    });
                }

                if (conflitos.Count == 0)
                {
                    continue;
                }

                commits.Add(new Model.Commit
                {
                    Sha = commit.Sha,
                    Autor = commit.Author.Name,
                    Email = commit.Author.Email,
                    Mensagem = commit.Message,
                    Data = commit.Author.When,
                    Conflitos = conflitos
                });
            }

            return commits;
        }
    }
}

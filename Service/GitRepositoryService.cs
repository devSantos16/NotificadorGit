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

        public Task<List<Model.CommitConflitado>> ListarCommitsComConflitoAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ListarCommitsInternal(cancellationToken), cancellationToken);
        }

        private List<Model.CommitConflitado> ListarCommitsInternal(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Listando commits com conflito em {RepositoryPath} na branch {Branch} da remota {Remote}.", _options.CaminhoRepositorio, _options.Branch, _options.Remota);
                using var repo = new Repository(_options.CaminhoRepositorio);

                try
                {
                    Commands.Fetch(repo, _options.Remota, new[] { _options.Branch }, null, null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao executar fetch em {Remote}/{Branch}. Continuando com refs locais.", _options.Remota, _options.Branch);
                }

                var localBranch = repo.Branches[_options.Branch];
                var remoteBranch = repo.Branches[$"{_options.Remota}/{_options.Branch}"];

                if (remoteBranch == null)
                {
                    _logger.LogInformation("Branch remota não encontrada: {RemoteBranch}", $"{_options.Remota}/{_options.Branch}");
                    return new List<Model.CommitConflitado>();
                }

                var commitsRemoto = repo.Commits.QueryBy(new CommitFilter
                {
                    IncludeReachableFrom = remoteBranch,
                    ExcludeReachableFrom = localBranch
                });

                var arquivosLocais = repo.RetrieveStatus()
                    .Select(s => s.FilePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var commitConflitado = new List<Model.CommitConflitado>();

                foreach (var commit in commitsRemoto)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parent = commit.Parents.FirstOrDefault();
                    
                    if (parent == null)
                    {
                        continue;
                    }

                    var changes = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

                    foreach (var change in changes)
                    {
                        if (!arquivosLocais.Contains(change.Path))
                            continue;

                        if (commitConflitado.Any(x => string.Equals(x.Arquivo, change.Path, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        commitConflitado.Add(new Model.CommitConflitado
                        {
                            Sha = commit.Sha,
                            Autor = commit.Author.Name,
                            Email = commit.Author.Email,
                            Mensagem = commit.Message,
                            Data = commit.Author.When,
                            Arquivo = change.Path
                        });
                    }
                }

                return commitConflitado;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operação cancelada ao listar commits.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar commits com conflito.");
                return new List<Model.CommitConflitado>();
            }
        }
    }
}

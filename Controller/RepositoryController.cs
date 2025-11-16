using Microsoft.Extensions.Logging;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class RepositoryController
    {
        private readonly IGitRepositoryService _gitService;
        private readonly ILogger<RepositoryController> _logger;

        public RepositoryController(IGitRepositoryService gitService, ILogger<RepositoryController> logger)
        {
            _gitService = gitService;
            _logger = logger;
        }

        public Task<List<CommitConflitado>> ListarCommitsComConflitoAsync(CancellationToken cancellationToken = default)
            => _gitService.ListarCommitsComConflitoAsync(cancellationToken);
    }
}
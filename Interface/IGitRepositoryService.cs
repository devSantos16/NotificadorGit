using NotificadorGit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificadorGit.Interface
{
    internal interface IGitRepositoryService
    {
        Task<List<Branch>> ListarBranchesComConflitoAsync(CancellationToken cancellationToken = default);
    }
}

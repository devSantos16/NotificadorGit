using LibGit2Sharp;
using NotificadorGit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificadorGit.Controller
{
    internal class RepositoryController
    {
        private readonly Repository _repository;

        public RepositoryController(string repoPath)
        {
            _repository = new Repository(repoPath);
        }

        public List<CommitModel> listarCommitsComConflito()
        {
            var commitsRemoto = listarCommitsRemotos();
            var arquivoLocaisModificados = listarCommitsLocais();
            return this.listarCommitsComConflito(commitsRemoto, arquivoLocaisModificados);
        }

        private ICommitLog listarCommitsRemotos()
        {
            Commands.Fetch(_repository, "origin", new string[] { "main" }, null, null);

            var localBranch = _repository.Branches["main"];
            var remoteBranch = _repository.Branches["origin/main"];

            var commitsRemoto = _repository.Commits.QueryBy(new CommitFilter
            {
                IncludeReachableFrom = remoteBranch,
                ExcludeReachableFrom = localBranch
            });

            return commitsRemoto;
        }

        private HashSet<String> listarCommitsLocais()
        {
            var status = _repository.RetrieveStatus();
            return status.Select(s => s.FilePath).ToHashSet();
        }

        private List<CommitModel> listarCommitsComConflito(ICommitLog commitsRemoto, HashSet<String> arquivosLocais)
        {
            var commitsModel = new List<CommitModel>();

            foreach (var commit in commitsRemoto)
            {
                var parent = commit.Parents.FirstOrDefault();
                
                if (parent == null) {
                    return null;
                }

                var changes = _repository.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);
                
                foreach (var change in changes)
                {
                    if (!arquivosLocais.Contains(change.Path))
                    {
                        continue;
                    }

                    if (commitsModel.Any(x => x.Arquivo.Contains(change.Path)))
                    {
                        continue;
                    }

                    commitsModel.Add(new CommitModel
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

            return commitsModel;
        }
    }
}

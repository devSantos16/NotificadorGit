using LibGit2Sharp;
using NotificadorGit.Controller;
using NotificadorGit.Model;

namespace NotificadorGit
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Repositório de Teste Local
            string repoPath = @"C:\dev\repoTeste";

            RepositoryController repoController = new RepositoryController(repoPath);
            while (true)
            {
                Thread.Sleep(20000);
                var commits = repoController.listarCommitsComConflito();
                if (commits != null && commits.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("════════════════════════════════════════════════════════");
                    Console.WriteLine($"⚠️  Possíveis conflitos encontrados: {commits.Count}");
                    Console.WriteLine("════════════════════════════════════════════════════════");

                    int i = 1;
                    foreach (var c in commits)
                    {
                        PrintCommitInfo(c, i++);
                    }

                    Console.WriteLine("════════════════════════════════════════════════════════");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("Nenhum commit com conflito encontrado.");
                }
            }

        }
        private static void PrintCommitInfo(CommitModel commit, int index)
        {
            Console.WriteLine($"{index}) {commit.Sha} - {commit.Mensagem}");
            Console.WriteLine($"   Autor : {commit.Autor} <{commit.Email}>");
            Console.WriteLine($"   Data  : {commit.Data}");
            Console.WriteLine();
        }
    }
}

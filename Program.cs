using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificadorGit.Controller;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using NotificadorGit.Service;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;

namespace NotificadorGit
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    services.AddLogging();
                    services.Configure<GitOptions>(opcoes =>
                    {
                        opcoes.CaminhoRepositorio = @"C:\dev\repoTeste";
                        opcoes.Branch = "main";
                        opcoes.Remota = "origin";
                    });
                    services.Configure<GeminiOptions>(opts =>
                    {
                        opts.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
                        opts.Model = "gemini-2.0-flash";
                        opts.BaseUrl = "https://generativelanguage.googleapis.com";
                    });

                    services.AddHttpClient<IGeminiService, GeminiService>();
                    services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
                    services.AddTransient<RepositoryController>();
                    services.AddTransient<GeminiController>();
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var controller = scope.ServiceProvider.GetRequiredService<Controller.RepositoryController>();
            var geminiController = scope.ServiceProvider.GetRequiredService<Controller.GeminiController>();
            

            try
            {
                var commits = await controller.ListarBranchesComConflito();
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Erro na execução");
            }
        }
    }
}

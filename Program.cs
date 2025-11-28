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
                    services.Configure<GitOpcoes>(opcoes =>
                    {
                        opcoes.CaminhoRepositorio = @"C:\dev\repoTeste";
                        opcoes.Branch = "main";
                        opcoes.Remota = "origin";
                    });
                    services.Configure<IAOpcoes>(opts =>
                    {
                        opts.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
                        opts.Model = "gemini-2.0-flash";
                        opts.BaseUrl = "https://generativelanguage.googleapis.com";
                    });

                    services.AddHttpClient<IIAService, GeminiService>();
                    services.AddSingleton<IGitRepositorioService, GitRepositorioService>();

                    services.AddTransient<GitRepositorioController>();
                    services.AddTransient<IAController>();
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var controller = scope.ServiceProvider.GetRequiredService<Controller.GitRepositorioController>();

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

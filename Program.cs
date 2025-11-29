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
                .ConfigureServices((ctx, servicos) =>
                {
                    servicos.AddLogging();
                    servicos.Configure<GitOpcoes>(opcoes =>
                    {
                        opcoes.CaminhoRepositorio = @"C:\dev\repoTeste";
                        opcoes.Branch = "main";
                        opcoes.Remota = "origin";
                    });
                    servicos.Configure<IAOpcoes>(opts =>
                    {
                        opts.ChaveApi = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
                        opts.Modelo = "gemini-2.0-flash";
                        opts.Url = "https://generativelanguage.googleapis.com";
                    });

                    servicos.AddHttpClient<IIAService, GeminiService>();
                    servicos.AddSingleton<IGitRepositorioService, GitRepositorioService>();

                    servicos.AddTransient<GitRepositorioController>();
                    servicos.AddTransient<IAController>();
                })
                .Build();

            using var escopo = host.Services.CreateScope();
            var controller = escopo.ServiceProvider.GetRequiredService<Controller.GitRepositorioController>();

            try
            {
                var branches = await controller.ListarBranchesComConflito();
            }
            catch (Exception ex)
            {
                var logger = escopo.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Erro na execução");
            }
        }
    }
}

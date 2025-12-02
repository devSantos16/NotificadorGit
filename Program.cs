using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificadorGit.Controller;
using NotificadorGit.Interface;
using NotificadorGit.Model;
using NotificadorGit.Service;

namespace NotificadorGit
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((ctx, servicos) =>
                {
                    servicos.AddLogging();
                    servicos.Configure<GitOpcoes>(ctx.Configuration.GetSection("GitOpcoes"));
                    servicos.Configure<IAOpcoes>(ctx.Configuration.GetSection("IAOpcoes"));

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

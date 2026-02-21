using Microsoft.Extensions.DependencyInjection;
using Soenneker.Lucide.Runners.Enums.Icons.Utils;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Managers.Runners.Registrars;

namespace Soenneker.Lucide.Runners.Enums.Icons;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddScoped<IFileOperationsUtil, FileOperationsUtil>()
                .AddRunnersManagerAsScoped();

        return services;
    }
}

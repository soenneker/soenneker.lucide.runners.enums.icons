using Microsoft.Extensions.DependencyInjection;
using Soenneker.Git.Util.Registrars;
using Soenneker.Lucide.Runners.Enums.Icons.Utils;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Dotnet.Registrars;
using Soenneker.Utils.Dotnet.NuGet.Registrars;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.Lucide.Runners.Enums.Icons;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    /// <summary>
    /// Configures services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    /// <summary>
    /// Sets up io c.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The result of the operation.</returns>
    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
                .AddDirectoryUtilAsSingleton()
                .AddDotnetUtilAsSingleton()
                .AddDotnetNuGetUtilAsSingleton()
                .AddFileUtilAsSingleton()
                .AddGitUtilAsSingleton();

        return services;
    }
}

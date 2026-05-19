using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Soenneker.Git.Util.Registrars;
using Soenneker.TestHosts.Unit;
using Soenneker.Utils.Test;
using Soenneker.Lucide.Runners.Enums.Icons.Utils;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Dotnet.Registrars;
using Soenneker.Utils.Dotnet.NuGet.Registrars;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.Lucide.Runners.Enums.Icons.Tests;

public sealed class Host : UnitTestHost
{
    public override Task InitializeAsync()
    {
        SetupIoC(Services);

        return base.InitializeAsync();
    }

    private static void SetupIoC(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: false);
        });

        IConfiguration config = new ConfigurationBuilder()
                                .AddConfiguration(TestUtil.BuildConfig())
                                .AddInMemoryCollection(new Dictionary<string, string?>
                                {
                                    ["Git:Token"] = "test-token",
                                    ["Git:Name"] = "Test User",
                                    ["Git:Email"] = "test@example.com",
                                    ["Git:DefaultBranch"] = "main",
                                    ["Git:Log"] = "false"
                                })
                                .Build();
        services.AddSingleton(config);

        services.AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
                .AddDirectoryUtilAsSingleton()
                .AddDotnetUtilAsSingleton()
                .AddDotnetNuGetUtilAsSingleton()
                .AddFileUtilAsSingleton()
                .AddGitUtilAsSingleton();
    }
}

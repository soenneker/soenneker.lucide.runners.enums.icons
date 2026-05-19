using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.PooledStringBuilders;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.Lucide.Runners.Enums.Icons.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string CSharpKeywordSuffix = "Icon";

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal",
        "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float",
        "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return",
        "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IGitUtil _gitUtil;
    private readonly IProcessUtil _processUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IGitUtil gitUtil, IProcessUtil processUtil)
    {
        _logger = logger;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _gitUtil = gitUtil;
        _processUtil = processUtil;
    }

    public async ValueTask Process(CancellationToken cancellationToken)
    {
        string workingDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        string upstreamDirectory = Path.Combine(workingDirectory, "lucide");
        string targetDirectory = Path.Combine(workingDirectory, Constants.TargetRepository);

        try
        {
            await _gitUtil.Clone(Constants.UpstreamRepositoryUrl, upstreamDirectory, shallow: true, cancellationToken: cancellationToken);
            string upstreamCommit = (await _gitUtil.Run("rev-parse HEAD", upstreamDirectory, cancellationToken: cancellationToken))[0].Trim();

            string generatedEnum = await GenerateEnum(upstreamDirectory, cancellationToken);

            await _gitUtil.Clone($"https://github.com/soenneker/{Constants.TargetRepository}.git", targetDirectory, shallow: true, cancellationToken: cancellationToken);

            string enumPath = Path.Combine(targetDirectory, "src", Constants.Library, Constants.EnumFileName);
            string? existingEnum = await _fileUtil.TryRead(enumPath, cancellationToken: cancellationToken);

            if (StringComparer.Ordinal.Equals(existingEnum, generatedEnum))
            {
                _logger.LogInformation("Lucide enum is already current at upstream commit {UpstreamCommit}", upstreamCommit);
                return;
            }

            await _fileUtil.Write(enumPath, generatedEnum, cancellationToken: cancellationToken);

            string projectPath = Path.Combine(targetDirectory, "src", Constants.Library, $"{Constants.Library}.csproj");

            await RunProcess("dotnet", $"restore \"{projectPath}\" --verbosity minimal", targetDirectory, cancellationToken);
            await RunProcess("dotnet", $"build \"{projectPath}\" --configuration Release --no-restore --verbosity minimal", targetDirectory, cancellationToken);

            string version = GetRequiredEnvironmentVariable("BUILD_VERSION");
            await RunProcess("dotnet", $"pack \"{projectPath}\" --configuration Release --no-build --no-restore --output \"{targetDirectory}\" /p:PackageVersion={version} --verbosity minimal",
                targetDirectory, cancellationToken);

            string packagePath = Path.Combine(targetDirectory, $"{Constants.Library}.{version}.nupkg");
            string apiKey = GetRequiredEnvironmentVariable("NUGET__TOKEN");
            await RunProcess("dotnet", $"nuget push \"{packagePath}\" --api-key \"{apiKey}\" --source https://api.nuget.org/v3/index.json --skip-duplicate",
                targetDirectory, cancellationToken);

            await CommitAndPush(targetDirectory, upstreamCommit, cancellationToken);

            _logger.LogInformation("Updated {Library} from lucide-icons/lucide commit {UpstreamCommit}", Constants.Library, upstreamCommit);
        }
        finally
        {
            await _directoryUtil.DeleteIfExists(workingDirectory, cancellationToken);
        }
    }

    private async ValueTask<string> GenerateEnum(string upstreamDirectory, CancellationToken cancellationToken)
    {
        string iconsDirectory = Path.Combine(upstreamDirectory, "icons");

        List<string> iconFiles = await _directoryUtil.GetFilesByExtension(iconsDirectory, ".svg", cancellationToken: cancellationToken);

        string[] enumMembers = iconFiles.Select(Path.GetFileNameWithoutExtension)
                                        .Where(name => !string.IsNullOrWhiteSpace(name))
                                        .Select(name => name!)
                                        .OrderBy(name => name, StringComparer.Ordinal)
                                        .Select(ToEnumMemberName)
                                        .ToArray();

        using var builder = new PooledStringBuilder();
        builder.Append("namespace Soenneker.Lucide.Enums.Icons;\n");
        builder.Append('\n');
        builder.Append("public enum LucideIcon\n");
        builder.Append("{\n");

        for (var i = 0; i < enumMembers.Length; i++)
        {
            builder.Append("    ");
            builder.Append(enumMembers[i]);

            if (i < enumMembers.Length - 1)
                builder.Append(',');

            builder.Append('\n');
        }

        builder.Append("}\n");

        return builder.ToString();
    }

    private static string ToEnumMemberName(string iconName)
    {
        string memberName = string.Concat(iconName.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(ToPascalToken));

        if (CSharpKeywords.Contains(iconName))
            memberName += CSharpKeywordSuffix;

        return memberName;
    }

    private static string ToPascalToken(string token)
    {
        using var builder = new PooledStringBuilder(token.Length);
        var capitalizeNextLetter = true;

        foreach (char character in token)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
                capitalizeNextLetter = true;
                continue;
            }

            builder.Append(capitalizeNextLetter ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            capitalizeNextLetter = false;
        }

        return builder.ToString();
    }

    private async ValueTask CommitAndPush(string targetDirectory, string upstreamCommit, CancellationToken cancellationToken)
    {
        if (!await _gitUtil.HasWorkingTreeChanges(targetDirectory, cancellationToken))
            return;

        string name = GetRequiredEnvironmentVariable("GIT__NAME");
        string email = GetRequiredEnvironmentVariable("GIT__EMAIL");
        string token = GetRequiredEnvironmentVariable("GH__TOKEN");

        await _gitUtil.CommitAndPush(targetDirectory, $"Update Lucide icons enum from upstream {upstreamCommit[..12]}", token, name, email, cancellationToken);
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is not set");

        return value;
    }

    private ValueTask<string> RunProcess(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        return _processUtil.StartAndGetOutput(fileName, arguments, workingDirectory, cancellationToken: cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.Hashing.XxHash;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Dotnet.NuGet.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.PooledStringBuilders;

namespace Soenneker.Lucide.Runners.Enums.Icons.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string HashFileName = "hash.txt";
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
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IDotnetNuGetUtil _dotnetNuGetUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IGitUtil gitUtil, IDotnetUtil dotnetUtil,
        IDotnetNuGetUtil dotnetNuGetUtil)
    {
        _logger = logger;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _dotnetNuGetUtil = dotnetNuGetUtil;
    }

    public async ValueTask Process(CancellationToken cancellationToken)
    {
        string upstreamDirectory = await _gitUtil.CloneToTempDirectory(Constants.UpstreamRepositoryUrl, cancellationToken: cancellationToken);
        string targetDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.TargetRepository}", cancellationToken: cancellationToken);

        try
        {
            string upstreamCommit = (await _gitUtil.Run("rev-parse HEAD", upstreamDirectory, log: false, cancellationToken: cancellationToken))[0].Trim();

            string generatedEnum = await GenerateEnum(upstreamDirectory, cancellationToken);
            string newHash = XxHash3Util.Hash(generatedEnum);

            string enumPath = Path.Combine(targetDirectory, "src", Constants.Library, Constants.EnumFileName);
            string hashPath = Path.Combine(targetDirectory, HashFileName);
            string? existingHash = await _fileUtil.TryRead(hashPath, cancellationToken: cancellationToken);

            if (StringComparer.Ordinal.Equals(existingHash?.Trim(), newHash))
            {
                _logger.LogInformation("Lucide enum hash is already current at upstream commit {UpstreamCommit}", upstreamCommit);
                return;
            }

            await _fileUtil.Write(enumPath, generatedEnum, cancellationToken: cancellationToken);
            await _fileUtil.Write(hashPath, newHash, cancellationToken: cancellationToken);

            string projectPath = Path.Combine(targetDirectory, "src", Constants.Library, $"{Constants.Library}.csproj");

            await RestoreBuildPackAndPush(projectPath, targetDirectory, cancellationToken);

            await CommitAndPush(targetDirectory, upstreamCommit, cancellationToken);

            _logger.LogInformation("Updated {Library} from lucide-icons/lucide commit {UpstreamCommit}", Constants.Library, upstreamCommit);
        }
        finally
        {
            await _directoryUtil.DeleteIfExists(upstreamDirectory, cancellationToken);
            await _directoryUtil.DeleteIfExists(targetDirectory, cancellationToken);
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

    private async ValueTask RestoreBuildPackAndPush(string projectPath, string targetDirectory, CancellationToken cancellationToken)
    {
        await _dotnetUtil.Restore(projectPath, verbosity: "minimal", cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projectPath, configuration: "Release", restore: false, verbosity: "minimal", cancellationToken: cancellationToken);

        if (!successful)
            throw new InvalidOperationException($"{Constants.Library} build failed");

        string version = GetRequiredEnvironmentVariable("BUILD_VERSION");
        await _dotnetUtil.Pack(projectPath, version, configuration: "Release", build: false, restore: false, output: targetDirectory, verbosity: "minimal",
            cancellationToken: cancellationToken);

        string packagePath = Path.Combine(targetDirectory, $"{Constants.Library}.{version}.nupkg");
        string apiKey = GetRequiredEnvironmentVariable("NUGET__TOKEN");
        await _dotnetNuGetUtil.Push(packagePath, apiKey: apiKey, skipDuplicate: true, cancellationToken: cancellationToken);
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is not set");

        return value;
    }

}

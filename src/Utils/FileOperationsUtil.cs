using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Utils.PooledStringBuilders;
using Microsoft.Extensions.Logging;
using Soenneker.Git.Util.Abstract;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Utils.Case;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Dotnet.NuGet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.SHA3.Abstract;

namespace Soenneker.Lucide.Runners.Enums.Icons.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private static readonly HashSet<string> _cSharpReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "private", "protected", "public", "readonly",
        "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IDotnetNuGetUtil _dotnetNuGetUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly ISha3Util _sha3Util;

    private string? _newHash;

    private const bool _overrideHash = false;

    public FileOperationsUtil(IFileUtil fileUtil, ILogger<FileOperationsUtil> logger, IGitUtil gitUtil, IDotnetUtil dotnetUtil,
        IDotnetNuGetUtil dotnetNuGetUtil, IDirectoryUtil directoryUtil, ISha3Util sha3Util)
    {
        _fileUtil = fileUtil;
        _logger = logger;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _dotnetNuGetUtil = dotnetNuGetUtil;
        _directoryUtil = directoryUtil;
        _sha3Util = sha3Util;
    }

    public async ValueTask Process(CancellationToken cancellationToken)
    {
        string iconsGitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.IconsLibrary}",
            cancellationToken: cancellationToken);

        string enumsGitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.EnumsIconsLibrary}",
            cancellationToken: cancellationToken);

        string resourceDirectory = Path.Combine(iconsGitDirectory, "src", "Resources");

        bool needToUpdate = await CheckForHashDifferences(enumsGitDirectory, resourceDirectory, cancellationToken);

        if (!needToUpdate)
            return;

        await BuildPackAndPush(enumsGitDirectory, resourceDirectory, cancellationToken);

        await SaveHashToGitRepo(enumsGitDirectory, cancellationToken);
    }

    private async ValueTask BuildPackAndPush(string enumsGitDirectory, string resourceDirectory, CancellationToken cancellationToken)
    {
        List<string> iconNames = await GetIconNamesFromResources(resourceDirectory, cancellationToken);

        string lucideIconPath = Path.Combine(enumsGitDirectory, "src", "LucideIcon.cs");
        string enumContent = GenerateLucideIconEnum(iconNames);

        await _fileUtil.Write(lucideIconPath, enumContent, true, cancellationToken);

        _logger.LogInformation("Generated LucideIcon enum with {Count} icons", iconNames.Count);

        string projFilePath = Path.Combine(enumsGitDirectory, "src", "Soenneker.Lucide.Enums.Icons.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
        {
            _logger.LogError("Build was not successful, exiting...");
            return;
        }

        string version = EnvironmentUtil.GetVariableStrict("BUILD_VERSION");

        await _dotnetUtil.Pack(projFilePath, version, true, "Release", false, false, enumsGitDirectory, cancellationToken: cancellationToken);

        string apiKey = EnvironmentUtil.GetVariableStrict("NUGET__TOKEN");

        string nuGetPackagePath = Path.Combine(enumsGitDirectory, $"Soenneker.Lucide.Enums.Icons.{version}.nupkg");

        await _dotnetNuGetUtil.Push(nuGetPackagePath, apiKey: apiKey, cancellationToken: cancellationToken);
    }

    private async ValueTask<List<string>> GetIconNamesFromResources(string resourceDirectory, CancellationToken cancellationToken)
    {
        List<string> svgFiles = await _directoryUtil.GetFilesByExtension(resourceDirectory, "svg", true, cancellationToken);
        return svgFiles
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static string GenerateLucideIconEnum(List<string> iconNames)
    {
        using var sb = new PooledStringBuilder();
        sb.AppendLine("namespace Soenneker.Lucide.Enums.Icons;");
        sb.AppendLine();
        sb.AppendLine("public enum LucideIcon");
        sb.AppendLine("{");

        for (var i = 0; i < iconNames.Count; i++)
        {
            string iconName = iconNames[i];
            string enumMemberName = CaseUtil.ToPascal(iconName);

            if (_cSharpReservedWords.Contains(enumMemberName))
                enumMemberName += "Icon";

            sb.Append("    ");
            sb.Append(enumMemberName);

            if (i < iconNames.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private async ValueTask<bool> CheckForHashDifferences(string enumsGitDirectory, string resourceDirectory, CancellationToken cancellationToken)
    {
        List<string> iconNames = await GetIconNamesFromResources(resourceDirectory, cancellationToken);
        string combinedNames = string.Join("\n", iconNames);
        _newHash = _sha3Util.HashString(combinedNames);

        string? oldHash = await _fileUtil.TryRead(Path.Combine(enumsGitDirectory, "hash.txt"), true, cancellationToken);

        if (oldHash == null)
        {
            _logger.LogDebug("Could not read hash from repository, proceeding to update...");
            return true;
        }

        if (_overrideHash)
        {
            _logger.LogWarning("Overriding hash check...");
        }
        else if (oldHash == _newHash)
        {
            _logger.LogInformation("Hashes are equal, no need to update, exiting...");
            return false;
        }

        return true;
    }

    private async ValueTask SaveHashToGitRepo(string enumsGitDirectory, CancellationToken cancellationToken)
    {
        string targetHashFile = Path.Combine(enumsGitDirectory, "hash.txt");

        await _fileUtil.DeleteIfExists(targetHashFile, cancellationToken: cancellationToken);

        await _fileUtil.Write(targetHashFile, _newHash!, true, cancellationToken);

        if (await _gitUtil.IsRepositoryDirty(enumsGitDirectory, cancellationToken))
        {
            _logger.LogInformation("Changes have been detected in the repository, committing and pushing...");

            string name = EnvironmentUtil.GetVariableStrict("GIT__NAME");
            string email = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");
            string token = EnvironmentUtil.GetVariableStrict("GH__TOKEN");

            await _gitUtil.Commit(enumsGitDirectory, "Update LucideIcon enum from Resources", name, email, cancellationToken);

            await _gitUtil.Push(enumsGitDirectory, token, cancellationToken);
        }
        else
        {
            _logger.LogInformation("There are no changes to commit");
        }
    }
}

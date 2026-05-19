using System;
using Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Lucide.Runners.Enums.Icons.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class LucideIconsEnumRunnerTests : HostedUnitTest
{
    private readonly IFileOperationsUtil _fileOperationsUtil;

    public LucideIconsEnumRunnerTests(Host host) : base(host)
    {
        _fileOperationsUtil = Resolve<IFileOperationsUtil>(true);
    }

    [Test]
    public void Default()
    {
        if (_fileOperationsUtil is null)
            throw new InvalidOperationException("Could not resolve file operations util");
    }
}

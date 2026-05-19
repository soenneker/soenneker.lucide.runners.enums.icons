using Soenneker.Lucide.Runners.Enums.Icons.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Lucide.Runners.Enums.Icons.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class LucideIconsEnumRunnerTests : HostedUnitTest
{
    private readonly ILucideIconsEnumRunner _runner;

    public LucideIconsEnumRunnerTests(Host host) : base(host)
    {
        _runner = Resolve<ILucideIconsEnumRunner>(true);
    }

    [Test]
    public void Default()
    {

    }
}

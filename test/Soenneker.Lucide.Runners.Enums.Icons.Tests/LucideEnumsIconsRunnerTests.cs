using Soenneker.Tests.HostedUnit;

namespace Soenneker.Lucide.Runners.Enums.Icons.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class LucideEnumsIconsRunnerTests : HostedUnitTest
{
    public LucideEnumsIconsRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}

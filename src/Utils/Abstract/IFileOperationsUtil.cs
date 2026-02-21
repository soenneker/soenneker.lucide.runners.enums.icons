using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;

public interface IFileOperationsUtil
{
    ValueTask Process(CancellationToken cancellationToken);
}

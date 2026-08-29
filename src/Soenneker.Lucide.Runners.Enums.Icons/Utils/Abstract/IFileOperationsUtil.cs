using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Lucide.Runners.Enums.Icons.Utils.Abstract;

/// <summary>
/// Defines the file operations util contract.
/// </summary>
public interface IFileOperationsUtil
{
    /// <summary>
    /// Processes the pending work managed by the file operations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the full processing workflow has finished.</returns>
    ValueTask Process(CancellationToken cancellationToken);
}

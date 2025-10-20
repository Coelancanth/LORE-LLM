using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Chat;

public interface IChatProvider
{
    string Name { get; }

    Task<Result<string>> CompleteAsync(string prompt, CancellationToken cancellationToken);
}



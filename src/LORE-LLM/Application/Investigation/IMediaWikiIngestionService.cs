using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Domain.Knowledge;

namespace LORE_LLM.Application.Investigation;

public interface IMediaWikiIngestionService
{
    Task<Result<KnowledgeBaseDocument>> EnsureKnowledgeBaseAsync(
        DirectoryInfo projectDirectory,
        string sanitizedProject,
        string projectDisplayName,
        IEnumerable<string> candidateTokens,
        bool forceRefresh,
        CancellationToken cancellationToken);
}

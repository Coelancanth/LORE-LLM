using System.IO;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed record ClusterCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    string Provider,
    int BatchSize,
    bool IncludeEmpty,
    FileInfo? PromptTemplate,
    bool SaveTranscript)
{
    public int MaxSegments { get; init; } = 0;
}



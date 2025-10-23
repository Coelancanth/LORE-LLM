using System.IO;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed record ClusterPrepCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    bool IncludeEmpty);



using System.IO;

namespace LORE_LLM.Application.Commands.Crawl;

public sealed record WikiCrawlCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    bool ForceRefresh,
    string[]? Pages,
    int MaxPages);

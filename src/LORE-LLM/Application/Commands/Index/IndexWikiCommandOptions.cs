using System.IO;

namespace LORE_LLM.Application.Commands.Index;

public sealed record IndexWikiCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    bool ForceRefresh);



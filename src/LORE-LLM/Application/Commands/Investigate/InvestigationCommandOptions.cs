using System.IO;

namespace LORE_LLM.Application.Commands.Investigate;

public sealed record InvestigationCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    bool ForceRefresh);

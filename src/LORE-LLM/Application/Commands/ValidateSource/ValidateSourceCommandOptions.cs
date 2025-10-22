namespace LORE_LLM.Application.Commands.ValidateSource;

public sealed record ValidateSourceCommandOptions(
    DirectoryInfo Workspace,
    string? Project = null);



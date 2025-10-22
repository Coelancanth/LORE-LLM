namespace LORE_LLM.Application.Commands.EnrichMetadata;

public sealed record EnrichMetadataCommandOptions(
    DirectoryInfo Workspace,
    string Project,
    FileInfo? Config = null);



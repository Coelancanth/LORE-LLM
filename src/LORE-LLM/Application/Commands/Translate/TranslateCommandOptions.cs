namespace LORE_LLM.Application.Commands.Translate;

public sealed record TranslateCommandOptions(DirectoryInfo Workspace, string TargetLanguage);

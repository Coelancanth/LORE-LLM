namespace LORE_LLM.Application.Commands.Extract;

public sealed record ExtractCommandOptions(FileInfo Input, DirectoryInfo Output, string Project);

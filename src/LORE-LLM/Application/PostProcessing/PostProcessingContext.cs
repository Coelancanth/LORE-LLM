using System.IO;

namespace LORE_LLM.Application.PostProcessing;

public sealed record PostProcessingContext(
    string ProjectDisplayName,
    string SanitizedProjectName,
    DirectoryInfo ProjectDirectory);

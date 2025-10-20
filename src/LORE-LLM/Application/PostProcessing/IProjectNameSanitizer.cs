namespace LORE_LLM.Application.PostProcessing;

public interface IProjectNameSanitizer
{
    string Sanitize(string project);
}

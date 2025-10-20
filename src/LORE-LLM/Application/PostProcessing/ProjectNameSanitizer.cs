using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LORE_LLM.Application.PostProcessing;

public sealed class ProjectNameSanitizer : IProjectNameSanitizer
{
    public string Sanitize(string project)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            return "default";
        }

        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var builder = new StringBuilder();
        foreach (var ch in project.Trim())
        {
            var normalized = char.IsWhiteSpace(ch) ? '-' : char.ToLowerInvariant(ch);
            if (invalid.Contains(normalized) || normalized == Path.DirectorySeparatorChar || normalized == Path.AltDirectorySeparatorChar)
            {
                builder.Append('-');
            }
            else
            {
                builder.Append(normalized);
            }
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }
}

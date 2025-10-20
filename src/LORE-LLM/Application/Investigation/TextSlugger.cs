using System.Text;

namespace LORE_LLM.Application.Investigation;

internal static class TextSlugger
{
    public static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "entry";
        }

        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "entry" : slug;
    }
}

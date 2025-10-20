using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;

namespace LORE_LLM.Application.Chat;

public sealed class LocalChatProvider : IChatProvider
{
    private static readonly Regex IdLineRegex = new("^-\\s*id:\\s*(?<id>.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public string Name => "local";

    public Task<Result<string>> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        foreach (Match match in IdLineRegex.Matches(prompt))
        {
            var id = match.Groups["id"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            return Task.FromResult(Result.Failure<string>("No segment ids found in prompt."));
        }

        var payload = new[]
        {
            new
            {
                clusterId = "cluster:local",
                memberIds = ids
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Task.FromResult(Result.Success(json));
    }
}



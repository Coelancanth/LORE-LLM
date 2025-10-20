using System.Text.Json.Serialization;

namespace LORE_LLM.Application.Clustering;

public sealed record ClusterConfiguration
{
    [JsonPropertyName("maxClusters")]
    public int? MaxClusters { get; init; }
}



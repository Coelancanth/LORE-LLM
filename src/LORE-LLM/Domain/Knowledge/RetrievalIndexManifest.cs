using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LORE_LLM.Domain.Knowledge;

public sealed class RetrievalIndexManifest
{
	[JsonConstructor]
	public RetrievalIndexManifest(DateTimeOffset generatedAt, IReadOnlyList<RetrievalProviderInfo> providers)
	{
		GeneratedAt = generatedAt;
		Providers = providers ?? Array.Empty<RetrievalProviderInfo>();
	}

	public DateTimeOffset GeneratedAt { get; }

	public IReadOnlyList<RetrievalProviderInfo> Providers { get; }
}

public sealed class RetrievalProviderInfo
{
	[JsonConstructor]
	public RetrievalProviderInfo(string name, string? artifact, string? hash, IReadOnlyDictionary<string, JsonElement>? config)
	{
		Name = name;
		Artifact = artifact;
		Hash = hash;
		Config = config ?? new Dictionary<string, JsonElement>();
	}

	public string Name { get; }

	public string? Artifact { get; }

	public string? Hash { get; }

	public IReadOnlyDictionary<string, JsonElement> Config { get; }
}



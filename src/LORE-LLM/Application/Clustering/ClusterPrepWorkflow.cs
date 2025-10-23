using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using LORE_LLM.Application.Commands.Cluster;
using LORE_LLM.Application.PostProcessing;
using LORE_LLM.Domain.Clusters;
using LORE_LLM.Domain.Extraction;

namespace LORE_LLM.Application.Clustering;

public interface IClusterPrepWorkflow
{
    Task<Result<int>> RunAsync(ClusterPrepCommandOptions options, CancellationToken cancellationToken);
}

public sealed class ClusterPrepWorkflow : IClusterPrepWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IProjectNameSanitizer _projectNameSanitizer;

    public ClusterPrepWorkflow(IProjectNameSanitizer projectNameSanitizer)
    {
        _projectNameSanitizer = projectNameSanitizer;
    }

    public async Task<Result<int>> RunAsync(ClusterPrepCommandOptions options, CancellationToken cancellationToken)
    {
        if (!options.Workspace.Exists)
        {
            return Result.Failure<int>("Workspace directory not found.");
        }

        var sanitizedProject = _projectNameSanitizer.Sanitize(options.Project);
        var projectDirectory = new DirectoryInfo(Path.Combine(options.Workspace.FullName, sanitizedProject));
        if (!projectDirectory.Exists)
        {
            return Result.Failure<int>($"Project directory not found: {projectDirectory.FullName}");
        }

        var sourcePath = Path.Combine(projectDirectory.FullName, "source_text_raw.json");
        if (!File.Exists(sourcePath))
        {
            return Result.Failure<int>($"Required artifact missing: {sourcePath}");
        }

        var doc = await ReadSourceDocumentAsync(sourcePath, cancellationToken);
        if (doc is null)
        {
            return Result.Failure<int>("Failed to deserialize source_text_raw.json.");
        }

        var segments = doc.Segments.Where(s => options.IncludeEmpty || !s.IsEmpty).ToList();
        if (segments.Count == 0)
        {
            return Result.Failure<int>("No segments to process.");
        }

        // Deterministic grouping heuristics for Ren'Py-like sources
        // Grouping key: (sourceRelPath, translationBlock, blockInstance, entryType, speaker)
        static string KeyFor(SourceSegment s)
        {
            var md = s.Metadata ?? new Dictionary<string, string>();
            md.TryGetValue("sourceRelPath", out var sourceRelPath);
            md.TryGetValue("translationBlock", out var translationBlock);
            md.TryGetValue("blockInstance", out var blockInstance);
            md.TryGetValue("entryType", out var entryType);
            md.TryGetValue("speaker", out var speaker);

            // Keep Ren'Py blocks intact; for character_line split per speaker
            var typePart = string.IsNullOrWhiteSpace(entryType) ? "_" : entryType;
            var speakerPart = typePart == "character_line" ? (string.IsNullOrWhiteSpace(speaker) ? "_" : speaker) : "_";
            return string.Join("|", new[]
            {
                sourceRelPath ?? "_",
                translationBlock ?? "_",
                blockInstance ?? "_",
                typePart,
                speakerPart
            });
        }

        var groups = segments
            .GroupBy(KeyFor)
            .OrderBy(g => g.Key)
            .ToList();

        var clusters = new List<ClusterContext>(capacity: groups.Count);
        foreach (var group in groups)
        {
            var parts = group.Key.Split('|');
            var sourceRelPath = parts.ElementAtOrDefault(0) ?? "_";
            var translationBlock = parts.ElementAtOrDefault(1) ?? "_";
            var blockInstance = parts.ElementAtOrDefault(2) ?? "_";
            var entryType = parts.ElementAtOrDefault(3) ?? "_";
            var speaker = parts.ElementAtOrDefault(4) ?? "_";

            var baseId = $"pre:{sourceRelPath}:{translationBlock}:{blockInstance}:{entryType}".Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(speaker) && speaker != "_")
            {
                baseId += $":{speaker}";
            }

            var memberIds = group.Select(s => s.Id).ToList();
            var shared = new List<string>();
            if (!string.IsNullOrWhiteSpace(translationBlock)) shared.Add($"block={translationBlock}");
            if (!string.IsNullOrWhiteSpace(blockInstance)) shared.Add($"instance={blockInstance}");
            if (!string.IsNullOrWhiteSpace(entryType)) shared.Add($"type={entryType}");
            if (!string.IsNullOrWhiteSpace(speaker) && speaker != "_") shared.Add($"speaker={speaker}");

            clusters.Add(new ClusterContext(
                ClusterId: baseId,
                MemberIds: memberIds,
                SharedContext: shared,
                KnowledgeReferences: null,
                Confidence: 1.0,
                Notes: "Deterministic pre-cluster"));
        }

        var output = new ClusterDocument(
            Project: doc.Project,
            ProjectDisplayName: doc.ProjectDisplayName,
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceTextHash: doc.InputHash,
            Clusters: clusters);

        var outPath = Path.Combine(projectDirectory.FullName, "clusters_precomputed.json");
        await SerializeAsync(outPath, output, cancellationToken);

        // Update workspace manifest
        var manifestPath = Path.Combine(projectDirectory.FullName, "workspace.json");
        if (File.Exists(manifestPath))
        {
            await UpdateManifestAsync(projectDirectory, manifestPath, cancellationToken);
        }

        return Result.Success(clusters.Count);
    }

    private static async Task<T?> DeserializeAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task SerializeAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static async Task UpdateManifestAsync(DirectoryInfo projectDirectory, string manifestPath, CancellationToken cancellationToken)
    {
        WorkspaceManifest? manifest;
        await using (var manifestStream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<WorkspaceManifest>(manifestStream, JsonOptions, cancellationToken);
        }

        if (manifest is null)
        {
            return;
        }

        var artifacts = new Dictionary<string, string>(manifest.Artifacts, StringComparer.OrdinalIgnoreCase)
        {
            ["clustersPrecomputed"] = "clusters_precomputed.json"
        };

        var updatedManifest = new WorkspaceManifest(
            GeneratedAt: DateTimeOffset.UtcNow,
            Project: manifest.Project,
            ProjectDisplayName: manifest.ProjectDisplayName,
            InputPath: manifest.InputPath,
            InputHash: manifest.InputHash,
            Artifacts: artifacts);

        await using var outputStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(outputStream, updatedManifest, JsonOptions, cancellationToken);
    }

    private static async Task<SourceTextRawDocument?> ReadSourceDocumentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            // First try strict model
            var strict = await DeserializeAsync<SourceTextRawDocument>(path, cancellationToken);
            if (strict is not null)
            {
                return strict;
            }
        }
        catch
        {
            // Fall through to tolerant reader
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            static string ReadString(JsonElement e, string name, string fallback = "")
            {
                return e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? fallback
                    : fallback;
            }

            var sourcePathVal = ReadString(root, "sourcePath");
            var projectVal = ReadString(root, "project");
            var projectDisplayNameVal = ReadString(root, "projectDisplayName");
            var inputHashVal = ReadString(root, "inputHash");

            DateTimeOffset generatedAtVal = DateTimeOffset.UtcNow;
            if (root.TryGetProperty("generatedAt", out var gen) && gen.ValueKind == JsonValueKind.String)
            {
                DateTimeOffset.TryParse(gen.GetString(), out generatedAtVal);
            }

            var segments = new List<SourceSegment>();
            if (root.TryGetProperty("segments", out var segs) && segs.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in segs.EnumerateArray())
                {
                    var id = ReadString(s, "id");
                    var text = ReadString(s, "text");
                    var lineNumber = s.TryGetProperty("lineNumber", out var ln) && ln.ValueKind == JsonValueKind.Number ? ln.GetInt32() : 0;
                    bool isEmpty = s.TryGetProperty("isEmpty", out var ie) && ie.ValueKind == JsonValueKind.True || (text.Trim().Length == 0);

                    IReadOnlyDictionary<string, string>? metaDict = null;
                    if (s.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object)
                    {
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in md.EnumerateObject())
                        {
                            var pv = prop.Value;
                            map[prop.Name] = pv.ValueKind switch
                            {
                                JsonValueKind.String => pv.GetString() ?? string.Empty,
                                JsonValueKind.Number => pv.ToString(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                JsonValueKind.Null => string.Empty,
                                _ => pv.GetRawText()
                            };
                        }
                        metaDict = map;
                    }

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    segments.Add(new SourceSegment(id, text, isEmpty, lineNumber, metaDict));
                }
            }

            if (segments.Count == 0)
            {
                return null;
            }

            return new SourceTextRawDocument(sourcePathVal, generatedAtVal, projectVal, projectDisplayNameVal, inputHashVal, segments);
        }
        catch
        {
            return null;
        }
    }
}



using System.IO;

namespace LORE_LLM.Application.Commands.Cluster;

public sealed record ClusterContextCommandOptions(
	DirectoryInfo Workspace,
	string Project,
	int TopK = 5);



using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using NuGet.Versioning;

namespace NuGetUpdater.Core.Run.ApiModel;

public sealed record CreatePullRequest : MessageBase
{
    public required ReportedDependency[] Dependencies { get; init; }

    [JsonPropertyName("updated-dependency-files")]
    public required DependencyFile[] UpdatedDependencyFiles { get; init; }

    [JsonPropertyName("base-commit-sha")]
    public required string BaseCommitSha { get; init; }

    [JsonPropertyName("commit-message")]
    public required string CommitMessage { get; init; }

    [JsonPropertyName("pr-title")]
    public required string PrTitle { get; init; }

    [JsonPropertyName("pr-body")]
    public required string PrBody { get; init; }

    /// <summary>
    /// This is serialized as either `null` or `{"name": "group-name"}`.
    /// </summary>
    [JsonPropertyName("dependency-group")]
    [JsonConverter(typeof(DependencyGroupConverter))]
    public required string? DependencyGroup { get; init; }

    public override string GetReport()
    {
        var dependencyNames = Dependencies
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => NuGetVersion.Parse(d.Version!))
            .Select(d => $"{d.Name}/{d.Version}")
            .ToArray();
        var report = new StringBuilder();
        report.AppendLine(nameof(CreatePullRequest));
        foreach (var d in dependencyNames)
        {
            report.AppendLine($"- {d}");
        }

        return report.ToString().Trim();
    }

    public static ImmutableArray<CreatePullRequest> FoldPullRequestMessages(ImmutableArray<CreatePullRequest> pullRequests)
    {
        // dedup any instances of CreatePullRequest that are identical
        var dedupedPullRequests = new List<CreatePullRequest>();
        for (int i = 0; i < pullRequests.Length; i++)
        {
            var candidatePr = pullRequests[i];
            var equivalentPrIndex = dedupedPullRequests.FindIndex(pr => Equivalent(pr, candidatePr));
            if (equivalentPrIndex < 0)
            {
                // no equivalent, add it
                dedupedPullRequests.Add(candidatePr);
                continue;
            }

            // merge dependency requirements
            var equivalentPr = dedupedPullRequests[equivalentPrIndex];
            var mergedDependencies = equivalentPr.Dependencies.ToDictionary(d => $"{d.Name}/{d.Version}", d => d, StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in candidatePr.Dependencies)
            {
                var key = $"{dependency.Name}/{dependency.Version}";
                if (mergedDependencies.TryGetValue(key, out var existingDependency))
                {
                    // merge requirements
                    var mergedRequirements = existingDependency.Requirements.Union(dependency.Requirements).ToArray();
                    mergedDependencies[key] = existingDependency with { Requirements = mergedRequirements };
                }
                else
                {
                    // add new dependency
                    mergedDependencies[key] = dependency;
                }
            }

            var mergedPr = equivalentPr with { Dependencies = [.. mergedDependencies.Values] };
            dedupedPullRequests[equivalentPrIndex] = mergedPr;
        }

        return [.. dedupedPullRequests];
    }

    private static bool Equivalent(CreatePullRequest a, CreatePullRequest b)
    {
        // check top-level items
        if (a.BaseCommitSha != b.BaseCommitSha ||
            a.DependencyGroup != b.DependencyGroup ||
            a.UpdatedDependencyFiles.Length != b.UpdatedDependencyFiles.Length)
        {
            return false;
        }

        // compare dependencies
        var aDependencies = a.Dependencies.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Version).Select(d => $"{d.Name}/{d.Version}").ToArray();
        var bDependencies = b.Dependencies.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Version).Select(d => $"{d.Name}/{d.Version}").ToArray();
        if (!aDependencies.SequenceEqual(bDependencies))
        {
            return false;
        }

        // compare updated dependency files
        var aFiles = a.UpdatedDependencyFiles.Select(df => (Path.Join(df.Directory, df.Name).NormalizePathToUnix(), df.Content)).OrderBy(df => df.Item1, StringComparer.OrdinalIgnoreCase).ToArray();
        var bFiles = b.UpdatedDependencyFiles.Select(df => (Path.Join(df.Directory, df.Name).NormalizePathToUnix(), df.Content)).OrderBy(df => df.Item1, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!aFiles.SequenceEqual(bFiles))
        {
            return false;
        }

        // no disqualifying differences found
        return true;
    }

    public class DependencyGroupConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
            if (dict is not null &&
                dict.TryGetValue("name", out var name))
            {
                return name;
            }

            throw new NotSupportedException("Expected an object with a `name` property.");
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("name", value);
                writer.WriteEndObject();
            }
        }
    }
}

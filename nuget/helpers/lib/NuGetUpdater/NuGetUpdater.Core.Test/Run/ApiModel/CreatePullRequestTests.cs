using System.Text.Json;

using NuGetUpdater.Core.Run;
using NuGetUpdater.Core.Run.ApiModel;
using NuGetUpdater.Core.Test.Utilities;

using Xunit;

namespace NuGetUpdater.Core.Test.Run.ApiModel;

public class CreatePullRequestTests
{
    [Theory]
    [MemberData(nameof(FoldPullRequestMessagesTestData))]
    public void FoldPullRequestMessages(CreatePullRequest[] messages, CreatePullRequest[] expectedMessages)
    {
        var foldedMessages = CreatePullRequest.FoldPullRequestMessages([.. messages]);
        var actualMessagesJson = foldedMessages.Select(m => JsonSerializer.Serialize(m, RunWorker.SerializerOptions)).ToArray();
        var expectedMessagesJson = expectedMessages.Select(m => JsonSerializer.Serialize(m, RunWorker.SerializerOptions)).ToArray();
        AssertEx.Equal(expectedMessagesJson, actualMessagesJson);
    }

    public static IEnumerable<object[]> FoldPullRequestMessagesTestData()
    {
        // unrelated prs are not folded
        yield return
        [
            // messages
            new object[]
            {
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.A", Version = "1.2.3", Requirements = [new() { Requirement = "1.0.0", File = "/project.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "project.csproj", Content = "some content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.B", Version = "1.2.3", Requirements = [new() { Requirement = "2.0.0", File = "/project.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "project.csproj", Content = "some other content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
            },
            // expectedMessages
            new object[]
            {
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.A", Version = "1.2.3", Requirements = [new() { Requirement = "1.0.0", File = "/project.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "project.csproj", Content = "some content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.B", Version = "1.2.3", Requirements = [new() { Requirement = "2.0.0", File = "/project.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "project.csproj", Content = "some other content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
            },
        ];

        // equivalent prs are folded; dependency requirements are merged
        yield return
        [
            // messages
            new object[]
            {
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.A", Version = "1.2.3", Requirements = [new() { Requirement = "1.0.0", File = "/project1.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "Directory.Packages.props", Content = "some content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.A", Version = "1.2.3", Requirements = [new() { Requirement = "1.0.0", File = "/project2.csproj"} ] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "Directory.Packages.props", Content = "some content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
            },
            // expectedMessages
            new object[]
            {
                new CreatePullRequest()
                {
                    Dependencies = [new() { Name = "Dependency.A", Version = "1.2.3", Requirements = [new() { Requirement = "1.0.0", File = "/project1.csproj"}, new() { Requirement = "1.0.0", File = "/project2.csproj" }] }],
                    UpdatedDependencyFiles = [new() { Directory = "/", Name = "Directory.Packages.props", Content = "some content" }],
                    BaseCommitSha = "TEST-SHA",
                    CommitMessage = "commit message",
                    PrTitle = "pr title",
                    PrBody = "pr body",
                    DependencyGroup = null,
                },
            },
        ];
    }
}

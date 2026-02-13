using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly FinalizeRepoCommandHandler _handler;

    private const string SOURCE_ORG = "source-org";
    private const string SOURCE_REPO = "source-repo";
    private const string TARGET_ORG = "target-org";
    private const string TARGET_REPO = "target-repo";
    private const string GHES_API_URL = "https://ghes.contoso.com/api/v3";

    public FinalizeRepoCommandHandlerTests()
    {
        _handler = new FinalizeRepoCommandHandler(_mockOctoLogger.Object, _mockSourceGithubApi.Object, _mockTargetGithubApi.Object);
    }

    private FinalizeRepoCommandArgs CreateDefaultArgs() => new()
    {
        GithubSourceOrg = SOURCE_ORG,
        SourceRepo = SOURCE_REPO,
        GithubTargetOrg = TARGET_ORG,
        TargetRepo = TARGET_REPO,
        GhesApiUrl = GHES_API_URL,
    };

    private void SetupReposExist()
    {
        _mockSourceGithubApi.Setup(x => x.DoesRepoExist(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(true);
        _mockTargetGithubApi.Setup(x => x.DoesRepoExist(TARGET_ORG, TARGET_REPO)).ReturnsAsync(true);
    }

    private void SetupNoArtifacts()
    {
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync([]);
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
    }

    [Fact]
    public async Task Throws_When_Source_Repo_Does_Not_Exist()
    {
        _mockSourceGithubApi.Setup(x => x.DoesRepoExist(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(false);

        var args = CreateDefaultArgs();

        await FluentActions.Invoking(() => _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"*{SOURCE_ORG}/{SOURCE_REPO}*does not exist*");
    }

    [Fact]
    public async Task Throws_When_Target_Repo_Does_Not_Exist()
    {
        _mockSourceGithubApi.Setup(x => x.DoesRepoExist(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(true);
        _mockTargetGithubApi.Setup(x => x.DoesRepoExist(TARGET_ORG, TARGET_REPO)).ReturnsAsync(false);

        var args = CreateDefaultArgs();

        await FluentActions.Invoking(() => _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"*{TARGET_ORG}/{TARGET_REPO}*does not exist*");
    }

    [Fact]
    public async Task Archives_Source_Repo_When_Flag_Set()
    {
        SetupReposExist();
        SetupNoArtifacts();
        _mockSourceGithubApi.Setup(x => x.IsRepoArchived(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(false);

        var args = CreateDefaultArgs();
        args.ArchiveSourceRepo = true;

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.ArchiveRepo(SOURCE_ORG, SOURCE_REPO), Times.Once);
    }

    [Fact]
    public async Task Skips_Archive_When_Already_Archived()
    {
        SetupReposExist();
        SetupNoArtifacts();
        _mockSourceGithubApi.Setup(x => x.IsRepoArchived(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(true);

        var args = CreateDefaultArgs();
        args.ArchiveSourceRepo = true;

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.ArchiveRepo(SOURCE_ORG, SOURCE_REPO), Times.Never);
    }

    [Fact]
    public async Task Does_Not_Archive_When_Flag_Not_Set()
    {
        SetupReposExist();
        SetupNoArtifacts();

        var args = CreateDefaultArgs();
        args.ArchiveSourceRepo = false;

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.IsRepoArchived(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockSourceGithubApi.Verify(x => x.ArchiveRepo(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DryRun_Does_Not_Archive()
    {
        SetupReposExist();
        SetupNoArtifacts();
        _mockSourceGithubApi.Setup(x => x.IsRepoArchived(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(false);

        var args = CreateDefaultArgs();
        args.ArchiveSourceRepo = true;
        args.DryRun = true;

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.ArchiveRepo(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Migrates_Repository_Settings()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync([]);
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings
            {
                Description = "test desc",
                Homepage = "https://example.com",
                HasIssues = true,
                HasProjects = false,
                HasWiki = true,
                AllowSquashMerge = true,
                AllowMergeCommit = false,
                AllowRebaseMerge = true,
                DeleteBranchOnMerge = true,
            });

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.UpdateRepositorySettings(TARGET_ORG, TARGET_REPO, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task Skips_Settings_When_Excluded()
    {
        SetupReposExist();
        SetupNoArtifacts();

        var args = CreateDefaultArgs();
        args.SkipArtifacts = "settings";

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.GetRepositorySettings(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockTargetGithubApi.Verify(x => x.UpdateRepositorySettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Migrates_Autolinks()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(
            [
                (1, "JIRA-", "https://jira.example.com/browse/JIRA-<num>"),
            ]);
        _mockTargetGithubApi.Setup(x => x.GetAutoLinks(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync([]);

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.AddAutoLink(TARGET_ORG, TARGET_REPO, "JIRA-", "https://jira.example.com/browse/JIRA-<num>"), Times.Once);
    }

    [Fact]
    public async Task Skips_Duplicate_Autolinks()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(
            [
                (1, "JIRA-", "https://jira.example.com/browse/JIRA-<num>"),
            ]);
        _mockTargetGithubApi.Setup(x => x.GetAutoLinks(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(
            [
                (2, "JIRA-", "https://jira.example.com/browse/JIRA-<num>"),
            ]);

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.AddAutoLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Migrates_Topics()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync([]);
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { "csharp", "dotnet", "migration" });

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.SetRepositoryTopics(TARGET_ORG, TARGET_REPO, It.Is<IEnumerable<string>>(t => t.Count() == 3)), Times.Once);
    }

    [Fact]
    public async Task Migrates_Branch_Protection_Rules()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync([]);
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { "main", "develop" });
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { "main", "develop" });

        var protection = JObject.Parse(@"{
            ""required_status_checks"": { ""strict"": true, ""contexts"": [""ci/build""] },
            ""enforce_admins"": { ""enabled"": true },
            ""required_pull_request_reviews"": { ""dismiss_stale_reviews"": true, ""require_code_owner_reviews"": false, ""required_approving_review_count"": 2 },
            ""restrictions"": null,
            ""required_linear_history"": { ""enabled"": false },
            ""allow_force_pushes"": { ""enabled"": false },
            ""allow_deletions"": { ""enabled"": false }
        }");

        _mockSourceGithubApi.Setup(x => x.GetBranchProtection(SOURCE_ORG, SOURCE_REPO, "main"))
            .ReturnsAsync(protection);
        _mockSourceGithubApi.Setup(x => x.GetBranchProtection(SOURCE_ORG, SOURCE_REPO, "develop"))
            .ReturnsAsync((JObject)null);

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.SetBranchProtection(TARGET_ORG, TARGET_REPO, "main", It.IsAny<object>()), Times.Once);
        _mockTargetGithubApi.Verify(x => x.SetBranchProtection(TARGET_ORG, TARGET_REPO, "develop", It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Skips_Branch_Protection_For_Missing_Target_Branch()
    {
        SetupReposExist();
        _mockSourceGithubApi.Setup(x => x.GetRepositorySettings(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new global::Octoshift.Models.RepositorySettings());
        _mockSourceGithubApi.Setup(x => x.GetAutoLinks(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync([]);
        _mockSourceGithubApi.Setup(x => x.GetRepositoryTopics(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Enumerable.Empty<string>());
        _mockSourceGithubApi.Setup(x => x.GetBranches(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { "feature-branch" });
        _mockTargetGithubApi.Setup(x => x.GetBranches(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { "main" }); // feature-branch doesn't exist on target

        var protection = JObject.Parse(@"{ ""enforce_admins"": { ""enabled"": false } }");
        _mockSourceGithubApi.Setup(x => x.GetBranchProtection(SOURCE_ORG, SOURCE_REPO, "feature-branch"))
            .ReturnsAsync(protection);

        var args = CreateDefaultArgs();
        await _handler.Handle(args);

        _mockTargetGithubApi.Verify(x => x.SetBranchProtection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Skips_All_Artifacts_When_All_Excluded()
    {
        SetupReposExist();

        var args = CreateDefaultArgs();
        args.SkipArtifacts = "settings,autolinks,topics,branch-protection";

        await _handler.Handle(args);

        _mockSourceGithubApi.Verify(x => x.GetRepositorySettings(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockSourceGithubApi.Verify(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockSourceGithubApi.Verify(x => x.GetRepositoryTopics(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockSourceGithubApi.Verify(x => x.GetBranches(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildBranchProtectionPayload_Maps_All_Fields()
    {
        var protection = JObject.Parse(@"{
            ""required_status_checks"": { ""strict"": true, ""contexts"": [""ci/build"", ""ci/test""] },
            ""enforce_admins"": { ""enabled"": true },
            ""required_pull_request_reviews"": { ""dismiss_stale_reviews"": true, ""require_code_owner_reviews"": true, ""required_approving_review_count"": 3 },
            ""restrictions"": { ""users"": [{ ""login"": ""user1"" }], ""teams"": [{ ""slug"": ""team1"" }], ""apps"": [{ ""slug"": ""app1"" }] },
            ""required_linear_history"": { ""enabled"": true },
            ""allow_force_pushes"": { ""enabled"": false },
            ""allow_deletions"": { ""enabled"": true }
        }");

        var result = FinalizeRepoCommandHandler.BuildBranchProtectionPayload(protection) as Dictionary<string, object>;

        result.Should().NotBeNull();

        var statusChecks = result["required_status_checks"] as Dictionary<string, object>;
        statusChecks["strict"].Should().Be(true);
        ((string[])statusChecks["contexts"]).Should().BeEquivalentTo("ci/build", "ci/test");

        result["enforce_admins"].Should().Be(true);

        var reviews = result["required_pull_request_reviews"] as Dictionary<string, object>;
        reviews["dismiss_stale_reviews"].Should().Be(true);
        reviews["require_code_owner_reviews"].Should().Be(true);
        reviews["required_approving_review_count"].Should().Be(3);

        var restrictions = result["restrictions"] as Dictionary<string, object>;
        ((string[])restrictions["users"]).Should().BeEquivalentTo("user1");
        ((string[])restrictions["teams"]).Should().BeEquivalentTo("team1");
        ((string[])restrictions["apps"]).Should().BeEquivalentTo("app1");

        result["required_linear_history"].Should().Be(true);
        result["allow_force_pushes"].Should().Be(false);
        result["allow_deletions"].Should().Be(true);
    }

    [Fact]
    public void BuildBranchProtectionPayload_Handles_Null_Fields()
    {
        var protection = JObject.Parse(@"{
            ""required_status_checks"": null,
            ""enforce_admins"": null,
            ""required_pull_request_reviews"": null,
            ""restrictions"": null,
            ""required_linear_history"": null,
            ""allow_force_pushes"": null,
            ""allow_deletions"": null
        }");

        var result = FinalizeRepoCommandHandler.BuildBranchProtectionPayload(protection) as Dictionary<string, object>;

        result.Should().NotBeNull();
        result["required_status_checks"].Should().BeNull();
        result["enforce_admins"].Should().Be(false);
        result["required_pull_request_reviews"].Should().BeNull();
        result["restrictions"].Should().BeNull();
        result["required_linear_history"].Should().Be(false);
        result["allow_force_pushes"].Should().Be(false);
        result["allow_deletions"].Should().Be(false);
    }
}

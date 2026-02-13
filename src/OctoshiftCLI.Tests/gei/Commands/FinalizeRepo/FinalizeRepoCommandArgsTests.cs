using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string SOURCE_ORG = "foo-source-org";
    private const string SOURCE_REPO = "blah";
    private const string TARGET_ORG = "foo-target-org";
    private const string GHES_API_URL = "https://ghes.contoso.com/api/v3";

    [Fact]
    public void Target_Repo_Defaults_To_Source_Repo()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = TARGET_ORG,
            GhesApiUrl = GHES_API_URL,
        };

        args.Validate(_mockOctoLogger.Object);

        args.TargetRepo.Should().Be(SOURCE_REPO);
    }

    [Fact]
    public void Validate_Throws_When_GithubSourceOrg_Is_Url()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = "https://github.com/my-org",
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = TARGET_ORG,
            GhesApiUrl = GHES_API_URL,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>();
    }

    [Fact]
    public void Validate_Throws_When_GithubTargetOrg_Is_Url()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = "http://github.com/my-org",
            GhesApiUrl = GHES_API_URL,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>();
    }

    [Fact]
    public void Validate_Throws_When_SourceRepo_Is_Url()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = "http://github.com/org/repo",
            GithubTargetOrg = TARGET_ORG,
            GhesApiUrl = GHES_API_URL,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>();
    }

    [Fact]
    public void Validate_Throws_When_TargetRepo_Is_Url()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = TARGET_ORG,
            TargetRepo = "http://github.com/org/repo",
            GhesApiUrl = GHES_API_URL,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>();
    }

    [Fact]
    public void Validate_Throws_When_GhesApiUrl_Is_Missing()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = TARGET_ORG,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--ghes-api-url*required*");
    }

    [Fact]
    public void GithubSourcePat_Falls_Back_To_GithubTargetPat()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            GithubTargetOrg = TARGET_ORG,
            GhesApiUrl = GHES_API_URL,
            GithubTargetPat = "target-pat",
        };

        args.Validate(_mockOctoLogger.Object);

        args.GithubSourcePat.Should().Be("target-pat");
    }
}

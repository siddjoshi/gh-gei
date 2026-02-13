using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommandTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ISourceGithubApiFactory> _mockSourceGithubApiFactory = new();
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();

    private readonly ServiceProvider _serviceProvider;
    private readonly FinalizeRepoCommand _command = [];

    public FinalizeRepoCommandTests()
    {
        var mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
        var mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();

        _mockSourceGithubApiFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockSourceGithubApi.Object);
        _mockSourceGithubApiFactory
            .Setup(x => x.CreateClientNoSsl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockSourceGithubApi.Object);
        _mockTargetGithubApiFactory
            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockTargetGithubApi.Object);

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockSourceGithubApiFactory.Object)
            .AddSingleton(_mockTargetGithubApiFactory.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("finalize-repo");
        _command.Options.Count.Should().Be(13);

        TestHelpers.VerifyCommandOption(_command.Options, "github-source-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "source-repo", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-repo", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ghes-api-url", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-source-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "archive-source-repo", false);
        TestHelpers.VerifyCommandOption(_command.Options, "skip-artifacts", false);
        TestHelpers.VerifyCommandOption(_command.Options, "dry-run", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }

    [Fact]
    public void BuildHandler_Returns_Handler()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = "source-org",
            SourceRepo = "source-repo",
            GithubTargetOrg = "target-org",
            TargetRepo = "target-repo",
            GhesApiUrl = "https://ghes.contoso.com/api/v3",
        };

        var handler = _command.BuildHandler(args, _serviceProvider);

        handler.Should().NotBeNull();
    }

    [Fact]
    public void BuildHandler_Uses_NoSsl_Factory_When_NoSslVerify_Set()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = "source-org",
            SourceRepo = "source-repo",
            GithubTargetOrg = "target-org",
            TargetRepo = "target-repo",
            GhesApiUrl = "https://ghes.contoso.com/api/v3",
            NoSslVerify = true,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockSourceGithubApiFactory.Verify(x => x.CreateClientNoSsl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockSourceGithubApiFactory.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildHandler_Uses_Regular_Factory_When_NoSslVerify_Not_Set()
    {
        var args = new FinalizeRepoCommandArgs
        {
            GithubSourceOrg = "source-org",
            SourceRepo = "source-repo",
            GithubTargetOrg = "target-org",
            TargetRepo = "target-repo",
            GhesApiUrl = "https://ghes.contoso.com/api/v3",
            NoSslVerify = false,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockSourceGithubApiFactory.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockSourceGithubApiFactory.Verify(x => x.CreateClientNoSsl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

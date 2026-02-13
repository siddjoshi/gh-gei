using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommand : CommandBase<FinalizeRepoCommandArgs, FinalizeRepoCommandHandler>
{
    public FinalizeRepoCommand() : base(
        name: "finalize-repo",
        description: "Finalizes a completed migration by optionally archiving the source repository and migrating repository artifacts (settings, autolinks, topics, branch protection rules) that are not included in the default migration.")
    {
        AddOption(GithubSourceOrg);
        AddOption(SourceRepo);
        AddOption(GithubTargetOrg);
        AddOption(TargetRepo);
        AddOption(GhesApiUrl);
        AddOption(TargetApiUrl);
        AddOption(NoSslVerify);
        AddOption(GithubSourcePat);
        AddOption(GithubTargetPat);
        AddOption(ArchiveSourceRepo);
        AddOption(SkipArtifacts);
        AddOption(DryRun);
        AddOption(Verbose);
    }

    public Option<string> GithubSourceOrg { get; } = new("--github-source-org")
    {
        Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set.",
        IsRequired = true,
    };
    public Option<string> SourceRepo { get; } = new("--source-repo")
    {
        IsRequired = true,
    };
    public Option<string> GithubTargetOrg { get; } = new("--github-target-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable or --github-target-pat option."
    };
    public Option<string> TargetRepo { get; } = new("--target-repo")
    {
        Description = "Defaults to the name of source-repo"
    };
    public Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
    {
        Description = "Required. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3",
        IsRequired = true,
    };
    public Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };
    public Option<bool> NoSslVerify { get; } = new("--no-ssl-verify")
    {
        Description = "Disables SSL verification when communicating with your GHES instance. All other operations will continue to verify SSL."
    };
    public Option<string> GithubSourcePat { get; } = new("--github-source-pat");
    public Option<string> GithubTargetPat { get; } = new("--github-target-pat");
    public Option<bool> ArchiveSourceRepo { get; } = new("--archive-source-repo")
    {
        Description = "Archive the source repository on GHES after finalization. This sets the repository to archived (read-only), blocking all writes including pushes, issues, and pull requests. This is reversible."
    };
    public Option<string> SkipArtifacts { get; } = new("--skip-artifacts")
    {
        Description = "Comma-separated list of artifact types to skip during finalization. Valid values: settings, autolinks, topics, branch-protection"
    };
    public Option<bool> DryRun { get; } = new("--dry-run")
    {
        Description = "Show what would be done without making any changes."
    };
    public Option<bool> Verbose { get; } = new("--verbose");

    public override FinalizeRepoCommandHandler BuildHandler(FinalizeRepoCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();

        var sourceGithubApiFactory = sp.GetRequiredService<ISourceGithubApiFactory>();
        var targetGithubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();

        var sourceGithubApi = args.NoSslVerify
            ? sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, null, args.GithubSourcePat)
            : sourceGithubApiFactory.Create(args.GhesApiUrl, null, args.GithubSourcePat);

        var targetGithubApi = targetGithubApiFactory.Create(args.TargetApiUrl, null, args.GithubTargetPat);

        return new FinalizeRepoCommandHandler(log, sourceGithubApi, targetGithubApi);
    }
}

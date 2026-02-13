using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommandArgs : CommandArgs
{
    public string GithubSourceOrg { get; set; }
    public string SourceRepo { get; set; }
    public string GithubTargetOrg { get; set; }
    public string TargetRepo { get; set; }
    public string GhesApiUrl { get; set; }
    public string TargetApiUrl { get; set; }
    public bool NoSslVerify { get; set; }
    [Secret]
    public string GithubSourcePat { get; set; }
    [Secret]
    public string GithubTargetPat { get; set; }
    public bool ArchiveSourceRepo { get; set; }
    public string SkipArtifacts { get; set; }
    public bool DryRun { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GithubSourceOrg.IsUrl())
        {
            throw new OctoshiftCliException("The --github-source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (GithubTargetOrg.IsUrl())
        {
            throw new OctoshiftCliException("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
        }

        if (SourceRepo.IsUrl())
        {
            throw new OctoshiftCliException("The --source-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
        }

        if (TargetRepo.IsUrl())
        {
            throw new OctoshiftCliException("The --target-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
        }

        if (GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url is required. The finalize-repo command currently only supports GHES as a source.");
        }

        if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--ghes-api-url must be specified when --no-ssl-verify is specified.");
        }

        if (GithubTargetPat.HasValue() && GithubSourcePat.IsNullOrWhiteSpace())
        {
            GithubSourcePat = GithubTargetPat;
            log?.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
        }

        if (TargetRepo.IsNullOrWhiteSpace())
        {
            log?.LogInformation($"Target repo name not provided, defaulting to same as source repo ({SourceRepo})");
            TargetRepo = SourceRepo;
        }
    }
}

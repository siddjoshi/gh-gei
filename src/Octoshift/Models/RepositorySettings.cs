namespace Octoshift.Models;

public class RepositorySettings
{
    public string Description { get; set; }
    public string Homepage { get; set; }
    public string Visibility { get; set; }
    public bool HasIssues { get; set; }
    public bool HasProjects { get; set; }
    public bool HasWiki { get; set; }
    public string DefaultBranch { get; set; }
    public bool AllowSquashMerge { get; set; }
    public bool AllowMergeCommit { get; set; }
    public bool AllowRebaseMerge { get; set; }
    public bool DeleteBranchOnMerge { get; set; }
    public bool IsArchived { get; set; }
}

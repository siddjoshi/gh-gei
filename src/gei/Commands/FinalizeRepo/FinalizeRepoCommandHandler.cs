using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.FinalizeRepo;

public class FinalizeRepoCommandHandler : ICommandHandler<FinalizeRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _sourceGithubApi;
    private readonly GithubApi _targetGithubApi;

    public FinalizeRepoCommandHandler(OctoLogger log, GithubApi sourceGithubApi, GithubApi targetGithubApi)
    {
        _log = log;
        _sourceGithubApi = sourceGithubApi;
        _targetGithubApi = targetGithubApi;
    }

    public async Task Handle(FinalizeRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        var skippedArtifacts = ParseSkipArtifacts(args.SkipArtifacts);

        _log.LogInformation($"Finalizing migration for repository '{args.GithubSourceOrg}/{args.SourceRepo}' -> '{args.GithubTargetOrg}/{args.TargetRepo}'...");

        if (args.DryRun)
        {
            _log.LogInformation("DRY RUN: No changes will be made.");
        }

        await ValidatePreconditions(args);

        var successCount = 0;
        var failureCount = 0;

        if (args.ArchiveSourceRepo)
        {
            TrackResult(await ExecuteStep("Archive source repository", () => ArchiveSourceRepository(args)), ref successCount, ref failureCount);
        }

        if (!skippedArtifacts.Contains("settings"))
        {
            TrackResult(await ExecuteStep("Migrate repository settings", () => MigrateRepositorySettings(args)), ref successCount, ref failureCount);
        }
        else
        {
            _log.LogInformation("Skipping repository settings (excluded via --skip-artifacts).");
        }

        if (!skippedArtifacts.Contains("autolinks"))
        {
            TrackResult(await ExecuteStep("Migrate autolinks", () => MigrateAutolinks(args)), ref successCount, ref failureCount);
        }
        else
        {
            _log.LogInformation("Skipping autolinks (excluded via --skip-artifacts).");
        }

        if (!skippedArtifacts.Contains("topics"))
        {
            TrackResult(await ExecuteStep("Migrate topics", () => MigrateTopics(args)), ref successCount, ref failureCount);
        }
        else
        {
            _log.LogInformation("Skipping topics (excluded via --skip-artifacts).");
        }

        if (!skippedArtifacts.Contains("branch-protection"))
        {
            TrackResult(await ExecuteStep("Migrate branch protection rules", () => MigrateBranchProtectionRules(args)), ref successCount, ref failureCount);
        }
        else
        {
            _log.LogInformation("Skipping branch protection rules (excluded via --skip-artifacts).");
        }

        LogSummary(successCount, failureCount, args.DryRun);
    }

    private async Task ValidatePreconditions(FinalizeRepoCommandArgs args)
    {
        _log.LogInformation("Validating source repository exists...");
        if (!await _sourceGithubApi.DoesRepoExist(args.GithubSourceOrg, args.SourceRepo))
        {
            throw new OctoshiftCliException($"Source repository '{args.GithubSourceOrg}/{args.SourceRepo}' does not exist or is not accessible.");
        }

        _log.LogInformation("Validating target repository exists...");
        if (!await _targetGithubApi.DoesRepoExist(args.GithubTargetOrg, args.TargetRepo))
        {
            throw new OctoshiftCliException($"Target repository '{args.GithubTargetOrg}/{args.TargetRepo}' does not exist. Ensure the migration has completed before running finalize-repo.");
        }
    }

    private async Task ArchiveSourceRepository(FinalizeRepoCommandArgs args)
    {
        var alreadyArchived = await _sourceGithubApi.IsRepoArchived(args.GithubSourceOrg, args.SourceRepo);
        if (alreadyArchived)
        {
            _log.LogInformation("Source repository is already archived. Skipping.");
            return;
        }

        if (args.DryRun)
        {
            _log.LogInformation($"DRY RUN: Would archive source repository '{args.GithubSourceOrg}/{args.SourceRepo}'.");
            return;
        }

        await _sourceGithubApi.ArchiveRepo(args.GithubSourceOrg, args.SourceRepo);
        _log.LogSuccess($"Source repository '{args.GithubSourceOrg}/{args.SourceRepo}' has been archived (read-only).");
    }

    private async Task MigrateRepositorySettings(FinalizeRepoCommandArgs args)
    {
        var sourceSettings = await _sourceGithubApi.GetRepositorySettings(args.GithubSourceOrg, args.SourceRepo);

        if (args.DryRun)
        {
            _log.LogInformation("DRY RUN: Would migrate repository settings (description, homepage, merge options, feature toggles).");
            return;
        }

        await _targetGithubApi.UpdateRepositorySettings(args.GithubTargetOrg, args.TargetRepo, new
        {
            description = sourceSettings.Description,
            homepage = sourceSettings.Homepage,
            has_issues = sourceSettings.HasIssues,
            has_projects = sourceSettings.HasProjects,
            has_wiki = sourceSettings.HasWiki,
            allow_squash_merge = sourceSettings.AllowSquashMerge,
            allow_merge_commit = sourceSettings.AllowMergeCommit,
            allow_rebase_merge = sourceSettings.AllowRebaseMerge,
            delete_branch_on_merge = sourceSettings.DeleteBranchOnMerge,
        });
        _log.LogInformation("Repository settings migrated.");
    }

    private async Task MigrateAutolinks(FinalizeRepoCommandArgs args)
    {
        var sourceAutolinks = await _sourceGithubApi.GetAutoLinks(args.GithubSourceOrg, args.SourceRepo);
        if (!sourceAutolinks.Any())
        {
            _log.LogInformation("No autolinks found on source repository.");
            return;
        }

        var targetAutolinks = await _targetGithubApi.GetAutoLinks(args.GithubTargetOrg, args.TargetRepo);
        var targetKeyPrefixes = targetAutolinks.Select(a => a.KeyPrefix).ToHashSet();

        var added = 0;
        var skipped = 0;
        foreach (var (_, keyPrefix, urlTemplate) in sourceAutolinks)
        {
            if (targetKeyPrefixes.Contains(keyPrefix))
            {
                _log.LogVerbose($"Autolink with key prefix '{keyPrefix}' already exists on target. Skipping.");
                skipped++;
                continue;
            }

            if (args.DryRun)
            {
                _log.LogInformation($"DRY RUN: Would add autolink '{keyPrefix}' -> '{urlTemplate}'.");
                added++;
                continue;
            }

            try
            {
                await _targetGithubApi.AddAutoLink(args.GithubTargetOrg, args.TargetRepo, keyPrefix, urlTemplate);
                added++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                _log.LogWarning($"Autolink with key prefix '{keyPrefix}' could not be added (may already exist). Skipping.");
                skipped++;
            }
        }

        _log.LogInformation($"Autolinks: {added} added, {skipped} skipped.");
    }

    private async Task MigrateTopics(FinalizeRepoCommandArgs args)
    {
        var sourceTopics = (await _sourceGithubApi.GetRepositoryTopics(args.GithubSourceOrg, args.SourceRepo)).ToList();
        if (!sourceTopics.Any())
        {
            _log.LogInformation("No topics found on source repository.");
            return;
        }

        if (args.DryRun)
        {
            _log.LogInformation($"DRY RUN: Would set topics: {string.Join(", ", sourceTopics)}");
            return;
        }

        await _targetGithubApi.SetRepositoryTopics(args.GithubTargetOrg, args.TargetRepo, sourceTopics);
        _log.LogInformation($"Migrated {sourceTopics.Count} topics.");
    }

    private async Task MigrateBranchProtectionRules(FinalizeRepoCommandArgs args)
    {
        var sourceBranches = (await _sourceGithubApi.GetBranches(args.GithubSourceOrg, args.SourceRepo)).ToList();
        var targetBranches = (await _targetGithubApi.GetBranches(args.GithubTargetOrg, args.TargetRepo)).ToHashSet();

        var migrated = 0;
        var skipped = 0;
        var failed = 0;
        foreach (var branch in sourceBranches)
        {
            var protection = await _sourceGithubApi.GetBranchProtection(args.GithubSourceOrg, args.SourceRepo, branch);
            if (protection is null)
            {
                continue; // Branch is not protected
            }

            if (!targetBranches.Contains(branch))
            {
                _log.LogVerbose($"Branch '{branch}' does not exist on target. Skipping branch protection.");
                skipped++;
                continue;
            }

            if (args.DryRun)
            {
                _log.LogInformation($"DRY RUN: Would apply branch protection rules to '{branch}'.");
                migrated++;
                continue;
            }

            try
            {
                var protectionPayload = BuildBranchProtectionPayload(protection);
                await _targetGithubApi.SetBranchProtection(args.GithubTargetOrg, args.TargetRepo, branch, protectionPayload);
                migrated++;
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning($"Failed to set branch protection for '{branch}': {ex.Message}");
                failed++;
            }
            catch (OctoshiftCliException ex)
            {
                _log.LogWarning($"Failed to set branch protection for '{branch}': {ex.Message}");
                failed++;
            }
        }

        _log.LogInformation($"Branch protection rules: {migrated} migrated, {skipped} skipped, {failed} failed.");
    }

    private void LogSummary(int successCount, int failureCount, bool dryRun)
    {
        _log.LogInformation("");
        _log.LogInformation("== Finalization Summary ==");
        _log.LogInformation($"Steps succeeded: {successCount}");
        if (failureCount > 0)
        {
            _log.LogWarning($"Steps failed: {failureCount}");
        }

        if (dryRun)
        {
            _log.LogInformation("DRY RUN completed. No changes were made.");
        }
        else if (failureCount == 0)
        {
            _log.LogSuccess("Repository finalization completed successfully.");
        }
        else
        {
            _log.LogWarning("Repository finalization completed with errors. Review the log output above and retry if needed. The command is safe to re-run (idempotent).");
        }
    }

    private static HashSet<string> ParseSkipArtifacts(string skipArtifacts)
    {
        return skipArtifacts.IsNullOrWhiteSpace()
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : skipArtifacts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> ExecuteStep(string stepName, Func<Task> action)
    {
        _log.LogInformation($"[{stepName}]");
        try
        {
            await action();
            return true;
        }
        catch (HttpRequestException ex)
        {
            _log.LogError($"[{stepName}] failed: {ex.Message}");
            _log.LogVerbose(ex.ToString());
            return false;
        }
        catch (OctoshiftCliException ex)
        {
            _log.LogError($"[{stepName}] failed: {ex.Message}");
            _log.LogVerbose(ex.ToString());
            return false;
        }
    }

    private static void TrackResult(bool success, ref int successCount, ref int failureCount)
    {
        if (success)
        {
            successCount++;
        }
        else
        {
            failureCount++;
        }
    }

    internal static object BuildBranchProtectionPayload(JObject protection)
    {
        var payload = new Dictionary<string, object>();

        AddRequiredStatusChecks(protection, payload);
        AddEnforceAdmins(protection, payload);
        AddRequiredPullRequestReviews(protection, payload);
        AddRestrictions(protection, payload);
        AddBooleanProtectionSetting(protection, payload, "required_linear_history");
        AddBooleanProtectionSetting(protection, payload, "allow_force_pushes");
        AddBooleanProtectionSetting(protection, payload, "allow_deletions");

        return payload;
    }

    private static void AddRequiredStatusChecks(JObject protection, Dictionary<string, object> payload)
    {
        var requiredStatusChecks = protection["required_status_checks"];
        payload["required_status_checks"] = IsNotNullToken(requiredStatusChecks)
            ? new Dictionary<string, object>
            {
                ["strict"] = (bool)(requiredStatusChecks["strict"] ?? false),
                ["contexts"] = requiredStatusChecks["contexts"]?.ToObject<string[]>() ?? Array.Empty<string>(),
            }
            : (object)null;
    }

    private static void AddEnforceAdmins(JObject protection, Dictionary<string, object> payload)
    {
        var enforceAdmins = protection["enforce_admins"];
        payload["enforce_admins"] = IsNotNullToken(enforceAdmins) && (bool)(enforceAdmins["enabled"] ?? false);
    }

    private static void AddRequiredPullRequestReviews(JObject protection, Dictionary<string, object> payload)
    {
        var reviews = protection["required_pull_request_reviews"];
        payload["required_pull_request_reviews"] = IsNotNullToken(reviews)
            ? new Dictionary<string, object>
            {
                ["dismiss_stale_reviews"] = (bool)(reviews["dismiss_stale_reviews"] ?? false),
                ["require_code_owner_reviews"] = (bool)(reviews["require_code_owner_reviews"] ?? false),
                ["required_approving_review_count"] = (int)(reviews["required_approving_review_count"] ?? 1),
            }
            : (object)null;
    }

    private static void AddRestrictions(JObject protection, Dictionary<string, object> payload)
    {
        var restrictions = protection["restrictions"];
        payload["restrictions"] = IsNotNullToken(restrictions)
            ? new Dictionary<string, object>
            {
                ["users"] = restrictions["users"]?.Select(u => (string)u["login"]).ToArray() ?? Array.Empty<string>(),
                ["teams"] = restrictions["teams"]?.Select(t => (string)t["slug"]).ToArray() ?? Array.Empty<string>(),
                ["apps"] = restrictions["apps"]?.Select(a => (string)a["slug"]).ToArray() ?? Array.Empty<string>(),
            }
            : (object)null;
    }

    private static void AddBooleanProtectionSetting(JObject protection, Dictionary<string, object> payload, string settingName)
    {
        var setting = protection[settingName];
        payload[settingName] = IsNotNullToken(setting) && (bool)(setting["enabled"] ?? false);
    }

    private static bool IsNotNullToken(JToken token) => token is not null && token.Type != JTokenType.Null;
}

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public sealed class UpdateService
    {
        private readonly GitHubReleaseService _githubService = new GitHubReleaseService();
        private readonly DownloadService _downloadService = new DownloadService();
        private readonly ProductionLogger _logger = ProductionLogger.Instance;

        public sealed class UpdateInfo
        {
            public Version CurrentVersion { get; init; } = new Version(0, 0, 0, 0);
            public Version LatestVersion { get; init; } = new Version(0, 0, 0, 0);
            public string DownloadUrl { get; init; } = string.Empty;
            public string AssetFileName { get; init; } = string.Empty;
            public UpdateCheckResultDto ReleaseDto { get; set; } = null!;
            public string? ReleaseNotes { get; init; }
            public string? ExpectedSha256Hash { get; init; }
            public bool IsUpdateAvailable => LatestVersion.CompareTo(CurrentVersion) > 0;
        }

        public sealed class UpdateCheckResult
        {
            public UpdateInfo? Info { get; init; }
            public string? ErrorMessage { get; init; }
            public bool IsNetworkError { get; init; }
            public bool IsSuccess => Info != null && ErrorMessage == null;

            public static UpdateCheckResult Success(UpdateInfo info) => new() { Info = info };
            public static UpdateCheckResult Error(string message) => new() { ErrorMessage = message };
            public static UpdateCheckResult NetworkError(string message) => new() { ErrorMessage = message, IsNetworkError = true };
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var latest = await _githubService.GetLatestReleaseAsync(cancellationToken);
                if (latest == null) return UpdateCheckResult.Error("No releases found on GitHub.");

                var asset = _githubService.GetMatchingAsset(latest);
                if (asset == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                    return UpdateCheckResult.Error("No suitable asset found in the latest release.");

                var latestVersion = ParseVersion(latest.TagName);
                if (latestVersion == null) return UpdateCheckResult.Error("Invalid version tag on release.");

                return UpdateCheckResult.Success(new UpdateInfo
                {
                    CurrentVersion = GetCurrentVersion(),
                    LatestVersion = latestVersion,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    AssetFileName = asset.Name ?? "update.zip",
                    ReleaseNotes = latest.Body,
                    ExpectedSha256Hash = _githubService.ExtractSha256(latest.Body)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update check failed: {ex.Message}", "UpdateService");
                return UpdateCheckResult.NetworkError(ex.Message);
            }
        }

        public async Task<DownloadResult> DownloadUpdateAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string updateDir = PathHelper.GetUpdateDirectory();
                string targetPath = Path.Combine(updateDir, info.AssetFileName);

                await _downloadService.DownloadFileAsync(info.DownloadUrl, targetPath, progress, cancellationToken);

                if (!string.IsNullOrEmpty(info.ExpectedSha256Hash))
                {
                    bool valid = await _downloadService.VerifySha256HashAsync(targetPath, info.ExpectedSha256Hash);
                    if (!valid) return DownloadResult.Error("Hash verification failed.");
                }

                return DownloadResult.Success(targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Update download failed: {ex.Message}", "UpdateService");
                return DownloadResult.Error(ex.Message);
            }
        }

        public async Task<List<UpdateCheckResult>> GetAllReleasesAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<UpdateCheckResult>();
            var releases = await _githubService.GetAllReleasesAsync(cancellationToken);
            foreach (var release in releases)
            {
                var info = ConvertToUpdateInfo(release);
                if (info != null) results.Add(UpdateCheckResult.Success(info));
            }
            return results;
        }

        public UpdateInfo? ConvertToUpdateInfo(UpdateCheckResultDto release)
        {
            var asset = _githubService.GetMatchingAsset(release);
            var version = ParseVersion(release.TagName);
            if (asset == null || version == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl)) return null;

            return new UpdateInfo
            {
                CurrentVersion = GetCurrentVersion(),
                LatestVersion = version,
                DownloadUrl = asset.BrowserDownloadUrl,
                AssetFileName = asset.Name ?? "update.zip",
                ReleaseNotes = release.Body,
                ExpectedSha256Hash = _githubService.ExtractSha256(release.Body)
            };
        }

        private static Version GetCurrentVersion()
        {
            var assembly = typeof(UpdateService).Assembly;
            return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        }

        private static Version? ParseVersion(string tag)
        {
            var trimmed = tag.Trim().TrimStart('v', 'V');
            return Version.TryParse(trimmed, out var v) ? v : null;
        }

        public sealed class DownloadResult
        {
            public string? FilePath { get; init; }
            public string? ErrorMessage { get; init; }
            public bool IsSuccess => FilePath != null && ErrorMessage == null;
            public static DownloadResult Success(string path) => new() { FilePath = path };
            public static DownloadResult Error(string msg) => new() { ErrorMessage = msg };
        }
    }
}

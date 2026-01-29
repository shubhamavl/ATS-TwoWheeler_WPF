using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public sealed class GitHubReleaseService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private const string RepositoryOwner = "shubhamavl";
        private const string RepositoryName = "ATS-TwoWheeler_WPF";
        private const string AssetNameSubstring = "ATS_TwoWheeler_WPF_Portable";
        private const string AssetExtension = ".zip";
        private static readonly string[] AllowedDomains = { "github.com", "githubusercontent.com" };

        private readonly ProductionLogger _logger = ProductionLogger.Instance;

        public async Task<UpdateCheckResultDto?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
            try
            {
                using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"GitHub API returned {(int)response.StatusCode}", "GitHubReleaseService");
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await JsonSerializer.DeserializeAsync<UpdateCheckResultDto>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching latest release: {ex.Message}", "GitHubReleaseService");
                throw;
            }
        }

        public async Task<List<UpdateCheckResultDto>> GetAllReleasesAsync(CancellationToken cancellationToken = default)
        {
            var allReleases = new List<UpdateCheckResultDto>();
            int page = 1;
            const int perPage = 30;

            try
            {
                while (true)
                {
                    var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?page={page}&per_page={perPage}";
                    using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    var releases = await JsonSerializer.DeserializeAsync<List<UpdateCheckResultDto>>(stream, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }, cancellationToken).ConfigureAwait(false);

                    if (releases == null || releases.Count == 0)
                    {
                        break;
                    }

                    foreach (var release in releases)
                    {
                        if (HasValidAsset(release))
                        {
                            allReleases.Add(release);
                        }
                    }

                    if (releases.Count < perPage)
                    {
                        break;
                    }

                    page++;
                }
                return allReleases;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching all releases: {ex.Message}", "GitHubReleaseService");
                return allReleases;
            }
        }

        public bool HasValidAsset(UpdateCheckResultDto release)
        {
            return release.Assets?.Exists(a =>
                !string.IsNullOrEmpty(a.BrowserDownloadUrl) &&
                !string.IsNullOrEmpty(a.Name) &&
                a.Name.Contains(AssetNameSubstring, StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase) &&
                IsValidDownloadUrl(a.BrowserDownloadUrl)) ?? false;
        }

        public AssetDto? GetMatchingAsset(UpdateCheckResultDto release)
        {
            return release.Assets?.Find(a =>
                !string.IsNullOrEmpty(a.BrowserDownloadUrl) &&
                !string.IsNullOrEmpty(a.Name) &&
                a.Name.Contains(AssetNameSubstring, StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase));
        }

        public string? ExtractSha256(string? releaseNotes)
        {
            if (string.IsNullOrWhiteSpace(releaseNotes))
            {
                return null;
            }

            var pattern = new Regex(@"SHA-256[:\s]+`?([a-fA-F0-9]{64})`?", RegexOptions.IgnoreCase);
            var match = pattern.Match(releaseNotes);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        private static bool IsValidDownloadUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                var uri = new Uri(url);
                var host = uri.Host.ToLowerInvariant();
                foreach (var domain in AllowedDomains)
                {
                    if (host == domain || host.EndsWith($".{domain}"))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ATS_TwoWheeler_WPF/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }
    }

    public sealed class UpdateCheckResultDto
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("published_at")] public string? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<AssetDto>? Assets { get; set; }
    }

    public sealed class AssetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ATS_TwoWheeler_WPF.Core;

namespace ATS_TwoWheeler_WPF.Services
{
    public sealed class DownloadService
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private readonly ProductionLogger _logger = ProductionLogger.Instance;

        public async Task<string> DownloadFileAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes.HasValue && progress != null)
                    {
                        progress.Report((double)totalRead / totalBytes.Value * 100.0);
                    }
                }

                return targetPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Download failed: {ex.Message}", "DownloadService");
                throw;
            }
        }

        public async Task<bool> VerifySha256HashAsync(string filePath, string expectedHash)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                using var sha256 = SHA256.Create();
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var hashBytes = await sha256.ComputeHashAsync(fileStream).ConfigureAwait(false);
                var computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

                return string.Equals(computedHash, expectedHash.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Hash verification error: {ex.Message}", "DownloadService");
                return false;
            }
        }
    }
}

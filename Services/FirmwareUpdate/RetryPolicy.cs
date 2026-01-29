using System;
using System.Threading;
using System.Threading.Tasks;

namespace ATS_TwoWheeler_WPF.Services.FirmwareUpdate
{
    /// <summary>
    /// Retry policy with exponential backoff for firmware operations
    /// </summary>
    public class RetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;

        public RetryPolicy(int maxRetries = 5, TimeSpan? initialDelay = null)
        {
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        }

        /// <summary>
        /// Execute an operation with retry and exponential backoff
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < _maxRetries - 1)
                {
                    lastException = ex;
                    attempt++;

                    // Exponential backoff: delay = initialDelay * 2^(attempt-1)
                    var delay = TimeSpan.FromMilliseconds(
                        _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                    await Task.Delay(delay, cancellationToken);
                }
            }

            // If we've exhausted all retries, throw the last exception
            throw lastException ?? new Exception("Operation failed after retries");
        }

        /// <summary>
        /// Execute an operation with retry (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            }, cancellationToken);
        }

        /// <summary>
        /// Execute an operation with retry and custom retry condition
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<Exception, bool> shouldRetry,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < _maxRetries - 1 && shouldRetry(ex))
                {
                    lastException = ex;
                    attempt++;

                    var delay = TimeSpan.FromMilliseconds(
                        _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw lastException ?? new Exception("Operation failed after retries");
        }
    }
}

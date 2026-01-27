using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static ATS_TwoWheeler_WPF.Services.UpdateService;

namespace ATS_TwoWheeler_WPF.Services.Interfaces
{
    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
        Task<DownloadResult> DownloadUpdateAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        Task<List<UpdateCheckResult>> GetAllReleasesAsync(CancellationToken cancellationToken = default);
        UpdateInfo? ConvertToUpdateInfo(UpdateCheckResultDto release);
    }
}

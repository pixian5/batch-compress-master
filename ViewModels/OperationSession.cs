using System;
using System.Threading;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Interfaces;
using BatchCompress.Avalonia.Core.Models;
using BatchCompress.Avalonia.Localization;

namespace BatchCompress.Avalonia.ViewModels;

/// <summary>
/// 单次批量操作（压缩或解压）的界面协调会话。
/// GLM-5.3, 2026-11-04：批次 2 阶段 3 引入。收敛 MainWindowViewModel 中
/// CompressAsync / DecompressAsync 重复的 50+ 行进度回调逻辑：
/// 统计属性回写、速度与剩余时间估算、进度通知节流、三分类日志写入，
/// 以及统一的生命周期封装（try/catch/finally、取消提示、关机确认触发、完成汇总）。
/// 差异点全部参数化：操作标签与完成词条以 Func&lt;string&gt; 传入，
/// 保持与原实现一致的“每次使用时求值”语言切换语义。
/// </summary>
internal sealed class OperationSession
{
    private readonly MainWindowViewModel _owner;
    private readonly ISystemIntegration _systemIntegration;

    // 会话内运行时状态：随每次 RunAsync 重新初始化（原 VM 私有字段迁移至此）。
    private DateTime _startTime = DateTime.MinValue;
    private DateTime _lastProgressNotification = DateTime.MinValue;
    private int _lastNotifiedCompletedCount;
    private double _lastNotifiedProcessedSizeGB;
    // Antigravity, 2026-11-04：批次 11——会话待处理总条目数（支持在无总大小或解压场景下通过单项均速提供准确 ETA 估算）。
    private int _totalItems;

    private static LanguageStrings L => LocalizationService.Instance.Strings;

    public OperationSession(MainWindowViewModel owner, ISystemIntegration systemIntegration)
    {
        _owner = owner;
        _systemIntegration = systemIntegration;
    }

    /// <summary>
    /// 设置当前会话待处理的总条目数，用于在无预估字节大小时（如提取模式或列表模式）提供基于条目数的准确 ETA 估算。
    /// </summary>
    public void SetTotalItems(int totalItems)
    {
        _totalItems = Math.Max(0, totalItems);
    }

    /// <summary>
    /// 创建统一进度回调。operationLabel 每次回调时求值，跟随当前界面语言。
    /// </summary>
    public IProgress<OperationProgressInfo> CreateProgress(Func<string> operationLabel) =>
        new Progress<OperationProgressInfo>(info => HandleProgress(operationLabel(), info));

    /// <summary>
    /// 统一操作生命周期：重置统计、创建取消令牌、执行核心委托，
    /// 成功后触发关机确认与完成汇总/通知；取消与异常分支收敛到统一文案与清理。
    /// execute 返回 null 表示“无事可做”提前退出（原实现的 return 语义），
    /// 跳过关机确认与完成汇总，但仍然执行 finally 清理。
    /// </summary>
    public async Task RunAsync(
        Func<string> operationLabel,
        Func<string> completeTitle,
        Func<CancellationToken, Task<BatchOperationOptions?>> execute)
    {
        try
        {
            _owner.IsOperating = true;
            _owner.ResetCounters();
            _startTime = DateTime.Now;
            _lastProgressNotification = DateTime.MinValue;
            _lastNotifiedCompletedCount = 0;
            _lastNotifiedProcessedSizeGB = 0;

            // 取消令牌的释放统一收敛到 finally 的 EndOperationCancellation，
            // 与原实现的单点释放语义一致（提前退出、取消、异常均可靠清理）。
            var cancellation = _owner.BeginOperationCancellation();
            var options = await execute(cancellation.Token);
            if (options == null)
            {
                return;
            }

            await _owner.OfferShutdownCancellationAsync(options);

            _owner.CommandLog += string.Format(L.OperationCompletionSummary,
                _owner.SuccessCount, _owner.FailCount, _owner.PostProcessFailCount, _owner.IgnoreCount,
                _owner.NonExistCount, _owner.IncompleteVolumeCount, _owner.AmbiguousArchiveCount) + "\n";

            _systemIntegration.ShowNotification(completeTitle(),
                string.Format(L.OperationResultSummary, _owner.SuccessCount, _owner.FailCount, _owner.PostProcessFailCount));
        }
        catch (OperationCanceledException)
        {
            var cancelledTitle = string.Format(L.OperationCancelled, operationLabel());
            _owner.CommandLog += cancelledTitle + "\n";
            _systemIntegration.ShowNotification(cancelledTitle,
                string.Format(L.SuccessFailMessage, _owner.SuccessCount, _owner.FailCount));
        }
        catch (Exception ex)
        {
            _owner.CommandLog += string.Format(L.OperationError, ex.Message) + "\n";
        }
        finally
        {
            _owner.IsOperating = false;
            _owner.CurrentSourcePath = string.Empty;
            _owner.SubProgressPercent = 0;
            _owner.HasSubProgress = false;
            _owner.EndOperationCancellation();
        }
    }

    // 统一进度回调：统计映射、耗时/速度/剩余时间估算、通知节流与三分类日志。
    private void HandleProgress(string operationLabel, OperationProgressInfo info)
    {
        _owner.CurrentFile = info.CurrentFile;
        _owner.CurrentSourcePath = info.CurrentSourcePath;
        _owner.SuccessCount = info.SuccessCount;
        _owner.FailCount = info.FailCount;
        _owner.PostProcessFailCount = info.PostProcessFailCount;
        _owner.IgnoreCount = info.IgnoreCount;
        _owner.NonExistCount = info.NonExistCount;
        _owner.IncompleteVolumeCount = info.IncompleteVolumeCount;
        _owner.AmbiguousArchiveCount = info.AmbiguousArchiveCount;
        _owner.ProcessedSizeGB = info.ProcessedSizeGB;

        if (info.SubProgressPercent.HasValue)
        {
            _owner.SubProgressPercent = info.SubProgressPercent.Value;
            _owner.HasSubProgress = true;
        }
        else
        {
            _owner.SubProgressPercent = 0;
            _owner.HasSubProgress = false;
        }

        // 更新处理统计。
        _owner.ElapsedTime = DateTime.Now - _startTime;

        // 根据已处理大小和耗时计算 MB/秒。
        if (_owner.ElapsedTime.TotalSeconds > 0.1 && _owner.ProcessedSizeGB > 0.01)
        {
            _owner.ProcessingSpeedMBPerSecond = (_owner.ProcessedSizeGB * 1024) / _owner.ElapsedTime.TotalSeconds;
        }

        // 根据当前速度估算剩余时间和完成时刻。
        // 优先路径：基于预估总字节大小与实时传输速度计算
        if (_owner.ProcessingSpeedMBPerSecond > 0.01 && _owner.TotalSizeGB > 0.01 && _owner.ProcessedSizeGB < _owner.TotalSizeGB)
        {
            double remainingSizeMB = (_owner.TotalSizeGB - _owner.ProcessedSizeGB) * 1024;
            _owner.RemainingTime = TimeSpan.FromSeconds(remainingSizeMB / _owner.ProcessingSpeedMBPerSecond);
            _owner.EstimatedCompletionTime = DateTime.Now + _owner.RemainingTime;
        }
        // Antigravity, 2026-11-04：批次 11——兜底路径：当未预估总大小（如解压或文件列表模式）时，根据平均单项耗时与剩余项数估算 ETA
        else if (_totalItems > 0)
        {
            var completedCount = info.SuccessCount + info.FailCount + info.IgnoreCount;
            if (completedCount > 0 && completedCount < _totalItems && _owner.ElapsedTime.TotalSeconds > 0.5)
            {
                var avgSecondsPerItem = _owner.ElapsedTime.TotalSeconds / completedCount;
                var remainingItems = _totalItems - completedCount;
                _owner.RemainingTime = TimeSpan.FromSeconds(avgSecondsPerItem * remainingItems);
                _owner.EstimatedCompletionTime = DateTime.Now + _owner.RemainingTime;
            }
            else
            {
                _owner.RemainingTime = TimeSpan.Zero;
                _owner.EstimatedCompletionTime = DateTime.MinValue;
            }
        }
        else
        {
            _owner.RemainingTime = TimeSpan.Zero;
            _owner.EstimatedCompletionTime = DateTime.MinValue;
        }

        MaybeShowProgressNotification(operationLabel, info);

        // GPT-5, 2026-09-29：命令输出分类改用进度对象上的标记，不再匹配本地化后的消息前缀。
        if (info.IsCommandOutput)
        {
            _owner.CommandLog += info.Message + "\n";
        }
        else if (info.IsError)
        {
            _owner.FailLog += info.Message + "\n";
        }
        else
        {
            _owner.SuccessLog += info.Message + "\n";
        }
    }

    // GPT-5, 2026-08-06：后台通知只在首个完成项、每 10 项、每增加 1GB 或每 5 分钟发送一次。
    // 这样隐藏窗口运行时仍能看到阶段性进度，同时避免大量归档产生通知风暴。
    private void MaybeShowProgressNotification(string operation, OperationProgressInfo info)
    {
        var completedCount = info.SuccessCount + info.FailCount + info.IgnoreCount;
        if (completedCount <= 0)
        {
            return;
        }

        var now = DateTime.Now;
        var first = _lastProgressNotification == DateTime.MinValue;
        var countMilestone = completedCount >= _lastNotifiedCompletedCount + 10;
        var sizeMilestone = info.ProcessedSizeGB >= _lastNotifiedProcessedSizeGB + 1;
        var timeMilestone = !first && now - _lastProgressNotification >= TimeSpan.FromMinutes(5);
        if (!first && !countMilestone && !sizeMilestone && !timeMilestone)
        {
            return;
        }

        _lastProgressNotification = now;
        _lastNotifiedCompletedCount = completedCount;
        _lastNotifiedProcessedSizeGB = info.ProcessedSizeGB;
        var remaining = _owner.RemainingTime > TimeSpan.Zero
            ? string.Format(L.ProgressNotificationRemaining, _owner.RemainingTime.ToString(@"hh\:mm\:ss"))
            : string.Empty;
        _systemIntegration.ShowNotification(
            string.Format(L.ProgressNotificationTitle, operation),
            string.Format(L.ProgressNotificationBody, completedCount, info.SuccessCount, info.FailCount, info.PostProcessFailCount, info.CurrentFile, remaining));
    }
}

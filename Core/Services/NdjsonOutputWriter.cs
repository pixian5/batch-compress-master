using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using BatchCompress.Avalonia.Core.Models;

namespace BatchCompress.Avalonia.Core.Services;

/// <summary>
/// NDJSON（Newline Delimited JSON，换行分隔 JSON）结构化输出器。
/// GLM-5.3, 2026-11-04：批次 3 引入，供 CLI --output-format json 使用。
/// 把批处理进度流逐行写成机器可读事件：
/// <code>
/// {"type":"progress","phase":"start","source":"...","message":"..."}
/// {"type":"item","status":"success|fail|skip","source":"...","message":"...",...七类计数}
/// {"type":"summary","success":1,"failed":0,...,"elapsedSeconds":3.2}
/// </code>
/// 分类依据进度快照上的计数增量（成功/失败/跳过类计数增加视为条目完成，其余视为过程消息）。
/// 命令与进程原始输出（IsCommandOutput，命令行含密码参数）按约定整条排除，
/// 任何密码文本不得进入 JSON 流。
/// </summary>
public sealed class NdjsonOutputWriter
{
    // 非 ASCII（中文等）原样输出而非 \uXXXX 转义，仍为合法 JSON，便于人类排查。
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly TextWriter _writer;

    // 上一条已处理事件的计数快照，用于把新事件分类为“条目完成”或“过程消息”。
    private int _successCount;
    private int _failCount;
    private int _postProcessFailCount;
    private int _ignoreCount;
    private int _nonExistentCount;
    private int _incompleteVolumeCount;
    private int _ambiguousCount;

    public NdjsonOutputWriter(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <summary>
    /// 处理一条进度报告。命令输出事件可能包含命令行密码参数，整条丢弃。
    /// </summary>
    public void Report(OperationProgressInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.IsCommandOutput)
        {
            return;
        }

        var succeeded = info.SuccessCount > _successCount;
        var failed = info.FailCount > _failCount;
        var skipped = info.IgnoreCount > _ignoreCount ||
                      info.NonExistCount > _nonExistentCount ||
                      info.IncompleteVolumeCount > _incompleteVolumeCount ||
                      info.AmbiguousArchiveCount > _ambiguousCount;

        // 先更新快照再输出；归档成功但后处理失败时 Success 与 PostProcessFail 同时增加，
        // 归类为 success 并在计数字段中呈现后处理失败。
        _successCount = info.SuccessCount;
        _failCount = info.FailCount;
        _postProcessFailCount = info.PostProcessFailCount;
        _ignoreCount = info.IgnoreCount;
        _nonExistentCount = info.NonExistCount;
        _incompleteVolumeCount = info.IncompleteVolumeCount;
        _ambiguousCount = info.AmbiguousArchiveCount;

        if (succeeded || failed || skipped)
        {
            var status = succeeded ? "success" : failed ? "fail" : "skip";
            Write(new
            {
                type = "item",
                status,
                source = info.CurrentFile,
                message = info.Message,
                successCount = info.SuccessCount,
                failCount = info.FailCount,
                postProcessFailCount = info.PostProcessFailCount,
                ignoreCount = info.IgnoreCount,
                nonexistentCount = info.NonExistCount,
                incompleteVolumes = info.IncompleteVolumeCount,
                ambiguous = info.AmbiguousArchiveCount
            });
        }
        else
        {
            // 计数未变化的过程消息（条目启动、大小上限提示、分卷实时子进度等）。
            var isSubProgress = info.SubProgressPercent.HasValue;
            var phase = isSubProgress ? "progress" : "start";
            if (isSubProgress)
            {
                Write(new
                {
                    type = "progress",
                    phase,
                    source = info.CurrentFile,
                    message = info.Message,
                    subProgressPercent = info.SubProgressPercent
                });
            }
            else
            {
                Write(new
                {
                    type = "progress",
                    phase,
                    source = info.CurrentFile,
                    message = info.Message
                });
            }
        }
    }

    /// <summary>
    /// 批次正常结束时输出汇总事件；elapsed 为整个批处理耗时。取消中止的批次不输出汇总。
    /// </summary>
    public void WriteSummary(TimeSpan elapsed)
    {
        Write(new
        {
            type = "summary",
            schemaVersion = 1,
            success = _successCount,
            failed = _failCount,
            postProcessFailed = _postProcessFailCount,
            ignored = _ignoreCount,
            nonexistent = _nonExistentCount,
            incompleteVolumes = _incompleteVolumeCount,
            ambiguous = _ambiguousCount,
            elapsedSeconds = Math.Round(elapsed.TotalSeconds, 3)
        });
    }

    private void Write<T>(T value)
    {
        _writer.WriteLine(JsonSerializer.Serialize(value, SerializerOptions));
        _writer.Flush();
    }
}

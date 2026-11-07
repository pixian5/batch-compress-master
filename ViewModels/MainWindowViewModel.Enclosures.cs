using System;
using System.Threading.Tasks;
using BatchCompress.Avalonia.Core.Services;
using CommunityToolkit.Mvvm.Input;

namespace BatchCompress.Avalonia.ViewModels;

// Antigravity, 2026-11-08：批次 15——拆分 MainWindowViewModel 超千行大文件。
// 本文件聚合附件目录配置管理（保存默认配置、恢复内置模版）。
public partial class MainWindowViewModel
{
    // GLM-5.3, 2026-11-04：批次 9——把界面当前附件文案持久化为启动默认配置。
    // 状态提示走 CurrentFile 状态行（GUI 既有用户可见反馈位，等价于 StatusMessage），
    // 操作过程写入文件日志便于诊断；保存失败不抛出（服务层已尽力而为并警告）。
    [RelayCommand]
    private void SaveEnclosuresConfig()
    {
        if (_enclosureConfig.SaveEnclosuresText(EnclosureList))
        {
            CurrentFile = L.EnclosuresConfigSaved;
            _fileLogger?.LogOperation("EnclosureConfig", $"保存附件默认配置（{EnclosureList.Length} 字符）");
        }
        else
        {
            CurrentFile = L.EnclosuresConfigSaveFailed;
            _fileLogger?.LogWarning("保存附件默认配置失败");
        }
    }

    // GLM-5.3, 2026-11-04：批次 9——恢复内置默认联系文案模版（仅重置界面文本，
    // 不删除已保存的配置文件；用户可编辑后再次保存覆盖）。
    [RelayCommand]
    private void ResetEnclosuresConfig()
    {
        EnclosureList = EnclosureConfigService.GetDefaultEnclosuresText();
        CurrentFile = L.EnclosuresConfigReset;
        _fileLogger?.LogOperation("EnclosureConfig", "恢复附件默认联系文案模版");
    }
}

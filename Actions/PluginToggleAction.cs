using System;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using SystemTools.Settings;

namespace SystemTools.Actions;

/// <summary>
/// "开关插件"行动：根据设置启用、禁用或切换本地插件的启用状态。
/// </summary>
[ActionInfo("SystemTools.PluginToggle", "开关插件", "\uE71D", false)]
public class PluginToggleAction(ILogger<PluginToggleAction> logger) : ActionBase<PluginToggleActionSettings>
{
    private readonly ILogger<PluginToggleAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (string.IsNullOrWhiteSpace(Settings.PluginId))
        {
            throw new InvalidOperationException("未指定要操作的插件 ID。");
        }

        var pluginService = IAppHost.TryGetService<IPluginService>();
        if (pluginService == null)
        {
            throw new InvalidOperationException("无法获取插件服务，请确保 ClassIsland 已正确加载。");
        }

        var target = pluginService.LoadedPlugins
            .FirstOrDefault(p => p.IsLocal &&
                                 string.Equals(p.Manifest.Id, Settings.PluginId, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            throw new InvalidOperationException(
                $"找不到本地插件 \"{Settings.PluginId}\"。请检查插件 ID 是否正确以及该插件是否已安装到本地。");
        }

        var currentEnabled = target.IsEnabled;
        var targetEnabled = Settings.Operation switch
        {
            PluginToggleOperation.Enable => true,
            PluginToggleOperation.Disable => false,
            _ => !currentEnabled,
        };

        if (currentEnabled == targetEnabled)
        {
            _logger.LogInformation(
                "插件 {PluginId} 已经是{Status}状态，跳过变更。",
                target.Manifest.Id, targetEnabled ? "启用" : "禁用");
        }
        else
        {
            // 写入 .disabled 文件，PluginInfo.IsEnabled 内部会同时把 RestartRequired 置 true
            target.IsEnabled = targetEnabled;

            _logger.LogInformation(
                "已{Status}插件 {PluginId}，需要重启 ClassIsland 后生效。",
                targetEnabled ? "启用" : "禁用",
                target.Manifest.Id);
        }

        if (Settings.RestartImmediately)
        {
            _logger.LogInformation("按设置立刻重启 ClassIsland，quiet={Quiet}", Settings.QuietRestart);
            AppBase.Current.Restart(Settings.QuietRestart);
        }
    }
}

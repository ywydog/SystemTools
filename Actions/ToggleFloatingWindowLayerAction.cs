using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using SystemTools.Services;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.ToggleFloatingWindowLayer", "切换悬浮窗置顶/置底", "\uE9A8", false)]
public class ToggleFloatingWindowLayerAction(ILogger<ToggleFloatingWindowLayerAction> logger) : ActionBase
{
    private readonly ILogger<ToggleFloatingWindowLayerAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleFloatingWindowLayerAction OnInvoke 开始");

        try
        {
            IAppHost.GetService<FloatingWindowService>().ToggleWindowLayer();
            _logger.LogInformation("已切换悬浮窗置顶/置底状态");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗置顶/置底失败");
            throw;
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleFloatingWindowLayerAction OnInvoke 完成");
    }
}

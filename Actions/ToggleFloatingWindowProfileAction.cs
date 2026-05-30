using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using SystemTools.Services;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.ToggleFloatingWindowProfile", "切换悬浮窗配置方案", "\uE9A8", false)]
public class ToggleFloatingWindowProfileAction(ILogger<ToggleFloatingWindowProfileAction> logger) : ActionBase
{
    private readonly ILogger<ToggleFloatingWindowProfileAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 开始");

        try
        {
            IAppHost.GetService<FloatingWindowService>().ToggleWindowProfile();
            _logger.LogInformation("已切换悬浮窗配置方案");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗配置方案失败");
            throw;
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 完成");
    }
}

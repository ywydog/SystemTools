using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using SystemTools.Services;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.ToggleFloatingWindowLayer", "切换悬浮窗置顶/置底", "\uE9A8")]
public class ToggleFloatingWindowLayerAction : ActionBase
{
    public override Task ExecuteAsync(object? settings, CancellationToken cancellationToken = new CancellationToken())
    {
        IAppHost.GetService<FloatingWindowService>().ToggleWindowLayer();
        return Task.CompletedTask;
    }
}

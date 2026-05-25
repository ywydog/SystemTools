using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SystemTools.Settings;
using Workflow = ClassIsland.Core.Models.Automation.Workflow;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.ToggleWorkflow", "开关自动化", "\uE9A8", true)]
public class ToggleWorkflowAction(ILogger<ToggleWorkflowAction> logger) : ActionBase<ToggleWorkflowSettings>
{
    private readonly ILogger<ToggleWorkflowAction> _logger = logger;

    private static readonly ConcurrentDictionary<Guid, OriginalStateSnapshot> PreviousSnapshots = new();

    /// <summary>
    /// 清空所有状态快照,防止插件卸载或重启后内存泄漏。
    /// 由 Plugin 在应用停止时调用。
    /// </summary>
    internal static void ClearSnapshots()
    {
        PreviousSnapshots.Clear();
    }

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleWorkflowAction OnInvoke 开始");

        if (Settings == null)
        {
            _logger.LogWarning("设置为空,无法执行");
            return;
        }

        var automationService = IAppHost.TryGetService<IAutomationService>();
        if (automationService?.Workflows == null)
        {
            _logger.LogError("无法获取自动化服务");
            throw new InvalidOperationException("无法获取自动化服务,请确保 ClassIsland 已正确加载。");
        }

        var targetWorkflow = FindTargetWorkflow(automationService);
        if (targetWorkflow == null)
        {
            _logger.LogWarning("未找到目标自动化: Index={Index}, Name={Name}",
                Settings.TargetWorkflowIndex, Settings.TargetWorkflowName);
            throw new InvalidOperationException($"未找到指定的自动化方案: {Settings.TargetWorkflowName}");
        }

        var actionSet = targetWorkflow.ActionSet;
        var currentStatus = actionSet.IsEnabled;

        if (IsRevertable)
        {
            PreviousSnapshots[ActionSet.Guid] = new OriginalStateSnapshot(
                actionSet.Name,
                automationService.Workflows.IndexOf(targetWorkflow),
                currentStatus,
                actionSet.Guid);
        }

        var (targetStatus, operationDescription) = Settings.EnableMode switch
        {
            true => (true, "启用"),
            false => (false, "禁用"),
            _ => (!currentStatus, !currentStatus ? "启用" : "禁用")
        };

        if (currentStatus == targetStatus)
        {
            _logger.LogInformation("自动化 "{WorkflowName}" 已经是{Operation}状态,无需操作",
                actionSet.Name, operationDescription);
        }
        else
        {
            _logger.LogInformation("正在{Operation}自动化 "{WorkflowName}" (原始: {OriginalStatus} -> 目标: {TargetStatus})",
                operationDescription, actionSet.Name, currentStatus, targetStatus);

            actionSet.IsEnabled = targetStatus;
            automationService.SaveConfig($"通过行动{operationDescription}自动化 "{actionSet.Name}"");

            _logger.LogInformation("自动化 "{WorkflowName}" 已成功{Operation}",
                actionSet.Name, operationDescription);
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleWorkflowAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousSnapshots.TryRemove(ActionSet.Guid, out var snapshot))
        {
            _logger.LogInformation("未找到触发前状态,跳过恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        var automationService = IAppHost.TryGetService<IAutomationService>();
        if (automationService?.Workflows == null)
        {
            _logger.LogError("无法获取自动化服务,恢复失败");
            return;
        }

        // 通过 Guid 精确查找,避免设置变更或列表顺序变动导致误操作其他自动化
        var targetWorkflow = automationService.Workflows
            .FirstOrDefault(w => w.ActionSet.Guid == snapshot.WorkflowGuid);

        if (targetWorkflow == null)
        {
            _logger.LogWarning("恢复时未找到目标自动化: {Name} (Guid={Guid})", snapshot.WorkflowName, snapshot.WorkflowGuid);
            return;
        }

        var actionSet = targetWorkflow.ActionSet;
        actionSet.IsEnabled = snapshot.IsEnabled;
        automationService.SaveConfig($"通过行动恢复自动化 "{actionSet.Name}" 到原始状态({snapshot.IsEnabled})");

        _logger.LogInformation("已恢复自动化 "{WorkflowName}" 为触发前状态。ActionSet={ActionSetGuid}",
            actionSet.Name, ActionSet.Guid);
    }

    private Workflow? FindTargetWorkflow(IAutomationService automationService)
    {
        Workflow? targetWorkflow = null;

        // 1. 尝试通过索引查找,并校验名称是否匹配,防止列表顺序变动后误操作
        if (Settings.TargetWorkflowIndex >= 0 && Settings.TargetWorkflowIndex < automationService.Workflows.Count)
        {
            var candidate = automationService.Workflows[Settings.TargetWorkflowIndex];
            if (candidate.ActionSet.Name == Settings.TargetWorkflowName)
            {
                targetWorkflow = candidate;
                _logger.LogDebug("通过索引 {Index} 找到自动化: {Name}",
                    Settings.TargetWorkflowIndex, targetWorkflow.ActionSet.Name);
            }
            else
            {
                _logger.LogDebug("索引 {Index} 处的自动化名称不匹配 (期望: {Expected}, 实际: {Actual}),将回退到名称查找",
                    Settings.TargetWorkflowIndex, Settings.TargetWorkflowName, candidate.ActionSet.Name);
            }
        }

        // 2. 如果索引找不到或名称不匹配,尝试通过名称查找
        if (targetWorkflow == null && !string.IsNullOrEmpty(Settings.TargetWorkflowName))
        {
            targetWorkflow = automationService.Workflows
                .FirstOrDefault(w => w.ActionSet.Name == Settings.TargetWorkflowName);

            if (targetWorkflow != null)
            {
                _logger.LogDebug("通过名称 \"{Name}\" 找到自动化", Settings.TargetWorkflowName);
                Settings.TargetWorkflowIndex = automationService.Workflows.IndexOf(targetWorkflow);
            }
        }

        return targetWorkflow;
    }

    private readonly record struct OriginalStateSnapshot(
        string WorkflowName,
        int WorkflowIndex,
        bool IsEnabled,
        Guid WorkflowGuid);
}

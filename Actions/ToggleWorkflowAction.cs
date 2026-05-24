using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SystemTools.Settings;
using ClassIsland.Shared;
using Workflow = ClassIsland.Core.Models.Automation.Workflow;

namespace SystemTools.Actions;

/// <summary>
/// 切换自动化启用状态的行动
/// </summary>
[ActionInfo("SystemTools.ToggleWorkflow", "开关自动化", "\uE9A8", true)]
public class ToggleWorkflowAction(ILogger<ToggleWorkflowAction> logger) : ActionBase<ToggleWorkflowSettings>
{
    private readonly ILogger<ToggleWorkflowAction> _logger = logger;

    // 使用静态字典存储原始状态，键为 ActionSet.Guid
    private static readonly ConcurrentDictionary<Guid, OriginalStateSnapshot> OriginalStates = new();

    /// <summary>
    /// 原始状态快照
    /// </summary>
    private readonly record struct OriginalStateSnapshot(
        string WorkflowName,
        int WorkflowIndex,
        bool IsEnabled);

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleWorkflowAction OnInvoke 开始");

        if (Settings == null)
        {
            _logger.LogWarning("设置为空，无法执行");
            return;
        }

        try
        {
            var automationService = IAppHost.TryGetService<IAutomationService>();
            if (automationService?.Workflows == null)
            {
                _logger.LogError("无法获取自动化服务");
                throw new InvalidOperationException("无法获取自动化服务，请确保 ClassIsland 已正确加载。");
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

            // 如果启用了恢复功能，保存原始状态
            if (IsRevertable && Settings.RevertToOriginal)
            {
                var snapshot = new OriginalStateSnapshot(
                    actionSet.Name,
                    automationService.Workflows.IndexOf(targetWorkflow),
                    currentStatus);

                OriginalStates[ActionSet.Guid] = snapshot;
                _logger.LogDebug("已保存原始状态: ActionSet={ActionSetGuid}, Workflow={WorkflowName}, IsEnabled={IsEnabled}",
                    ActionSet.Guid, actionSet.Name, currentStatus);
            }

            // 确定目标状态
            bool targetStatus;
            string operationDescription;

            switch (Settings.EnableMode)
            {
                case true:
                    targetStatus = true;
                    operationDescription = "启用";
                    break;
                case false:
                    targetStatus = false;
                    operationDescription = "禁用";
                    break;
                default:
                    targetStatus = !currentStatus;
                    operationDescription = targetStatus ? "启用" : "禁用";
                    break;
            }

            // 执行状态切换
            if (currentStatus == targetStatus)
            {
                _logger.LogInformation("自动化 \"{WorkflowName}\" 已经是{Operation}状态，无需操作",
                    actionSet.Name, operationDescription);
            }
            else
            {
                _logger.LogInformation("正在{Operation}自动化 \"{WorkflowName}\" (原始: {OriginalStatus} -> 目标: {TargetStatus})",
                    operationDescription, actionSet.Name, currentStatus, targetStatus);

                actionSet.IsEnabled = targetStatus;
                automationService.SaveConfig($"通过行动{operationDescription}自动化 \"{actionSet.Name}\"");

                _logger.LogInformation("自动化 \"{WorkflowName}\" 已成功{Operation}",
                    actionSet.Name, operationDescription);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换自动化状态失败");
            throw;
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleWorkflowAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        _logger.LogDebug("ToggleWorkflowAction OnRevert 开始");

        if (Settings == null)
        {
            _logger.LogWarning("设置为空，无法执行恢复");
            await base.OnRevert();
            return;
        }

        // 检查是否启用了自动恢复
        if (!Settings.RevertToOriginal)
        {
            _logger.LogInformation("恢复功能已禁用 (RevertToOriginal=false)，跳过恢复操作");
            await base.OnRevert();
            return;
        }

        try
        {
            var automationService = IAppHost.TryGetService<IAutomationService>();
            if (automationService?.Workflows == null)
            {
                _logger.LogError("无法获取自动化服务");
                throw new InvalidOperationException("无法获取自动化服务。");
            }

            // 尝试获取原始状态
            if (!OriginalStates.TryRemove(ActionSet.Guid, out var snapshot))
            {
                _logger.LogWarning("未找到原始状态快照，可能未启用恢复或已被清除。ActionSet={ActionSetGuid}", ActionSet.Guid);
                await base.OnRevert();
                return;
            }

            _logger.LogDebug("找到原始状态: Workflow={WorkflowName}, Index={Index}, IsEnabled={IsEnabled}",
                snapshot.WorkflowName, snapshot.WorkflowIndex, snapshot.IsEnabled);

            // 查找目标自动化（优先使用索引，回退到名称）
            Workflow? targetWorkflow = null;

            if (snapshot.WorkflowIndex >= 0 && snapshot.WorkflowIndex < automationService.Workflows.Count)
            {
                var workflowByIndex = automationService.Workflows[snapshot.WorkflowIndex];
                if (workflowByIndex.ActionSet.Name == snapshot.WorkflowName)
                {
                    targetWorkflow = workflowByIndex;
                    _logger.LogDebug("通过索引 {Index} 找到自动化", snapshot.WorkflowIndex);
                }
            }

            if (targetWorkflow == null)
            {
                targetWorkflow = automationService.Workflows
                    .FirstOrDefault(w => w.ActionSet.Name == snapshot.WorkflowName);

                if (targetWorkflow != null)
                {
                    _logger.LogDebug("通过名称 \"{Name}\" 找到自动化", snapshot.WorkflowName);
                }
            }

            if (targetWorkflow == null)
            {
                _logger.LogWarning("恢复时未找到目标自动化: {Name}", snapshot.WorkflowName);
                await base.OnRevert();
                return;
            }

            var actionSet = targetWorkflow.ActionSet;
            var currentStatus = actionSet.IsEnabled;
            var originalStatus = snapshot.IsEnabled;

            if (currentStatus == originalStatus)
            {
                _logger.LogInformation("自动化 \"{WorkflowName}\" 当前状态({CurrentStatus})与原始状态一致，无需恢复",
                    actionSet.Name, currentStatus);
            }
            else
            {
                _logger.LogInformation("正在恢复自动化 \"{WorkflowName}\" 状态: {CurrentStatus} -> {OriginalStatus}",
                    actionSet.Name, currentStatus, originalStatus);

                actionSet.IsEnabled = originalStatus;
                automationService.SaveConfig($"通过行动恢复自动化 \"{actionSet.Name}\" 到原始状态({originalStatus})");

                _logger.LogInformation("自动化 \"{WorkflowName}\" 已成功恢复到原始状态",
                    actionSet.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复自动化状态失败");
            throw;
        }

        await base.OnRevert();
        _logger.LogDebug("ToggleWorkflowAction OnRevert 完成");
    }

    /// <summary>
    /// 查找目标自动化
    /// </summary>
    private Workflow? FindTargetWorkflow(IAutomationService automationService)
    {
        Workflow? targetWorkflow = null;

        // 1. 尝试通过索引查找
        if (Settings.TargetWorkflowIndex >= 0 && Settings.TargetWorkflowIndex < automationService.Workflows.Count)
        {
            targetWorkflow = automationService.Workflows[Settings.TargetWorkflowIndex];
            _logger.LogDebug("通过索引 {Index} 找到自动化: {Name}",
                Settings.TargetWorkflowIndex, targetWorkflow.ActionSet.Name);
        }

        // 2. 如果索引找不到，尝试通过名称查找
        if (targetWorkflow == null && !string.IsNullOrEmpty(Settings.TargetWorkflowName))
        {
            targetWorkflow = automationService.Workflows
                .FirstOrDefault(w => w.ActionSet.Name == Settings.TargetWorkflowName);

            if (targetWorkflow != null)
            {
                _logger.LogDebug("通过名称 \"{Name}\" 找到自动化", Settings.TargetWorkflowName);
                // 更新索引以便下次使用
                Settings.TargetWorkflowIndex = automationService.Workflows.IndexOf(targetWorkflow);
            }
        }

        return targetWorkflow;
    }
}
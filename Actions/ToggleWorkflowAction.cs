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
public class ToggleWorkflowAction : ActionBase<ToggleWorkflowSettings>
{
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
        if (Settings == null)
        {
            return;
        }

        try
        {
            var automationService = IAppHost.TryGetService<IAutomationService>();
            if (automationService?.Workflows == null)
            {
                throw new InvalidOperationException("无法获取自动化服务，请确保 ClassIsland 已正确加载。");
            }

            var targetWorkflow = FindTargetWorkflow(automationService);
            if (targetWorkflow == null)
            {
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
            if (currentStatus != targetStatus)
            {
                actionSet.IsEnabled = targetStatus;
                automationService.SaveConfig($"通过行动{operationDescription}自动化 \"{actionSet.Name}\"");
            }
        }
        catch (Exception)
        {
            throw;
        }

        await base.OnInvoke();
    }

    protected override async Task OnRevert()
    {
        if (Settings == null)
        {
            await base.OnRevert();
            return;
        }

        // 检查是否启用了自动恢复
        if (!Settings.RevertToOriginal)
        {
            await base.OnRevert();
            return;
        }

        try
        {
            var automationService = IAppHost.TryGetService<IAutomationService>();
            if (automationService?.Workflows == null)
            {
                throw new InvalidOperationException("无法获取自动化服务。");
            }

            // 尝试获取原始状态
            if (!OriginalStates.TryRemove(ActionSet.Guid, out var snapshot))
            {
                await base.OnRevert();
                return;
            }

            // 查找目标自动化（优先使用索引，回退到名称）
            Workflow? targetWorkflow = null;

            if (snapshot.WorkflowIndex >= 0 && snapshot.WorkflowIndex < automationService.Workflows.Count)
            {
                var workflowByIndex = automationService.Workflows[snapshot.WorkflowIndex];
                if (workflowByIndex.ActionSet.Name == snapshot.WorkflowName)
                {
                    targetWorkflow = workflowByIndex;
                }
            }

            if (targetWorkflow == null)
            {
                targetWorkflow = automationService.Workflows
                    .FirstOrDefault(w => w.ActionSet.Name == snapshot.WorkflowName);
            }

            if (targetWorkflow == null)
            {
                await base.OnRevert();
                return;
            }

            var actionSet = targetWorkflow.ActionSet;
            var currentStatus = actionSet.IsEnabled;
            var originalStatus = snapshot.IsEnabled;

            if (currentStatus != originalStatus)
            {
                actionSet.IsEnabled = originalStatus;
                automationService.SaveConfig($"通过行动恢复自动化 \"{actionSet.Name}\" 到原始状态({originalStatus})");
            }
        }
        catch (Exception)
        {
            throw;
        }

        await base.OnRevert();
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
        }

        // 2. 如果索引找不到，尝试通过名称查找
        if (targetWorkflow == null && !string.IsNullOrEmpty(Settings.TargetWorkflowName))
        {
            targetWorkflow = automationService.Workflows
                .FirstOrDefault(w => w.ActionSet.Name == Settings.TargetWorkflowName);

            if (targetWorkflow != null)
            {
                // 更新索引以便下次使用
                Settings.TargetWorkflowIndex = automationService.Workflows.IndexOf(targetWorkflow);
            }
        }

        return targetWorkflow;
    }
}

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SystemTools.Settings;

namespace SystemTools.Actions;

[ActionInfo("SystemTools.AdvancedShutdown", "高级计时关机", "\uE4C4", false)]
public class AdvancedShutdownAction(ILogger<AdvancedShutdownAction> logger) : ActionBase<AdvancedShutdownSettings>
{
    private readonly ILogger<AdvancedShutdownAction> _logger = logger;
    private readonly object _syncLock = new();
    private DateTimeOffset _shutdownAt = DateTimeOffset.MinValue;

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("AdvancedShutdownAction OnInvoke 开始");

        var configuredMinutes = Math.Max(1, Settings?.Minutes ?? 2);
        ScheduleShutdown(configuredMinutes);

        await ShowDialogAsync();
        await base.OnInvoke();
    }

    private void ScheduleShutdown(int minutes)
    {
        var safeMinutes = Math.Max(1, minutes);
        lock (_syncLock)
        {
            _shutdownAt = DateTimeOffset.Now.AddMinutes(safeMinutes);
        }

        ExecuteShutdownCommand($"-s -t {safeMinutes * 60}");
    }

    private int ExtendShutdown(int extendMinutes)
    {
        var safeExtendMinutes = Math.Max(1, extendMinutes);
        DateTimeOffset targetTime;

        lock (_syncLock)
        {
            var baseline = _shutdownAt > DateTimeOffset.Now ? _shutdownAt : DateTimeOffset.Now;
            _shutdownAt = baseline.AddMinutes(safeExtendMinutes);
            targetTime = _shutdownAt;
        }

        var totalSeconds = (int)Math.Ceiling((targetTime - DateTimeOffset.Now).TotalSeconds);
        totalSeconds = Math.Max(60, totalSeconds);

        ExecuteShutdownCommand("-a");
        ExecuteShutdownCommand($"-s -t {totalSeconds}");

        return (int)Math.Ceiling(totalSeconds / 60.0);
    }

    private void CancelShutdownPlan()
    {
        ExecuteShutdownCommand("-a");
        lock (_syncLock)
        {
            _shutdownAt = DateTimeOffset.MinValue;
        }
    }

    private void ExecuteShutdownCommand(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行关机命令失败: {Args}", args);
            throw;
        }
    }


    private int GetRemainingMinutes()
    {
        lock (_syncLock)
        {
            var remainingSeconds = (int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds);
            remainingSeconds = Math.Max(60, remainingSeconds);
            return (int)Math.Ceiling(remainingSeconds / 60.0);
        }
    }

    private async Task ShowDialogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new Window
            {
                Title = "高级计时关机",
                Width = 380,
                Height = 200,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var message = new TextBlock
            {
                Text = $"将在{GetRemainingMinutes()}分钟后关机……",
                FontSize = 15,
                Margin = new(0, 0, 0, 12)
            };

            var readButton = new Button { Content = "已阅", Width = 90 };
            var cancelButton = new Button { Content = "取消计划", Width = 90 };
            var extendButton = new Button { Content = "延长时间", Width = 90 };

            readButton.Click += (_, _) => dialog.Close();
            cancelButton.Click += (_, _) =>
            {
                CancelShutdownPlan();
                dialog.Close();
            };

            extendButton.Click += async (_, _) =>
            {
                var extendMinutes = await ShowExtendInputDialogAsync(dialog);
                if (extendMinutes.HasValue)
                {
                    ExtendShutdown(extendMinutes.Value);
                    dialog.Close();
                }
            };

            dialog.Content = new StackPanel
            {
                Margin = new(16),
                Spacing = 8,
                Children =
                {
                    message,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { readButton, cancelButton, extendButton }
                    }
                }
            };

            dialog.Show();
            await Task.CompletedTask;
        });
    }

    private static async Task<int?> ShowExtendInputDialogAsync(Window owner)
    {
        var input = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 720,
            Value = 1,
            Width = 120
        };

        var result = (int?)null;
        var dialog = new Window
        {
            Title = "延长关机时间",
            Width = 320,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "请输入要延长的分钟数：" },
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            new Button { Content = "确认", Width = 80, Name = "ConfirmButton" },
                            new Button { Content = "取消", Width = 80, Name = "CancelButton" }
                        }
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel
            && panel.Children[2] is StackPanel buttonPanel
            && buttonPanel.Children[0] is Button confirm
            && buttonPanel.Children[1] is Button cancel)
        {
            confirm.Click += (_, _) =>
            {
                result = (int)(input.Value ?? 1);
                dialog.Close();
            };

            cancel.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(owner);
        return result;
    }
}

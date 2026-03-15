using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SystemTools.Triggers;

public class LongIdleTriggerConfig : ObservableRecipient
{
    private int _idleMinutes = 5;

    public int IdleMinutes
    {
        get => _idleMinutes;
        set
        {
            var normalized = Math.Max(1, value);
            if (_idleMinutes == normalized) return;
            _idleMinutes = normalized;
            OnPropertyChanged();
        }
    }
}

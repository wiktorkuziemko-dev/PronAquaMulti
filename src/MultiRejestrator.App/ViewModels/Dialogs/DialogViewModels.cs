using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels.Dialogs;

/// <summary>Okno dialogowe wyświetlane nad oknem głównym. Wynik przez <see cref="Result"/>.</summary>
public abstract partial class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> Result => _tcs.Task;

    public virtual bool CanClose => true;

    [RelayCommand]
    protected virtual void Confirm() => _tcs.TrySetResult(true);

    [RelayCommand]
    protected virtual void Cancel() => _tcs.TrySetResult(false);
}

public sealed partial class MessageDialogViewModel : DialogViewModel
{
    public required string Title { get; init; }

    public required string Text { get; init; }

    public string Hint { get; init; } = string.Empty;

    public bool IsError { get; init; }

    public bool IsSuccess { get; init; }

    public bool IsWarning => !IsError && !IsSuccess;

    public bool HasHint => !string.IsNullOrEmpty(Hint);
}

public enum UnsavedChoice
{
    Cancel,
    Discard,
    Save,
}

public sealed partial class UnsavedDialogViewModel : DialogViewModel
{
    public required string DocumentName { get; init; }

    public UnsavedChoice Choice { get; private set; } = UnsavedChoice.Cancel;

    [RelayCommand]
    private void Save()
    {
        Choice = UnsavedChoice.Save;
        Confirm();
    }

    [RelayCommand]
    private void Discard()
    {
        Choice = UnsavedChoice.Discard;
        Confirm();
    }
}

public sealed partial class SendDialogViewModel : DialogViewModel
{
    public required string DeviceSerial { get; init; }

    public required string Port { get; init; }

    public required string ConfigurationSerial { get; init; }

    public required string DocumentName { get; init; }

    public required int FrameCount { get; init; }

    public bool SerialMismatch => DeviceSerial != ConfigurationSerial;

    public string ConfirmText => $"Wyślij do {DeviceSerial}";

    [ObservableProperty]
    public partial bool SetTime { get; set; }
}

public sealed partial class ProgressDialogViewModel : DialogViewModel
{
    public required string Title { get; init; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Percent { get; set; }

    public override bool CanClose => false;
}

public sealed partial class ChangeCodeDialogViewModel : DialogViewModel
{
    public required string DeviceSerial { get; init; }

    public required string CurrentCode { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError), nameof(IsValid))]
    public partial string NewCode { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError), nameof(IsValid))]
    public partial string RepeatCode { get; set; } = string.Empty;

    public string? Error =>
        ParameterCodec.Validate(ParameterCatalog.Configuration[ParameterIds.AccessCode], NewCode) is { } e ? "Kod dostępu: " + e.ToLowerInvariant()
        : NewCode != RepeatCode ? "Kody nie są takie same."
        : null;

    public bool HasError => Error is not null && (NewCode.Length > 0 || RepeatCode.Length > 0);

    public bool IsValid => Error is null;
}

public sealed partial class CopyChannelDialogViewModel : DialogViewModel
{
    public required int SourceChannel { get; init; }

    public string Title => $"Kopiuj ustawienia kanału {SourceChannel + 1}";

    public ObservableCollection<CopyTargetViewModel> Targets { get; } = new();

    public IEnumerable<int> SelectedChannels => Targets.Where(t => t.IsChecked && t.IsEnabled).Select(t => t.Channel);

    public bool HasSelection => SelectedChannels.Any();

    public void NotifySelection() => OnPropertyChanged(nameof(HasSelection));
}

public sealed partial class CopyTargetViewModel : ObservableObject
{
    private readonly CopyChannelDialogViewModel _owner;

    public CopyTargetViewModel(CopyChannelDialogViewModel owner, int channel, bool enabled)
    {
        _owner = owner;
        Channel = channel;
        IsEnabled = enabled;
    }

    public int Channel { get; }

    public string Label => $"Kanał {Channel + 1}";

    public bool IsEnabled { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value) => _owner.NotifySelection();
}

public sealed partial class SettingsDialogViewModel : DialogViewModel
{
    public required string SettingsPath { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    public partial string DefaultAccessCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SetTimeOnSend { get; set; }

    public string? Error => ParameterCodec.Validate(ParameterCatalog.Configuration[ParameterIds.AccessCode], DefaultAccessCode);

    public bool IsValid => Error is null;
}

public sealed class AboutDialogViewModel : DialogViewModel
{
    public required string Version { get; init; }
}

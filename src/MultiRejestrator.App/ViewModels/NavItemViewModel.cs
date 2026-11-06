using CommunityToolkit.Mvvm.ComponentModel;
using MultiRejestrator.Core.Parameters;

namespace MultiRejestrator.App.ViewModels;

/// <summary>Pozycja menu bocznego: sekcja konfiguracji albo kanał.</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    public required string Title { get; init; }

    public required string Icon { get; init; }

    public Section? Section { get; init; }

    public int? Channel { get; init; }

    public bool IsChannel => Channel is not null;

    [ObservableProperty]
    public partial string SubText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DotColor { get; set; } = "#9CA3AF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial int Errors { get; set; }

    public bool HasErrors => Errors > 0;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsCoupled { get; set; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public static class Icons
    {
        public const string General = "M2.5 4h11M2.5 8h11M2.5 12h7";
        public const string Modem = "M3 13.5v-2M6.5 13.5V9M10 13.5V6.5M13.5 13.5V3.5";
        public const string Apn = "M8 2a6 6 0 1 0 0 12A6 6 0 0 0 8 2zM2 8h12M8 2c2.2 2.2 2.2 9.8 0 12M8 2c-2.2 2.2-2.2 9.8 0 12";
        public const string Servers = "M2.5 2.5h11v4.5h-11zM2.5 9h11v4.5h-11zM5 4.75h.01M5 11.25h.01";
        public const string Transmission = "M3.5 7h9v6.5h-9zM5.5 7V5a2.5 2.5 0 0 1 5 0v2";
        public const string Channel = "M1.5 8h2.5l1.5-4 3 8 1.5-4h4.5";
    }
}

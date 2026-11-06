using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MultiRejestrator.App.Services;
using MultiRejestrator.App.ViewModels;
using MultiRejestrator.Core.Settings;

namespace MultiRejestrator.App.Views;

public partial class MainWindow : Window, IFileService
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel(this, AppSettings.DefaultPath, DemoDevice.Open);
        DataContext = vm;
        vm.LogLines.CollectionChanged += OnLogChanged;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            {
                ContentScroll.Offset = default;
            }
        };
        Closing += OnClosing;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext!;

    public async Task<string?> PickOpenAsync(string title, string? folder)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await Folder(folder),
            FileTypeFilter =
            [
                new FilePickerFileType("Konfiguracja MultiRejestrator") { Patterns = ["*.txt"] },
                FilePickerFileTypes.All,
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveAsync(string title, string suggestedName, string filterName, string pattern, string? folder)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(pattern).TrimStart('.'),
            SuggestedStartLocation = await Folder(folder),
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [pattern] }],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    private async Task<IStorageFolder?> Folder(string? path) =>
        string.IsNullOrEmpty(path) || !Directory.Exists(path) ? null : await StorageProvider.TryGetFolderFromPathAsync(path);

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;
        if (await ViewModel.PrepareCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}

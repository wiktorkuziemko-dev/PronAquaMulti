using Avalonia;
using MultiRejestrator.App.Services;
using MultiRejestrator.App.ViewModels;

namespace MultiRejestrator.App;

internal sealed class Program
{
    // Avalonia: przed AppMain nie wolno używać API zależnych od SynchronizationContext.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--symulator", StringComparer.OrdinalIgnoreCase))
        {
            DemoDevice.Enabled = true;
            MainViewModel.ExtraPorts.Add(DemoDevice.PortName);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Konfiguracja Avalonii – używana także przez podgląd w IDE.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

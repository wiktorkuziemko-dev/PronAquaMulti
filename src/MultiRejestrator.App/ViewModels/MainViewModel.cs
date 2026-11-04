using System.Reflection;

namespace MultiRejestrator.App.ViewModels;

/// <summary>
/// Model widoku głównego okna. Na razie tylko dane paska statusu (pkt 19 Założeń);
/// polecenia paska funkcji zostaną podpięte w kolejnych etapach.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string NotAvailable = "—";

    public string AppVersion { get; } =
        "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

    public string ComPort => NotAvailable;

    public string ConnectionState => "Brak połączenia";

    public string SerialNumber => NotAvailable;

    public string FirmwareVersion => NotAvailable;

    public string ProtocolVersion => NotAvailable;
}

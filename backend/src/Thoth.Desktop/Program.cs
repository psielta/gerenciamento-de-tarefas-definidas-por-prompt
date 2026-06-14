using System.Threading;
using System.Windows.Forms;

namespace Thoth.Desktop;

internal static class Program
{
    private const string SingleInstanceMutexName = "Thoth.Desktop.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Ja existe um Thoth aberto (e, portanto, a API ja esta no ar). Evita subir uma segunda API.
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

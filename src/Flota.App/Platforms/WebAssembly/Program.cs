using System.Runtime.InteropServices.JavaScript;
using Uno.UI.Hosting;

namespace Flota.App;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        // Adres strony — domyślny adres Flota.Api, gdy aplikacja jest udostępniana przez usługę (ta sama domena)
        App.BrowserOrigin = GetOrigin();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWebAssembly()
            .Build();

        await host.RunAsync();
    }

    [JSImport("globalThis.eval")]
    private static partial string Eval(string code);

    static string GetOrigin() => Eval("window.location.origin");
}

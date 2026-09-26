using Flota.Client;
using Flota.Core.Contracts;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Flota.App.Services;

/// <summary>Eksport tabel do XLSX (plik generowany przez Flota.Api) i zapis przez okno wyboru pliku.</summary>
public sealed class ExportService(FlotaApiClient api, Notifier notifier)
{
    public async Task ExportAsync(ExportTable table, string fileName, CancellationToken ct = default)
    {
        try
        {
            var bytes = await api.ExportXlsxAsync(table, ct);
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Sanitize(fileName),
            };
            picker.FileTypeChoices.Add(Loc.Get("ExcelFileType"), [".xlsx"]);
#if WINDOWS
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
#endif
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await FileIO.WriteBytesAsync(file, bytes);
            notifier.Success(Loc.Format("ExportSaved", file.Name));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notifier.Error(ex);
        }
    }

    static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

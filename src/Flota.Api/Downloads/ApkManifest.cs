using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Flota.Api.Downloads;

/// <summary>
/// Odczyt wersji (<c>android:versionName</c>) z manifestu pakietu APK — plik <c>AndroidManifest.xml</c> w formacie binarnym
/// Android (AXML): pula ciągów, mapa identyfikatorów zasobów, elementy z atrybutami.
/// </summary>
public static class ApkManifest
{
    const ushort ChunkXml = 0x0003;
    const ushort ChunkStringPool = 0x0001;
    const ushort ChunkResourceMap = 0x0180;
    const ushort ChunkStartElement = 0x0102;
    const uint Utf8Flag = 0x100;
    const uint NoIndex = 0xFFFFFFFF;
    const byte TypeString = 0x03;
    // Identyfikator zasobu atrybutu android:versionName (nazwa atrybutu bywa usunięta z puli ciągów)
    const uint VersionNameResourceId = 0x0101021C;

    /// <summary>Wersja z pakietu APK; <c>null</c>, gdy pakiet bez manifestu albo manifest nieczytelny.</summary>
    public static string? ReadVersionName(string apkPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(apkPath);
            var entry = zip.GetEntry("AndroidManifest.xml");
            if (entry is null) return null;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ReadVersionName(ms.ToArray());
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Wersja z binarnego manifestu; <c>null</c>, gdy brak atrybutu lub format nieobsługiwany.</summary>
    public static string? ReadVersionName(ReadOnlySpan<byte> axml)
    {
        if (axml.Length < 8 || U16(axml, 0) != ChunkXml) return null;
        string[] strings = [];
        uint[] resourceIds = [];
        int offset = U16(axml, 2);
        while (offset + 8 <= axml.Length)
        {
            var type = U16(axml, offset);
            var headerSize = U16(axml, offset + 2);
            var size = (int)U32(axml, offset + 4);
            if (size < 8 || offset + size > axml.Length) return null;
            var chunk = axml.Slice(offset, size);
            switch (type)
            {
                case ChunkStringPool:
                    strings = ReadStringPool(chunk);
                    break;
                case ChunkResourceMap:
                    resourceIds = new uint[(size - headerSize) / 4];
                    for (var i = 0; i < resourceIds.Length; i++) resourceIds[i] = U32(chunk, headerSize + i * 4);
                    break;
                case ChunkStartElement:
                    var version = VersionFromElement(chunk, headerSize, strings, resourceIds);
                    if (version is not null) return version;
                    break;
            }
            offset += size;
        }
        return null;
    }

    static string? VersionFromElement(ReadOnlySpan<byte> chunk, int headerSize, string[] strings, uint[] resourceIds)
    {
        // Rozszerzenie elementu: ns, name, attributeStart, attributeSize, attributeCount
        var ext = headerSize;
        var name = Str(strings, U32(chunk, ext + 4));
        if (name != "manifest") return null;
        var attributeStart = U16(chunk, ext + 8);
        var attributeSize = U16(chunk, ext + 10);
        var attributeCount = U16(chunk, ext + 12);
        for (var i = 0; i < attributeCount; i++)
        {
            var a = ext + attributeStart + i * attributeSize;
            if (a + 20 > chunk.Length) return null;
            var nameIndex = U32(chunk, a + 4);
            var isVersionName = Str(strings, nameIndex) == "versionName"
                || (nameIndex < resourceIds.Length && resourceIds[nameIndex] == VersionNameResourceId);
            if (!isVersionName) continue;
            var raw = U32(chunk, a + 8);
            if (raw != NoIndex) return Str(strings, raw);
            return chunk[a + 15] == TypeString ? Str(strings, U32(chunk, a + 16)) : null;
        }
        return null;
    }

    static string[] ReadStringPool(ReadOnlySpan<byte> chunk)
    {
        var headerSize = U16(chunk, 2);
        var count = (int)U32(chunk, 8);
        var flags = U32(chunk, 16);
        var stringsStart = (int)U32(chunk, 20);
        var utf8 = (flags & Utf8Flag) != 0;
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            var p = stringsStart + (int)U32(chunk, headerSize + i * 4);
            result[i] = utf8 ? Utf8At(chunk, p) : Utf16At(chunk, p);
        }
        return result;
    }

    static string Utf8At(ReadOnlySpan<byte> chunk, int p)
    {
        // Długość w znakach, następnie w bajtach (1 lub 2 bajty każda)
        p += (chunk[p] & 0x80) != 0 ? 2 : 1;
        var length = chunk[p] & 0x7F;
        if ((chunk[p] & 0x80) != 0) { length = (length << 8) | chunk[p + 1]; p += 2; }
        else p += 1;
        return Encoding.UTF8.GetString(chunk.Slice(p, length));
    }

    static string Utf16At(ReadOnlySpan<byte> chunk, int p)
    {
        int length = U16(chunk, p);
        if ((length & 0x8000) != 0) { length = ((length & 0x7FFF) << 16) | U16(chunk, p + 2); p += 4; }
        else p += 2;
        return Encoding.Unicode.GetString(chunk.Slice(p, length * 2));
    }

    static string? Str(string[] strings, uint index) => index < strings.Length ? strings[index] : null;

    static ushort U16(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b[offset..]);

    static uint U32(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
}

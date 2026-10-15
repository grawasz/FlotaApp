using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Flota.Api.Auth;

/// <summary>
/// Nagłówki odwrotnego proxy (Caddy): <c>X-Forwarded-For</c> i <c>X-Forwarded-Proto</c> przyjmowane wyłącznie od adresów
/// z zaufanych sieci i adresów z konfiguracji (<c>ForwardedHeaders:KnownNetworks</c>, <c>ForwardedHeaders:KnownProxies</c>);
/// domyślnie lista pusta — nagłówki ignorowane. Schemat żądania z nagłówka decyduje o atrybucie <c>Secure</c> ciasteczka.
/// </summary>
public static class ProxyHeaders
{
    public const string Section = "ForwardedHeaders";

    public static ForwardedHeadersOptions Options(IConfiguration configuration)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };
        // Bez domyślnego zaufania do adresów pętli zwrotnej
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var network in configuration.GetSection($"{Section}:KnownNetworks").Get<string[]>() ?? [])
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        foreach (var proxy in configuration.GetSection($"{Section}:KnownProxies").Get<string[]>() ?? [])
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        return options;
    }
}

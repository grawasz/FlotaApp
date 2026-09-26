using System.Text.Json;
using Flota.Client;
using Flota.Core.Contracts;
using Windows.Security.Credentials;

namespace Flota.App.Services;

/// <summary>
/// Tokeny Flota.Api w bezpiecznym magazynie platformy (<see cref="PasswordVault"/>: Windows Credential Locker,
/// Android Keystore, iOS Keychain). Hasło użytkownika nie jest przechowywane.
/// </summary>
public sealed class SecureTokenStore : ITokenStore
{
    const string Resource = "Flota.Api";
    const string UserName = "tokens";

    public SecureTokenStore()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(Resource, UserName);
            credential.RetrievePassword();
            Current = JsonSerializer.Deserialize(credential.Password, FlotaJsonContext.Default.TokenResponse);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Brak zapisanych tokenów lub nieczytelny wpis — wymagane logowanie
            Current = null;
        }
    }

    public TokenResponse? Current { get; private set; }

    public Task SaveAsync(TokenResponse tokens)
    {
        Current = tokens;
        try
        {
            var vault = new PasswordVault();
            Remove(vault);
            vault.Add(new PasswordCredential(Resource, UserName, JsonSerializer.Serialize(tokens, FlotaJsonContext.Default.TokenResponse)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Magazyn niedostępny — sesja tylko w pamięci
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Current = null;
        try
        {
            Remove(new PasswordVault());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
        return Task.CompletedTask;
    }

    static void Remove(PasswordVault vault)
    {
        try
        {
            foreach (var c in vault.FindAllByResource(Resource)) vault.Remove(c);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Brak wpisów
        }
    }
}

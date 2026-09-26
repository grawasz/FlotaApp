using Flota.Core.Contracts;

namespace Flota.Client;

/// <summary>Przechowywanie tokenów Flota.Api na urządzeniu (bezpieczny magazyn platformy).</summary>
public interface ITokenStore
{
    TokenResponse? Current { get; }

    Task SaveAsync(TokenResponse tokens);

    Task ClearAsync();
}

/// <summary>Magazyn tokenów w pamięci (testy, sesje bez utrwalania).</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    public TokenResponse? Current { get; private set; }

    public Task SaveAsync(TokenResponse tokens)
    {
        Current = tokens;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Current = null;
        return Task.CompletedTask;
    }
}

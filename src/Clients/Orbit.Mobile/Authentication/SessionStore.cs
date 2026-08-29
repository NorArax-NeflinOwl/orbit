namespace Orbit.Mobile.Authentication;

/// <summary>
/// The app's view of who is signed in. Sits in front of <see cref="ISessionStorage"/> so the Keychain
/// is read once per launch rather than on every outgoing request, and so the shell can react when the
/// session goes away - a rejected refresh signs the user out from deep inside an HTTP call, with no
/// screen involved.
/// </summary>
public sealed class SessionStore
{
    private readonly ISessionStorage _storage;

    // Guards the first read: several requests can start at once on launch, and each finding nothing
    // loaded would send them all to the Keychain together.
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private UserSession? _current;
    private bool _loaded;

    public SessionStore(ISessionStorage storage) => _storage = storage;

    /// <summary>Raised whenever the signed-in user changes, including signing out.</summary>
    public event Action<UserSession?>? Changed;

    public async ValueTask<UserSession?> GetAsync()
    {
        if (_loaded)
        {
            return _current;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (!_loaded)
            {
                _current = await _storage.ReadAsync();
                _loaded = true;
            }
        }
        finally
        {
            _loadLock.Release();
        }

        return _current;
    }

    public async Task SetAsync(UserSession session)
    {
        await _storage.WriteAsync(session);
        _current = session;
        _loaded = true;
        Changed?.Invoke(session);
    }

    public async Task ClearAsync()
    {
        await _storage.ClearAsync();
        _current = null;
        _loaded = true;
        Changed?.Invoke(null);
    }
}

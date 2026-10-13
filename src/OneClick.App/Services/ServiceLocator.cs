namespace OneClick.App.Services;

/// <summary>
/// Lightweight service locator used by pages to resolve services.
/// Populated at startup from the DI container.
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static void Configure(IServiceProvider provider) => _provider = provider;

    public static T Get<T>() where T : notnull
    {
        if (_provider is null)
            throw new InvalidOperationException("ServiceLocator has not been configured.");
        return _provider.GetService(typeof(T)) is T svc
            ? svc
            : throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}

﻿namespace VpnHood.Core.Toolkit.Utils;

public class Singleton<T> where T : Singleton<T>
{
    private static T? _instance;

    public static T Instance =>
        _instance ?? throw new InvalidOperationException($"{typeof(T).Name} has not been initialized yet.");

    public static bool IsInit => _instance != null;

    protected Singleton()
    {
        if (IsInit) throw new InvalidOperationException($"{typeof(T).Name} is already initialized.");
        _instance = (T?)this;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsInit)
            return;

        if (disposing) {
            _instance = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
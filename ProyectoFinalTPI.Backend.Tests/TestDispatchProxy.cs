using System.Reflection;

namespace ProyectoFinalTPI.Backend.Tests;

public class TestDispatchProxy<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?>? _handler;

    public static T Create(Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = DispatchProxy.Create<T, TestDispatchProxy<T>>();
        ((TestDispatchProxy<T>)(object)proxy)._handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return _handler!(targetMethod!, args);
    }
}


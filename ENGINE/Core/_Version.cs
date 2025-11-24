using System;
using System.Reflection;

namespace LARS.ENGINE.Core;

public static class _Version
{
    static _Version Current
        => Assembly.GetExecutingAssembly().GetName().Version!;

    public static string Display
        => Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
           ?? Current.ToString();
}
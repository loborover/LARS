using System;
using System.Reflecion;

namespace LARS.Core;

public static class  LARS_Version
{
    public static Version Current
        => Assembly.GetExecutingAssembly().GetName().Version!;

    public static string Display
        => Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .AssemblyInformationalVersion
            ?? Current.ToString();
}
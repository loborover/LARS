using System;
using System.Reflection;

namespace Engine.Core;

public static class  LARS_Version
{
    public static Version Current
        => Assembly.GetExecutingAssembly().GetName().Version!;

    public static string Display
        => Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
           ?? Current.ToString();
}
using System.Reflection;

namespace SnippyGrab.App.Services;
internal static class BuildVersion
{
    public static string Display => typeof(BuildVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown development version";
}

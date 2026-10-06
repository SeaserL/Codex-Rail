using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal static class SessionPathResolver
{
    public static string Resolve(IReadOnlyList<string>? arguments = null)
    {
        if (arguments is not null)
        {
            for (var index = 0; index < arguments.Count - 1; index++)
            {
                if (arguments[index].Equals("--sessions", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return Normalize(arguments[index + 1]);
                }
            }
        }

        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        }

        return Path.Combine(Normalize(codexHome), "sessions");
    }

    private static string Normalize(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (expanded.Equals("~", StringComparison.Ordinal)
            || expanded.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded.Length == 1 ? string.Empty : expanded[2..]);
        }

        return Path.GetFullPath(expanded);
    }
}

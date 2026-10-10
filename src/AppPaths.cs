using System.Security.Cryptography;
using System.Text;
namespace CodexTokenOverlay;
internal static class AppPaths
{
    internal static bool Development { get; private set; }
    internal static string DataDirectory { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexUsageCardLocal");
    internal static string InstanceSuffix { get; private set; } = "";
    internal static string Name(string name) => @"Local\" + name + InstanceSuffix;
    internal static void Configure(string[] args)
    {
        Development = args.Contains("--dev");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); !Development && directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "codexrail.development"))) Development = true;
        if(Development) { DataDirectory=Path.Combine(AppContext.BaseDirectory,"dev-data"); InstanceSuffix="-dev-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..12]; }
        AssetStore.Root=Path.Combine(DataDirectory,"assets");
    }
}

using System.Reflection;
using BepInEx;
using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// A BepInEx folder of its own for the tests that read or write BepInEx's paths (Paths.ConfigPath,
/// Paths.PluginPath): a new temporary folder each time, deleted with the object. Paths is static,
/// so those tests are in the "BepInEx paths" collection, which runs one test at a time.
/// </summary>
public sealed class TestBepInEx : IDisposable
{
    public TestBepInEx()
    {
        Root = Directory.CreateTempSubdirectory("mwp-tests-").FullName;
        string bepInEx = Path.Combine(Root, "BepInEx");
        Directory.CreateDirectory(Path.Combine(bepInEx, "config"));
        Directory.CreateDirectory(Path.Combine(bepInEx, "plugins"));
        // Set by BepInEx's own start in the game; internal, so reached the way the game's start does.
        typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Path.Combine(Root, "valheim_server.x86_64"), bepInEx, null, null]);
    }

    /// <summary>The game folder; BepInEx/ is in it.</summary>
    public string Root { get; }

    public string Config(string name) => Path.Combine(Paths.ConfigPath, name);

    public string Plugins(string name) => Path.Combine(Paths.PluginPath, name);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

[CollectionDefinition("BepInEx paths", DisableParallelization = true)]
public sealed class BepInExPathsCollection;

using System.Reflection;
using Mono.Cecil;

// Usage: GamePatcher <ServersideQoL.Patchers.dll> <assembly_valheim.dll> <patched assembly_valheim.dll>
if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: GamePatcher <ServersideQoL.Patchers.dll> <assembly_valheim.dll> <output>");
    return 2;
}
// The patcher's own entry point (BepInEx's preloader calls Patch(AssemblyDefinition)).
MethodInfo patch = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("ServersideQoL.Patchers.Patcher", throwOnError: true)!
    .GetMethod("Patch", BindingFlags.Public | BindingFlags.Static)!;
// The game's other DLLs (Unity's) are next to it.
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
using (var game = AssemblyDefinition.ReadAssembly(args[1], new ReaderParameters { InMemory = true, AssemblyResolver = resolver }))
{
    patch.Invoke(null, [game]);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    game.Write(args[2]);
}
Console.WriteLine($"Patched {args[1]} with {Path.GetFileName(args[0])} into {args[2]}.");
return 0;

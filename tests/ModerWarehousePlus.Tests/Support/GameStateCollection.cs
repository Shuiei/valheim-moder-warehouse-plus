using Xunit;

namespace ModerWarehouse.Tests;

/// <summary>
/// The tests that use static state of the game, BepInEx or the plugin: BepInEx's paths, the game's
/// singletons (TestWorld), the warehouse's item names and translations. They run one at a time;
/// the other tests run beside them.
/// </summary>
[CollectionDefinition("Game state", DisableParallelization = true)]
public sealed class GameStateCollection;

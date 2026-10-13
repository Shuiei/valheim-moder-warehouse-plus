using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using ServersideQoL;

namespace ModerWarehouse;

// A fork of Moder Warehouse 2.0.0 by Cris Haani (MIT). It replaces the original, which must not be
// loaded beside it: both would drive the same warehouse signs and chests.
[BepInPlugin(Guid, Name, Version)]
[BepInIncompatibility(OriginalGuid)]
public sealed class WarehousePlugin : ServersideQoLPluginBase<WarehousePlugin, WarehouseConfig>
{
	public const string Guid = "Tie.ModerWarehousePlus";

	public const string Name = "Moder Warehouse Plus";

	public const string Version = "1.0.1";

	// The original mod's id, which names its settings file.
	private const string OriginalGuid = "ch.cris.valheim.moderwarehouse";

	protected override WarehouseConfig CreateConfigSingleton(ConfigFile file, Logger logger)
	{
		CopyOriginalSettings(file, x => logger.LogInfo(x), x => logger.LogWarning(x));
		return new WarehouseConfig(file, logger);
	}

	// First start: the original mod's settings are copied to this mod's file, so a server switching
	// over keeps them; the original file stays, for going back to the original mod. Nothing is copied
	// when the settings are in another file (ServersideQoL's UnifiedConfig). It logs through info and
	// warning rather than the game's logger, which needs the game's clock (and so the game).
	internal static void CopyOriginalSettings(ConfigFile file, Action<string> info, Action<string> warning)
	{
		if (Path.GetFileName(file.ConfigFilePath) != Guid + ".cfg")
		{
			return;
		}
		try
		{
			if (CopyIfMissing(Path.Combine(Paths.ConfigPath, OriginalGuid + ".cfg"), file.ConfigFilePath))
			{
				file.Reload();
				info("Settings copied from " + OriginalGuid + ".cfg to " + Guid + ".cfg.");
			}
		}
		catch (IOException ex)
		{
			warning("Could not copy the settings from " + OriginalGuid + ".cfg: " + ex.Message);
		}
	}

	// Copies source to destination when destination does not exist yet and source does; true when it
	// copied. The source stays where it is.
	internal static bool CopyIfMissing(string source, string destination)
	{
		if (File.Exists(destination) || !File.Exists(source))
		{
			return false;
		}
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		File.Copy(source, destination);
		return true;
	}

	protected override void RegisterProcessors(IProcessorCollection processors)
	{
		processors.Add<WarehouseProcessor>();
	}
}

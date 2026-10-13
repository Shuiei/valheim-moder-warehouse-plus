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

	public const string Version = "1.0.0";

	// The original mod's id, which names its settings file.
	private const string OriginalGuid = "ch.cris.valheim.moderwarehouse";

	protected override WarehouseConfig CreateConfigSingleton(ConfigFile file, Logger logger)
	{
		CopyOriginalSettings(file, logger);
		return new WarehouseConfig(file, logger);
	}

	// First start: the original mod's settings are copied to this mod's file, so a server switching
	// over keeps them. The original file stays, for going back to the original mod.
	private static void CopyOriginalSettings(ConfigFile file, Logger logger)
	{
		string original = Path.Combine(Paths.ConfigPath, OriginalGuid + ".cfg");
		if (Path.GetFileName(file.ConfigFilePath) != Guid + ".cfg" || File.Exists(file.ConfigFilePath) || !File.Exists(original))
		{
			return;
		}
		try
		{
			File.Copy(original, file.ConfigFilePath);
			file.Reload();
			logger.LogInfo("Settings copied from " + OriginalGuid + ".cfg to " + Guid + ".cfg.");
		}
		catch (IOException ex)
		{
			logger.LogWarning("Could not copy the settings from " + OriginalGuid + ".cfg: " + ex.Message);
		}
	}

	protected override void RegisterProcessors(IProcessorCollection processors)
	{
		processors.Add<WarehouseProcessor>();
	}
}

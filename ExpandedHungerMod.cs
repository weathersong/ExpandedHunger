using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using static ExpandedHunger.ExpandedHungerUtils;

namespace ExpandedHunger
{
	public class ExpandedHungerMod : ModSystem
	{
		private const string logHeader = "EXPANDED_HUNGER_1_1_0";
		private const string verString = "1.1.0";
		private const string configFilename = "ExpandedHungerConfig.json";

		private ICoreAPI coreApi;
		private ICoreServerAPI serverApi;
		private ExpandedHungerConfig config;

		private List<KeyValuePair<IServerPlayer, DateTime>> bucketList;

		#region STARTUP

		public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

			this.coreApi = api;
			config = new ExpandedHungerConfig();

			bucketList = new List<KeyValuePair<IServerPlayer, DateTime>>();

			LoadConfig();
        }

        public override void Start(ICoreAPI api)
		{
			base.Start(api);
		}


		public override void StartServerSide(ICoreServerAPI api)
		{
			serverApi = api;

			serverApi.Event.PlayerNowPlaying += Server_OnServerPlayerNowPlaying;
			serverApi.Event.PlayerRespawn += Server_OnPlayerRespawn;
			//serverApi.Event.PlayerDeath += Server_OnPlayerDeath;

			// The tick listener is to handle some kind of problem that BehaviorHunger has, at least in 1.16.5.
			// Trying to manipulate bh.Saturation in the PlayerRespawn event itself has no effect. (It works otherwise, cf commands below.)
			// This seems like some kind of hard-coded behavior, though it's not anywhere in BehaviorEntity's code itself. Server / network?
			// Probably something that happens *after* the PlayerRespawn / EntityRevive events, unfortunately.
			// Not only that! But trying to manipulate Saturation on the very next tick after a respawn also does not work!
			// So the wonky workaround (wonkaround) here is to have a tick listener, *and* wait some amount of extra time. Gah!
			serverApi.Event.RegisterGameTickListener(new Action<float>(Server_OnGameTick), 1000, 0);

			serverApi.RegisterCommand("xh", "Expanded Hunger utility command.", "[gain #|lose #|resetmax|reseteveryone|max #|pukeondeath true/false|drainondeath #|dcp #]", Cmd_Xh);
			serverApi.RegisterCommand("xhl", "Expanded Hunger nutrient levels command.", "[all #|fruit #|vegetable #|grain #|protein #|dairy #|gainall #|loseall #]", Cmd_Xhl);
		}

		#endregion

		#region EVENTS

		private void Server_OnGameTick(float deltaTime)
		{
			for (int k = bucketList.Count-1; k >= 0; k--)
			{
				IServerPlayer player = bucketList[k].Key;
				DateTime time = bucketList[k].Value;
				if ((DateTime.Now - time).TotalSeconds < config.DeathCheckPrecision)
					continue;

				Shim shim = new(player);
				if (!shim.IsValid) { }
				else if (config.PukeOnDeath)
				{
					LogNotif($"Player {player.PlayerName} has respawned. Emptying the contents of their stomach. Poor bastard!");
					// Fow some reason this only works if run twice. ¯\_(ツ)_/¯
					shim.Puke();
					shim.Puke();
					player.SendMessage(0, $"Upon reviving, you find that you are starving!", EnumChatType.OwnMessage);
				}
				else if (config.DrainOnDeath > 0)
				{
					LogNotif($"Player {player.PlayerName} has respawned. Draining Saturation by {config.DrainOnDeath}. Poor bastard!");
					shim.ConsumeSaturation(config.DrainOnDeath);
					shim.ConsumeSaturation(config.DrainOnDeath);
					player.SendMessage(0, $"Upon reviving, you find that you are hungrier.", EnumChatType.OwnMessage);
				}

				bucketList.RemoveAt(k);
			}

		}

		private void Server_OnServerPlayerNowPlaying(IServerPlayer player)
		{
			Shim shim = new(player);

			if (!shim.IsValid)
			{
				LogWarn($"Player {player.PlayerName} has joined. Cannot set MaxSaturation because of a problem retrieving EntityBehaviorHunger!");
			}
			else if (config == null)
			{
				LogWarn($"Player {player.PlayerName} has joined. Cannot set MaxSaturation because Config is invalid!");
			}
			else
			{
				LogNotif($"Player {player.PlayerName} has joined. Setting their MaxSaturation to {config.MaxSaturation}.");
				shim.MaxSaturation = config.MaxSaturation;
			}

		}

		private void Server_OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
		{
			//EntityBehaviorHunger playerHunger = byPlayer.Entity.GetBehavior<EntityBehaviorHunger>();
			//LogNotif($"entityDead = {player.Entity.WatchedAttributes.GetInt("entityDead")}.");
		}

		private void Server_OnPlayerRespawn(IServerPlayer player)
		{
			if (config.PukeOnDeath == true || config.DrainOnDeath > 0)
			{
				KeyValuePair<IServerPlayer, DateTime> pair = new(player, DateTime.MinValue);
				bucketList.Add(pair);
			}

			// This just plain doesn't work. Frustrating. ლ(ಠ益ಠლ)
			//player.SendMessage(0, $"Upon reviving, you find that you are starving!!", EnumChatType.OwnMessage);
			//Shim shim = new(player);
			//shim.Puke();
			//shim.Puke();
		}

		#endregion

		#region COMMANDS

		private void Cmd_Xh(IServerPlayer player, int groupId, CmdArgs args)
		{
			// This is just a simple enforcement instead of having custom and specific privileges, for now.
			if (player.Role.PrivilegeLevel < config.CommandPrivilege)
			{
				player.SendMessage(groupId, $"Sorry, you have insufficient privileges to run this command!", EnumChatType.CommandError);
				return;
			}

			Shim shim = new(player);
			if (!shim.IsValid)
			{
				player.SendMessage(groupId, $"Invalid player or EntityBehaviorHunger for player is null!", EnumChatType.CommandError);
				return;
			}

			string configmax = config.MaxSaturation.ToString("N0");
			string verb = args.PopWord() ?? "";
			verb = verb.ToUpper();
			float? val = null;
			if (verb != "PUKEONDEATH")
				val = args.PopFloat();
			string response;

			switch (verb)
			{
				case "RESETMAX":
					response = $"Okay, MaxSaturation reset to {configmax}. This may not show up immediately in the game.";
					shim.MaxSaturation = config.MaxSaturation;
					break;

				case "RESETEVERYONE":
					player.SendMessage(groupId, $"Okay. Querying all online players, stand by...", EnumChatType.CommandSuccess);
					foreach (IPlayer p in serverApi.World.AllOnlinePlayers)
					{
						if (p != null && p is IServerPlayer sp)
						{
							Shim spshim = new(sp);
							if (spshim.IsValid)
							{
								spshim.MaxSaturation = config.MaxSaturation;
								player.SendMessage(groupId, $"{sp.PlayerName} :: MaxSaturation reset.", EnumChatType.CommandSuccess);
							}
						}
					}
					player.SendMessage(groupId, $"ResetMax complete!", EnumChatType.CommandSuccess);
					return;

				case "MAX":
					if (val.HasValue)
					{
						config.MaxSaturation = val.Value;
						shim.MaxSaturation = config.MaxSaturation;
						response = $"Okay, MaxSaturation set to {shim.MaxSaturationString}. This may not show up immediately in the game.";
					}
					else
						response = $"MaxSaturation is configured to be {configmax}. Player's current MaxSaturation is {shim.MaxSaturationString}.";
					break;

				case "GAIN":
					if (val.HasValue)
					{
						shim.AddSaturation(val.Value);
						response = $"Okay. Player's Saturation is now {shim.SaturationString}. Use /XHL to check nutrient levels.";
					}
					else
						response = $"Player's current Saturation is {shim.SaturationString}.";
					break;

				case "LOSE":
					if (val.HasValue)
					{
						shim.ConsumeSaturation(val.Value);
						response = $"Okay. Player's Saturation is now {shim.SaturationString}. Use /XHL to check nutrient levels.";
					}
					else
						response = $"Player's current Saturation is {shim.SaturationString}.";
					break;

				case "PUKE":
					shim.Puke();
					response = $"Draining all Saturation.";
					break;

				case "PUKEONDEATH":
					bool? pod = args.PopBool();
					if (pod.HasValue)
					{
						config.PukeOnDeath = pod.Value;
						response = $"Okay, PukeOnDeath is now {config.PukeOnDeath}.";
					}
					else
						response = $"PukeOnDeath is currently set {config.PukeOnDeath}.";
					break;

				case "DRAINONDEATH":
					if (val.HasValue)
					{
						config.DrainOnDeath = val.Value;
						response = $"Okay, DrainOnDeath is now {config.DrainOnDeath:#0.0}." +
							(config.PukeOnDeath ? $" However, PukeOnDeath is TRUE, so DrainOnDeath will be disregarded!" : $"");
					}
					else
						response = $"DrainOnDeath is currently set to {config.DrainOnDeath:#0.0}.";
					break;

				case "DCP":
					if (val.HasValue)
					{
						config.DeathCheckPrecision = val.Value;
						response = $"Okay, DeathCheckPrecision is now {config.DeathCheckPrecision:#0.0}.";
					}
					else
						response = $"DeathCheckPrecision is currently set to {config.DeathCheckPrecision:#0.0}.";
					break;

				default:
					response = $"Current configuration for Expanded Hunger v{verString}:\n" +
						$"MaxSaturation = {config.MaxSaturation:N1}\n" +
						$"PukeOnDeath = {config.PukeOnDeath}\n" +
						$"DrainOnDeath = {config.DrainOnDeath:N1}\n" +
						$"DeathCheckPrecision = {config.DeathCheckPrecision:#0.0}\n" +
						$"CommandPrivilege = {config.CommandPrivilege:N0}";
					break;
			}

			coreApi.StoreModConfig(config, configFilename);
			player.SendMessage(groupId, response, EnumChatType.CommandSuccess);

		}

		private void Cmd_Xhl(IServerPlayer player, int groupId, CmdArgs args)
		{
			if (player.Role.PrivilegeLevel < config.CommandPrivilege)
			{
				player.SendMessage(groupId, $"Sorry, you have insufficient privileges to run this command!", EnumChatType.CommandError);
				return;
			}

			Shim shim = new(player);
			if (!shim.IsValid)
			{
				player.SendMessage(groupId, $"Invalid player or EntityBehaviorHunger for player is null!", EnumChatType.CommandError);
				return;
			}

			string response = "";

			string verb = args.PopWord() ?? "";
			verb = verb.ToUpper();
			float? val = args.PopFloat();

			switch (verb)
			{
				case "":
					// will output the report, below
					break;

				case string _ when "ALL".StartsWith(verb):
					if (val.HasValue)
					{
						foreach (HungerTypes ht in EachHungerType)
							shim[ht] = val.Value;
					}
					break;
				case string _ when "GAINALL".StartsWith(verb):
					if (val.HasValue)
					{
						foreach (HungerTypes ht in EachHungerType)
							shim[ht] += val.Value;
					}
					break;
				case string _ when "LOSEALL".StartsWith(verb):
					if (val.HasValue)
					{
						foreach (HungerTypes ht in EachHungerType)
							shim[ht] -= val.Value;
					}
					break;

				case string _ when "FRUIT".StartsWith(verb):
					if (val.HasValue)
						shim[HungerTypes.Fruit] = val.Value;
					response = $"Fruit = {shim.Level(HungerTypes.Fruit)} / {shim.MaxSaturationString}";
					break;
				case string _ when "VEGETABLE".StartsWith(verb):
					if (val.HasValue)
						shim[HungerTypes.Vegetable] = val.Value;
					response = $"Vegetable = {shim.Level(HungerTypes.Vegetable)} / {shim.MaxSaturationString}";
					break;
				case string _ when "GRAIN".StartsWith(verb):
					if (val.HasValue)
						shim[HungerTypes.Grain] = val.Value;
					response = $"Grain = {shim.Level(HungerTypes.Grain)} / {shim.MaxSaturationString}";
					break;
				case string _ when "PROTEIN".StartsWith(verb):
					if (val.HasValue)
						shim[HungerTypes.Protein] = val.Value;
					response = $"Protein = {shim.Level(HungerTypes.Protein)} / {shim.MaxSaturationString}";
					break;
				case string _ when "DAIRY".StartsWith(verb):
					if (val.HasValue)
						shim[HungerTypes.Dairy] = val.Value;
					response = $"Dairy = {shim.Level(HungerTypes.Dairy)} / {shim.MaxSaturationString}";
					break;

				default:
					player.SendMessage(groupId, $"The saturation type '{verb}' isn't recognized.", EnumChatType.CommandError);
					return;

			}

			if (response == "")
				response = $"Current saturation levels:\n" +
					$"Saturation {shim.SaturationString} / {shim.MaxSaturationString}\n" +
					$"Fruit {shim.Level(HungerTypes.Fruit)}\n" +
					$"Vegetable {shim.Level(HungerTypes.Vegetable)}\n" +
					$"Grain {shim.Level(HungerTypes.Grain)}\n" +
					$"Protein {shim.Level(HungerTypes.Protein)}\n" +
					$"Dairy {shim.Level(HungerTypes.Dairy)}";

			player.SendMessage(groupId, response, EnumChatType.CommandSuccess);
		}

		#endregion

		#region UTILS

		private void LoadConfig()
		{
			try
			{
				config = coreApi.LoadModConfig<ExpandedHungerConfig>(configFilename);
				if (config == null)
				{
					LogNotif("No config file found. Using defaults, and creating a default config file.");
					config = DefaultConfig();
					coreApi.StoreModConfig(config, configFilename);
				}
				else
				{
					// Extra sanity checks / warnings on particular values:
					// ...
					LogNotif("Config loaded.");
					// In case this was an old version of the config, store again anyway so that it's updated.
					coreApi.StoreModConfig(config, configFilename);
				}
			}
			catch (Exception ex)
			{
				LogError($"Problem loading the mod's config file, using defaults. Check the config file for typos! Error details: {ex.Message}");
				config = DefaultConfig();
			}
		}

		private ExpandedHungerConfig DefaultConfig()
		{
			ExpandedHungerConfig defaultConfig = new();
			defaultConfig.ResetToDefaults();

			return defaultConfig;
		}

		private void LogNotif(string msg)
        {
			coreApi?.Logger.Notification($"[{logHeader}] {msg}");
        }

		private void LogWarn(string msg)
		{
			coreApi?.Logger.Warning($"[{logHeader}] {msg}");
		}

		private void LogError(string msg)
        {
			coreApi?.Logger.Error($"[{logHeader}] {msg}");
        }

		#endregion

	}
}

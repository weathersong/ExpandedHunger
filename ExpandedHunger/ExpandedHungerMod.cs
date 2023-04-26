using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using static ExpandedHunger.ExpandedHungerUtils;

namespace ExpandedHunger
{
	public class ExpandedHungerMod : ModSystem
	{
		private const string logHeader = "EXPANDED_HUNGER_1_2_0";
		private const string verString = "1.2.0";
		private const string configFilename = "ExpandedHungerConfig.json";

		private ICoreAPI coreApi;
		private ICoreServerAPI serverApi;
		private ExpandedHungerConfig config;
		private long pukeListener;
		private long pulseListener;

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
			pukeListener = serverApi.Event.RegisterGameTickListener(new Action<float>(Server_OnGameTick_PukeCheck), 1000, 0);

			// This is a separate listener for the "reset pulse," which is optional / experimental.
			if (config != null && config.PulseReset && config.PulseResetIntervalMs > 0)
			{
				pulseListener = serverApi.Event.RegisterGameTickListener(new Action<float>(Server_OnGameTick_ResetPulse), config.PulseResetIntervalMs, 15000);
			}

			serverApi.RegisterCommand("xh", "Expanded Hunger utility command.", "[gain #|lose #|set #|resetmax|reseteveryone|max #|pukeondeath true/false|drainondeath #|dcp #|pulsereset true/false|pri #]", Cmd_Xh, Privilege.controlserver);
			// 1.18
			//serverApi.ChatCommands.
			//	Create("xh").
			//	WithDescription("Expanded Hunger utility command.").
			//	WithArgs( ? ).
			//	RequiresPrivilege(Privilege.chat).
			//	HandleWith(Do_Xh);

			serverApi.RegisterCommand("xhl", "Expanded Hunger nutrient levels command.", "[all #|fruit #|vegetable #|grain #|protein #|dairy #|gainall #|loseall #]", Cmd_Xhl, Privilege.controlserver);
			// 1.18
			//serverApi.ChatCommands.
			//	Create("xhl").
			//	WithDescription("Expanded Hunger nutrient levels command.").
			//	RequiresPrivilege(Privilege.chat).
			//	HandleWith(Do_Xhl);

		}

		#endregion

		#region EVENTS

		private void Server_OnGameTick_PukeCheck(float deltaTime)
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

					// This is a safeguard in case Puke (ConsumeSaturation) isn't working for some reason. Temporal Storms seem to act weird, for example.
					if (shim.Saturation < 10)
						bucketList.RemoveAt(k);
					else
						shim.Saturation = 0; // and we'll try again next tick
				}
				else if (config.DrainOnDeath > 0)
				{
					LogNotif($"Player {player.PlayerName} has respawned. Draining Saturation by {config.DrainOnDeath}. Poor bastard!");
					shim.ConsumeSaturation(config.DrainOnDeath);
					shim.ConsumeSaturation(config.DrainOnDeath);
					// No safeguard on this one. Drain isn't working like I want it to anyway, this whole function needs revising.
				}


			}

		}

		private void Server_OnGameTick_ResetPulse(float deltaTime)
		{
			ResetEveryoneMaxSaturation(serverApi, config.MaxSaturation);
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

				if (config.PukeOnDeath)
					player.SendMessage(0, $"Upon reviving, you find that you are starving!", EnumChatType.OwnMessage);
				else
					player.SendMessage(0, $"Upon reviving, you find that you are hungrier.", EnumChatType.OwnMessage);
			}

			// This just plain doesn't work. Frustrating. ლ(ಠ益ಠლ)
			//player.SendMessage(0, $"Upon reviving, you find that you are starving!!", EnumChatType.OwnMessage);
			//Shim shim = new(player);
			//shim.Puke();
			//shim.Puke();
		}

		#endregion

		#region COMMANDS

		// 1.18 version
		private TextCommandResult Do_Xh(TextCommandCallingArgs args)
		{
			throw new NotImplementedException();
		}

		// 1.18 version
		private TextCommandResult Do_Xhl(TextCommandCallingArgs args)
		{
			throw new NotImplementedException();
		}

		// <= 1.17 API version
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

			float? fval = null;
			int? ival = null;
			bool? toggle = null;
			if (verb == "PUKEONDEATH" || verb == "PULSERESET")
				toggle = args.PopBool();
			else if (verb == "PRI")
				ival = args.PopInt();
			else
				fval = args.PopFloat();

			string response;

			switch (verb)
			{
				case "RESETMAX":
					response = $"Okay, MaxSaturation reset to {configmax}. This may not show up immediately in the game.";
					shim.MaxSaturation = config.MaxSaturation;
					break;

				case "RESETEVERYONE":
					ResetEveryoneMaxSaturation(serverApi, config.MaxSaturation, player, groupId);
					return;

				case "MAX":
					if (fval.HasValue)
					{
						config.MaxSaturation = fval.Value;
						shim.MaxSaturation = config.MaxSaturation;
						response = $"Okay, MaxSaturation set to {shim.MaxSaturationString}. This may not show up immediately in the game.";
					}
					else
						response = $"MaxSaturation is configured to be {configmax}. Player's current MaxSaturation is {shim.MaxSaturationString}.";
					break;

				case "GAIN":
					if (fval.HasValue)
					{
						shim.AddSaturation(fval.Value);
						response = $"Okay. Player's Saturation after GAIN is now {shim.SaturationString}. Use /XHL to check nutrient levels.";
					}
					else
						response = $"Player's current Saturation is {shim.SaturationString}.";
					break;

				case "LOSE":
					if (fval.HasValue)
					{
						shim.ConsumeSaturation(fval.Value);
						response = $"Okay. Player's Saturation after LOSE is now {shim.SaturationString}. Use /XHL to check nutrient levels.";
					}
					else
						response = $"Player's current Saturation is {shim.SaturationString}.";
					break;

				case "SET":
					if (fval.HasValue)
					{
						shim.Saturation = fval.Value;
						response = $"Okay. Player's Saturation after SET is now {shim.SaturationString}. Use /XHL to check nutrient levels.";
					}
					else
						response = $"Player's current Saturation is {shim.SaturationString}.";
					break;

				case "PUKE":
					shim.Puke();
					response = $"Attempting to drain all Saturation Saturation is not {shim.SaturationString}.";
					break;

				case "PUKEONDEATH":
					if (toggle.HasValue)
					{
						config.PukeOnDeath = toggle.Value;
						response = $"Okay, PukeOnDeath is now {config.PukeOnDeath}.";
					}
					else
						response = $"PukeOnDeath is currently set {config.PukeOnDeath}.";
					break;

				case "DRAINONDEATH":
					if (fval.HasValue)
					{
						config.DrainOnDeath = fval.Value;
						response = $"Okay, DrainOnDeath is now {config.DrainOnDeath:#0.0}." +
							(config.PukeOnDeath ? $" However, PukeOnDeath is TRUE, so DrainOnDeath will be disregarded!" : $"");
					}
					else
						response = $"DrainOnDeath is currently set to {config.DrainOnDeath:#0.0}.";
					break;

				case "DCP":
					if (fval.HasValue)
					{
						config.DeathCheckPrecision = fval.Value;
						response = $"Okay, DeathCheckPrecision is now {config.DeathCheckPrecision:#0.0}.";
					}
					else
						response = $"DeathCheckPrecision is currently set to {config.DeathCheckPrecision:#0.0}.";
					break;

				case "PULSERESET":
					if (toggle.HasValue)
					{
						if (toggle.Value && config.PulseResetIntervalMs <= 0)
						{
							response = $"Cannot enable PulseReset: PulseResetIntervalMs must be non-zero. Set that value first.";
							break;
						}
						config.PulseReset = toggle.Value;
						response = $"Okay, PulseReset is now {config.PukeOnDeath}. ";
						if (config.PulseReset)
						{
							if (config.PulseResetIntervalMs < 1000)
								response += $" WARNING! PulseResetIntervalMs is {config.PulseResetIntervalMs} milliseconds. Use caution with low values for a tick listener! ";
							if (pulseListener != 0)
							{
								response += $"Unregistering existing tick listener {pulseListener} first. ";
								serverApi.Event.UnregisterGameTickListener(pulseListener);
							}
							pulseListener = serverApi.Event.RegisterGameTickListener(new Action<float>(Server_OnGameTick_ResetPulse), config.PulseResetIntervalMs);
							response += $"Registered tick listener {pulseListener}.";
						}
						else
						{
							if (pulseListener != 0)
							{
								response += $"Unregistering tick listener {pulseListener} now.";
								serverApi.Event.UnregisterGameTickListener(pulseListener);
							}
							pulseListener = 0;
						}
					}
					else
						response = $"PulseReset is currently set {config.PulseReset}.";
					break;

				case "PRI":
					if (ival.HasValue)
					{
						config.PulseResetIntervalMs = ival.Value;
						response = $"Okay, PulseResetIntervalMs is now {config.PulseResetIntervalMs:N0}.";
					}
					else
						response = $"PulseResetIntervalMs is currently set to {config.PulseResetIntervalMs:N0}.";
					break;

				default:
					response = $"Current configuration for Expanded Hunger v{verString}:\n"
						+ $"MaxSaturation = {config.MaxSaturation:N1}\n"
						+ $"PukeOnDeath = {config.PukeOnDeath}\n"
						+ $"DrainOnDeath = {config.DrainOnDeath:N1}\n"
						+ $"DeathCheckPrecision = {config.DeathCheckPrecision:#0.0}\n"
						+ $"PulseReset = {config.PulseReset}" + (config.PulseReset ? $" (Listener {pulseListener})\n" : "\n")
						+ $"PulseResetIntervalMs = {config.PulseResetIntervalMs:N0}\n"
						+ $"CommandPrivilege = {config.CommandPrivilege:N0}";
					break;
			}

			coreApi.StoreModConfig(config, configFilename);
			player.SendMessage(groupId, response, EnumChatType.CommandSuccess);

		}

		// <= 1.17 API version
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

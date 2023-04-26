# ExpandedHunger
This is a mod for the game Vintage Story (https://www.vintagestory.at/) that has one basic function and then a few extra tweaks besides.

The major intent of the mod is to increase the value of MaxSaturation for the player(s), to any configurable number. By default MaxSaturation is 1500. This is generally fine with a default world config, but for worlds (and servers) that have time slowed down and, consequently, playerHungerSpeed reduced, the default of 1500 becomes problematic; mostly because by default many of the game's meals (or those added by mods, such as the exceptional Expanded Foods) fill the player up completely, even with just a single serving. It felt weird to me to only be eating once every other day, so I made this mod to be able to make a balance between playerHungerSpeed and MaxSaturation, so that I eat once or twice a day, without having to customize how many calories meals yield. By the same token, I can eat a variety of tasty meals in just one day, which makes cooking rather more engaging!

Another possible use for the mod would be to vastly increase MaxSaturation in order to simulate more realistic starvation. A human (granted, Seraphs may be different!) can go without food for about a week (sometimes several), before death is any real risk. With the defaults in Vintage Story, you'll be taking damage after just a few in-game hours.

ExpandedHunger (XH) can be configured with a mod config, ExpandedHungerConfig.json in the game's ModConfig folder, but also through in-game commands that then save back to the mod config. The main configuration value is MaxSaturation, which defaults to 3000. You can set it to whatever you like. A few other configurable values - and extra features of the mod - are documented below.

XH is a "young" mod and I am still somewhat inexperienced with Vintage Story modding. I have done my best to test the mod in single player and multi player (but only here on my own LAN), but it's very possible it has some bugs, and it most definitely has some Funny Behavior because of how the game itself works. Please consider this mod as beta!

# Installation
XH is a server side mod. It doesn't need to be on the client at all. In fact since you're here looking at the source and reading this tediously long readme, you can even load it up in Visual Studio (make sure to make the game's exe the startup project since this is a NET Standard 2.0 project), Debug, Open to LAN, make changes, and Hot Reload with players connected!

# Features and Configuration
XH 1.2 has these configurable options:

**MaxSaturation**: The mod's main function. When XH is first installed this defaults to 3000. It can be configured with */xh max*.<br>
**PukeOnDeath**: True or False, defaults to False. When True, a player loses all saturation (is immediately starving) upon death and respawn. This is an experimental feature.<br>
**DrainOnDeath**: An alternative to PukeOnDeath, this drains a specific amount of saturation instead of all of it. PukeOnDeath must be False for this feature to work. This is a VERY experimental feature and is not yet completely implemented; right now the player will reset to the game's default of half-max-saturation on respawn, and then DrainOnDeath applies to *that* value. It will take some engineering (maybe more than is worthwhile) to save what the player's saturation was immediately before death and drain from that value instead.<br>
**DeathCheckPrecision**: With either the PukeOnDeath or DrainOnDeath feature enabled, this is how many whole seconds after the player respawns before the effect is applied. (Why not immediately on respawn? See Technical Details below.) This defaults to 2, so about two seconds after the player respawns, their saturation will be modified.<br>
**CommandPrivilege**: This is the required PrivilegeLevel needed to run the /xh and /xhl commands (below). Unlike the other configs here, this can *only* be set in the config file; that is, it requires direct access to the server. It defaults to 99,999, which is op (by default). serverconfig.json defines roles and their privilege levels.

XH also has commands that help you to see useful hunger-related information and make changes to the mod's config while the game is running, without the need to restart the game or world. It's a bunch of different functions so I decided to split them out into these two commands. Note that when *the player* is mentioned, it is the player running the command.

*/xh*: If used by itself, shows you the current mod configuration. It also has these functions:<br>
*/xh gain #*: Add # saturation to the player.<br>
*/xh lose #*: Remove # saturation from the player. Note that this *also* affects nutrient levels (gain does not), but only by a fractional amount, because of how the game's own hunger system works.<br>
*/xh resetmax*: Reset the player's MaxSaturation to the mod's configured MaxSaturation. This can be useful for some Funny Behaviors.<br>
*/xh reseteveryone*: If running XH on a server and adjusting MaxSaturation, this can be useful to propagate the change to everyone already connected.<br>
*/xh max #*: Set MaxSaturation (and apply it). This is the same as editing the configuration file; the change is saved to the mod config file.<br>
*/xh puke*: Immediately removes all the player's saturation, so that they are starving. This is faster to type than */player playername entity satiety 0*<br>
*/xh pukeondeath ?*: Sets the PukeOnDeath configuration, to true or false.<br>
*/xh drainondeath #*: Sets the DrainOnDeath value.<br>
*/xh dcp #*: Sets the DeathCheckPrecision value. This is in whole seconds, but it can be fractional, e.g. 0.5.<br>

*/xhl*: If used by itself, shows all of the player's nutrient values; Fruit, Vegetable, Grain, Protein, Dairy. It also has these functions:<br>
*/xhl all #*: Sets *all* of the player's nutrient levels to #. This is *not* the same as saturation, instead it affects the player's max heatlh.<br>
*/xhl fruit #*: Sets the player's fruit nutrient level to #. (Note that you can abbreviate 'fruit'. This goes for the rest.)<br>
*/xhl vegetable #*: Sets the player's vegetable nutrient level to #.<br>
*/xhl grain #*: Sets the player's grain nutrient level to #.<br>
*/xhl protein #*: Sets the player's protein nutrient level to #.<br>
*/xhl dairy #*: Sets the player's dairy nutrient level to #.<br>
*/xhl gainall #*: Add # to all of the nutrient levels. Again, this does *not* affect the player's saturation.<br>
*/xhl loseall #*: Removes # from all of the nutrient levels.

# Funny Behaviors (Known Bugs)

Vintage Story exhibits some side-effects when MaxSaturation is changed. In fact, it's possible to change the player entity's Max Saturation strictly through JSON alone, but because of these Funny Behaviors that change by itself isn't sufficient. Hence, why this is a code mod.

The most prominent known bug here is that after waking up from sleeping, your MaxSaturation will appear to have reset back to 1500. Sometimes this "catches up" after just a few seconds. Sometimes if you open your character sheet and close it, it'll catch up. Sometimes eating breakfast jars things loose. If none of the above work or you're just impatient, use the command */xh resetmax*

This reset-to-default behavior shows up some other times too, almost at random. It may (or may not) happens when you die. It happens when you first join the game (single or multi player), but the mod specifically handles that so you shouldn't need to use the command every time you load up the world.

The game also scales all of your nutrient levels to your MaxSaturation, and it does not appear they can be set separately. So unfortunately you cannot have a max Protein of 10,000 but a max Grain of 5,000.

Finally, the game has a hard-coded reset of your saturation (to 50%) whenever you die. The way this behaves is locked up in code that is not documented, and requires some fairly sophisticated (read: hacky) workarounds in order for PukeOnDeath to work, if you're using that feature. Technical details are below; the short version is that when respawning, your saturation may not catch up for a couple seconds.

(Also, while it's not a bug per se, XH is not localization-ready. I know, mea culpa, mea culpa.)

# Technical Details
Under the hood, XH works by loading the Entity Behavior "BehaviorHunger" (https://github.com/anegostudios/vsessentialsmod/blob/master/Entity/Behavior/BehaviorHunger.cs) for the/each player. It is not a Harmony mod.

BehaviorHunger has all the necessary properties and methods exposed to simply change MaxSaturation (and saturation, and nutrient levels) directly through it. Most the mod is really just a shim on top of BehaviorHunger, with a lot of amateur code spent on the /xh and /xhl commands. Plus the extra logic for PukeOnDeath.

PukeOnDeath is where things do get especially "technical" (read: hacky) in XH. I wanted to have some kind of alternative or additional penalty for death in my own games, and losing all saturation seemed like an easy addition to XH. If only it were. I tried many many experiments with setting saturation in the server's PlayerRespawn and PlayerDeath events. They didn't take. It's possible that I could force the issue with something like EnumHandling, but I wanted XH to be as forward-compatible as is reasonable, and play nice with other mods, as much as possible.

So I have XH registered for a game tick listener, every 1 second. When a player respawns, the IServerPlayer and a timestamp are added to an internal list (called bucketList because, you know, Mr. Creosote). Why the timestamp? I first tried just a List<IServerPlayer> and making 'em all puke on every tick, but I found *that* didn't work either! I don't think this is a synchronicity/threading problem, though I haven't found where in the code the death-resets-saturation actually occurs. My best guess is that it's related to the "loss delay" facility of BehaviorHunger, which can also be observed by eating and then immediately after attempting to drain saturation; it fails, and must be done *twice* to push through the effect. (XH handles this for you, with /xh puke.)

You can control the hold-off time (how much time must have passed since the timestamp for that player in the bucketList) with DeathCheckPrecision. Keep in mind that the server tick is only running every 1 second, however. I wasn't sure if particularly high-load servers may need a higher hold-off or not. You can configure it with /xh dcp and that takes effect immediately.

One last technical detail that may be of importance to other developers picking up the source and tinkering with it in Visual Studio. XH uses the .NET Standard 2.0 framework. At least at the time of this writing, debugging your class library with an executable profile will not correctly attach to the Vintagestory.exe process. (Something to do with the guts of devenv and Vintage Story's own framework.) Instead, add Vintagestory.exe itself as an "Existing Project" to your solution. Yes, the exe itself. Make it the Startup Project, set its arguments (in its project properties), and then double check your Visual Studio options > Projects and Solutions > Build and Run, and make sure to un-check "Only build startup projects and dependencies on Run". Et voila!

# Contact
This mod is by @unuroboros (weathersong), usually hanging out on the Vintage Story Discord. Or dfrauzel@pm.me.

# License
None. Do whatever you want with this mod and its source, and no crediting required. If anyone asks, you found it on StackOverflow. Yes the whole thing, wild huh?!

using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using static ExpandedHunger.ExpandedHungerUtils;

namespace ExpandedHunger
{
	internal static class ExpandedHungerUtils
	{

		internal enum HungerTypes
		{
			None,
			Fruit, Vegetable, Grain, Protein, Dairy,
			All
		}

		internal static HungerTypes[] EachHungerType = new HungerTypes[] { HungerTypes.Fruit, HungerTypes.Vegetable, HungerTypes.Grain, HungerTypes.Protein, HungerTypes.Dairy };

		internal static EntityBehaviorHunger GetEBH(IServerPlayer player)
		{
			return player == null ? null : player.Entity == null ? null : player.Entity.GetBehavior<EntityBehaviorHunger>();
		}

		internal static HungerTypes ToHungerType(this string s)
		{
			if (Enum.TryParse(s, true, out HungerTypes result))
				return result;
			return HungerTypes.None;
		}

	}

	internal class Shim
	{
		public bool IsValid => hunger != null;

		public float MaxSaturation { get => GetMaxSaturation(); set => SetMaxSaturation(value); }
		public string MaxSaturationString => GetMaxSaturation().ToString("N0");
		public float Saturation { get => GetSaturation(); }
		public string SaturationString => GetSaturation().ToString("N0");

		public float this[HungerTypes type] { get => GetSaturationLevel(type); set => SetSaturationLevel(type, value); }

		private IServerPlayer player;
		private EntityBehaviorHunger hunger;

		public Shim(IServerPlayer player)
		{
			this.player = player;
			hunger = GetEBH(player);

		}

		private float GetMaxSaturation()
		{
			return hunger != null ? hunger.MaxSaturation : 0;
		}

		//private void SetMaxSaturation(IServerPlayer byPlayer)
		//{
		//	SetMaxSaturation(byPlayer, config.MaxSaturation);
		//}

		private void SetMaxSaturation(float max)
		{
			if (hunger != null)
				hunger.MaxSaturation = max;
		}

		private float GetSaturation()
		{
			return hunger != null ? hunger.Saturation : 0;
		}

		private float GetSaturationLevel(HungerTypes type)
		{
			if (hunger == null)
				return 0;

			switch (type)
			{
				case HungerTypes.Fruit:
					return hunger.FruitLevel;
				case HungerTypes.Vegetable:
					return hunger.VegetableLevel;
				case HungerTypes.Grain:
					return hunger.GrainLevel;
				case HungerTypes.Protein:
					return hunger.ProteinLevel;
				case HungerTypes.Dairy:
					return hunger.DairyLevel;
				default:
					return 0;
			}
		}

		public string Level(HungerTypes type)
		{
			if (hunger == null)
				return "?";

			string format = "N1";
			switch (type)
			{
				case HungerTypes.Fruit:
					return GetSaturationLevel(HungerTypes.Fruit).ToString(format);
				case HungerTypes.Vegetable:
					return GetSaturationLevel(HungerTypes.Vegetable).ToString(format);
				case HungerTypes.Grain:
					return GetSaturationLevel(HungerTypes.Grain).ToString(format);
				case HungerTypes.Protein:
					return GetSaturationLevel(HungerTypes.Protein).ToString(format);
				case HungerTypes.Dairy:
					return GetSaturationLevel(HungerTypes.Dairy).ToString(format);

				case HungerTypes.None:
				default:
					return "?";
			}
		}

		private void SetSaturationLevel(HungerTypes type, float value)
		{
			if (hunger == null)
				return;

			switch (type)
			{
				case HungerTypes.Fruit:
					hunger.FruitLevel = value;
					break;
				case HungerTypes.Vegetable:
					hunger.VegetableLevel = value;
					break;
				case HungerTypes.Grain:
					hunger.GrainLevel = value;
					break;
				case HungerTypes.Protein:
					hunger.ProteinLevel = value;
					break;
				case HungerTypes.Dairy:
					hunger.DairyLevel = value;
					break;

				case HungerTypes.All:
					break;
			}
		}

		public void ConsumeSaturation(float amount)
		{
			if (hunger == null)
				return;

			hunger.ConsumeSaturation(amount);
			player.Entity.WatchedAttributes.MarkPathDirty("hunger"); // probably not needed, just in case.
		}

		public void AddSaturation(float amount)
		{
			if (hunger == null)
				return;

			hunger.Saturation += amount;
		}

		/// <summary>
		/// All Saturation is drained. This has the same effect on nutrient levels as if saturation had drained over time.
		/// That is, nutrient levels will NOT be set to 0. Use the this[] setters for that.
		/// </summary>
		public void Puke()
		{
			if (Saturation < 3)
				return;

			// Running this twice seems to force the effect. Otherwise it can by blocked by the "lossdelaygain" function of BehaviorHunger.
			// You can observe this by (commenting out the first one and) running this normally, then trying right after the player eats.
			ConsumeSaturation(1);
			ConsumeSaturation(Saturation - 1);
		}

	}

}

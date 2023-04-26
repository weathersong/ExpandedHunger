
namespace ExpandedHunger
{
    internal class ExpandedHungerConfig
    {
		public float MaxSaturation = 3000f;
		public bool PukeOnDeath = false;
		public float DrainOnDeath = 0f;
		public float DeathCheckPrecision = 2f;
		public bool PulseReset = false;
		public int PulseResetIntervalMs = 3000;
		public int CommandPrivilege = 99999;

		public ExpandedHungerConfig()
		{

		}

		public void ResetToDefaults()
		{
			MaxSaturation = 3000f;
			PukeOnDeath = false;
			DrainOnDeath = 0f;
			DeathCheckPrecision = 2f;
			PulseReset = false;
			PulseResetIntervalMs = 3000;
			CommandPrivilege = 99999;
		}
	}

}


namespace ExpandedHunger
{
    internal class ExpandedHungerConfig
    {
        public float MaxSaturation;
		public bool PukeOnDeath = true;
		public float DrainOnDeath = 0f;
		public float DeathCheckPrecision = 2f;

		public ExpandedHungerConfig()
		{

		}

		public void ResetToDefaults()
		{
			MaxSaturation = 3000f;
			PukeOnDeath = true;
			DrainOnDeath = 0f;
			DeathCheckPrecision = 2f;
		}
	}

}

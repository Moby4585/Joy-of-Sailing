using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sailboat
{
    class SailboatConfig
    {
        public string windMultiplierText = "The number by which the wind strenght should be multiplied (it goes from 0 to about 1.2 in vanilla)";
        public float windMultiplier = 20f;

        public string hullSpeedText = "The maximum speed at which the sailboat can go.";
        public float hullSpeed = 10f;

        public SailboatConfig()
        { }

        public static SailboatConfig Current { get; set; }

        public static SailboatConfig GetDefault()
        {
            SailboatConfig defaultConfig = new SailboatConfig();

            defaultConfig.windMultiplierText.ToString();
            defaultConfig.windMultiplier = 20f;

            defaultConfig.hullSpeedText.ToString();
            defaultConfig.hullSpeed = 10f;
            return defaultConfig;
        }
    }
}

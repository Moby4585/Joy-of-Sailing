using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace joyofsailing
{
    public class ConfigCommands : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }
    }

    public class JosConfig
    {
        public float minWindSpeed = 0.0f;
        public float sailSpeedMul = 1.0f;
        public float scullSpeedMul = 1.0f;
        public bool enableSculling = true;
    }
}

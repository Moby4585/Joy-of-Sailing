using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using System.Drawing;
using HarmonyLib;

namespace sailboat
{
    public class SailboatMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntity("EntitySailboat", typeof(EntitySailboat));


            api.RegisterMountable("sailboat", EntitySailboatSeat.GetMountable);
            //api.RegisterItemClass("ItemBoat", typeof(ItemBoat));
            //api.RegisterEntity("EntityVehicle", typeof(EntityVehicle));
            /*if (api is ICoreServerAPI sapi)
            { 
                sapi.World.Logger.StoryEvent("kosFireMod loaded");
            }*/
        }
    }
}

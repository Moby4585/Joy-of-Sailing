using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace joyofsailing
{
    public class joyofsailingModSystem : ModSystem
    {
        public static ClientMain main;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.RegisterEntity("EntitySailboat", typeof(EntitySailboat));
            Achievements.AchievementsManager.RegisterAchievement("joyofsailing", "joyofsailing.setsail", "joyofsailing:sailboat-oak");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("Hello from template mod server side: " + Lang.Get("joyofsailing2:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("joyofsailing2:hello"));

            main = api.World as ClientMain;


        }
    }
}

using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using static HarmonyLib.Code;
using static System.Formats.Asn1.AsnWriter;

namespace joyofsailing
{
    public class EntitySailboat : EntityBoat
    {
        // WORLDCONFIG OPTIONS
        // joyofsailing.minwindspeed : adjusts the minimum wind speed
        // joyofsailing.sailspeedmul : multiplies the speed of all sailboats when sailing
        // joyofsailing.scullspeedmul : multiplies the sculling speed of all sailboats

        float sailLevel = 0f;
        float sailAccuracy = 0f;

        double windSpeed = 0f;
        double windAngle = 0f;

        float sailAngle = 0f;

        float rudderAngle = 0f;

        SailboatAttributes sailAttr;

        EntityBehaviorSelectionBoxes behaviorSelectionBoxes;
        Dictionary<string, int> selBoxId = new Dictionary<string, int>();

        float autoScullTimer = 0f;
        float autoScullEnableTimer = 0f;
        bool isAutoSculling = false;
        bool canDisableAutoSculling = false;

        private float curRotMountAngleZ;

        /*public override void OnEntityLoaded()
        {
            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
        }
        public override void OnEntitySpawn()
        {
            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
        }*/

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            behaviorSelectionBoxes = this.GetBehavior<EntityBehaviorSelectionBoxes>();
            sailAttr = properties.Attributes["sailAttributes"].AsObject<SailboatAttributes>();
        }

        public override void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (!capi.IsGamePaused)
            {
                updateBoatAngleAndMotion(dt);
                long inWorldEllapsedMilliseconds = capi.InWorldEllapsedMilliseconds;
                float num = 0f;
                if (Swimming)
                {
                    double num2 = capi.World.Calendar.SpeedOfTime / 60f;
                    float num3 = 0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f;
                    float num4 = MathF.PI / 360f * num3;
                    mountAngle.X = GameMath.Sin((float)((double)inWorldEllapsedMilliseconds / 1000.0 * 2.0 * num2)) * 8f * num4;
                    mountAngle.Y = GameMath.Cos((float)((double)inWorldEllapsedMilliseconds / 2000.0 * 2.0 * num2)) * 3f * num4;
                    mountAngle.Z = (0f - GameMath.Sin((float)((double)inWorldEllapsedMilliseconds / 3000.0 * 2.0 * num2))) * 8f * num4;
                    curRotMountAngleZ += ((float)AngularVelocity * 5f * (float)Math.Sign(ForwardSpeed) - curRotMountAngleZ) * dt * 5f;
                    num = (0f - (float)ForwardSpeed) * 1.3f * sailAttr.speedPitchMultiplier; // Configurable speed-pitching
                }

                EntityShapeRenderer entityShapeRenderer = base.Properties.Client.Renderer as EntityShapeRenderer;
                if (entityShapeRenderer != null)
                {
                    entityShapeRenderer.xangle = mountAngle.X + curRotMountAngleZ;
                    entityShapeRenderer.yangle = mountAngle.Y;

                    float maxAngle = sailAttr.speedPitchMaximum / 57.2958f; // degrees to radians
                    entityShapeRenderer.zangle = mountAngle.Z + Math.Clamp(num, -maxAngle, maxAngle); // Only change in that method: makes sure the boat doesn't roll back too far when it's going fast
                }
            }
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            Shape shape = entityShape;

            if (shape == entityShape)
            {
                entityShape = entityShape.Clone();
                entityShape.Animations = shape.Animations;
            }

            foreach (SailLevel level in sailAttr.sailLevels)
            {
                if (sailLevel > level.threshold) continue;

                foreach (string element in level.disableElements)
                {
                    entityShape.RemoveElementByName(element);
                }

                foreach (KeyValuePair<string, float> element in level.sailSegmentsRotation)
                {
                    ShapeElement shapeEl = entityShape.GetElementByName(element.Key);
                    if (shapeEl != null)
                    shapeEl.RotationZ = element.Value * sailAccuracy;
                }
                break;
            }

            foreach (string rotatedElement in sailAttr.sailElements)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(rotatedElement);
                if (shapeEl != null)
                    shapeEl.RotationY = sailAngle;
            }

            foreach (string flagElement in sailAttr.flagElements)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(flagElement);
                if (shapeEl != null) shapeEl.RotationY = -(Pos.Yaw * 57.2958f + windAngle) + 180f;
            }

            foreach (KeyValuePair<string, float> element in sailAttr.rudderRotation)
            {
                ShapeElement shapeEl = entityShape.GetElementByName(element.Key);
                if (shapeEl != null)
                {
                    shapeEl.RotationY = rudderAngle * element.Value;
                }
            }

            /*if (Api is ICoreClientAPI capi)
            {
                Vec3d boxPosRight = behaviorSelectionBoxes.GetCenterPosOfBox(22);
                Vec3d boxPosLeft = behaviorSelectionBoxes.GetCenterPosOfBox(23);


                if (boxPosRight != null && boxPosLeft != null)
                {

                    //capi.Render.RenderRectangle((float)boxPosRight.X, (float)boxPosRight.Y, (float)boxPosRight.Z, 1f, 1f, Color.Red.ToArgb());

                    string msg = "";
                    foreach (var atpt in behaviorSelectionBoxes.selectionBoxes)
                    {
                        msg +=  atpt.AttachPoint.Code + " : ";
                    }

                    //capi.ShowChatMessage((boxPosLeft?.ToString() ?? "null") + " : " + (boxPosRight?.ToString() ?? "null"));
                }
            }*/

            base.OnTesselation(ref entityShape, shapePathForLogging);
        }

        protected override void updateBoatAngleAndMotion(float dt)
        {


            updateWind();

            dt = Math.Min(0.5f, dt);
            float physicsFrameTime = GlobalConstants.PhysicsFrameTime;
            this.MarkShapeModified();
            //this.shapeFresh = true;

            bool hasController = false;
            Vec3d controlsVec = SeatsToMotionSail(physicsFrameTime, ref hasController);

            if (!Swimming)
            {
                return;
            }


            //ForwardSpeed += (vec2d.X * (double)SpeedMultiplier - ForwardSpeed) * (double)dt;
            AngularVelocity += (controlsVec.Y * (double)SpeedMultiplier - AngularVelocity) * (double)dt;

            sailLevel += (float)controlsVec.X * 8f * (float)dt;
            sailLevel = Math.Clamp(sailLevel, 0f, 1f);
            sailAngle += (float)controlsVec.Z * 1200f * (float)dt;
            sailAngle = Math.Clamp(sailAngle, -sailAttr.maximumSailAngle, sailAttr.maximumSailAngle);

            rudderAngle += (float)(((-controlsVec.Y / dt) - rudderAngle) * dt * 6f);
            rudderAngle = Math.Clamp(rudderAngle, -1f, 1f);

            if (!hasController)
            {
                sailLevel = 0f;
                WatchedAttributes.SetFloat("josailing.sailLevel", sailLevel);
            }

            float windYaw = ((Pos.Yaw * 57.2958f % 360f + (float)windAngle) + 360f + 180f) % 360f - 180f;

            float windSailDifference = (windYaw + sailAngle + 360f + 180f) % 360f - 180f;

            sailAccuracy = calculateSailPower(windSailDifference);
            //if (Api is ICoreClientAPI capi) capi.ShowChatMessage(sailAccuracy.ToString() + " : " + windYaw.ToString() + " : " + (((windYaw + sailAngle)).ToString()));

            float scullSpeed = (Math.Sign(controlsVec.Y) != Math.Sign(AngularVelocity)) && controlsVec.Y != 0d ? (sailAttr.scullSpeed * World.Config.GetFloat("joyofsailing.scullspeedmul", 1f)) : 0f;

            double desiredSpeed = sailAccuracy
                * Math.Max(Math.Max(windSpeed, sailAttr.minimumWindSpeed), World.Config.GetFloat("joyofsailing.minwindspeed", 0f)) // wind speed
                * sailAttr.windSpeedMultiplier * World.Config.GetFloat("joyofsailing.sailspeedmul", 1f) // wind speed multiplier
                * sailLevel
                + (scullSpeed);

            ForwardSpeed += (desiredSpeed - ForwardSpeed) * (double)dt;


            EntityPos sidedPos = base.SidedPos;
            if (ForwardSpeed != 0.0)
            {
                Vec3d vec3d = sidedPos.GetViewVector().Mul((float)(0.0 - ForwardSpeed)).ToVec3d();
                sidedPos.Motion.X = vec3d.X;
                sidedPos.Motion.Z = vec3d.Z;
            }

            EntityBehaviorPassivePhysicsMultiBox behavior = GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();
            bool flag = true;
            if (AngularVelocity != 0.0)
            {
                float num = (float)AngularVelocity * dt * 30f;
                if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw + num))
                {
                    sidedPos.Yaw += num;
                }
                else
                {
                    flag = false;
                }
            }
            else
            {
                flag = behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw);
            }

            if (!flag)
            {
                if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw - 0.1f))
                {
                    sidedPos.Yaw -= 0.0002f;
                }
                else if (behavior.AdjustCollisionBoxesToYaw(dt, push: true, base.SidedPos.Yaw + 0.1f))
                {
                    sidedPos.Yaw += 0.0002f;
                }
            }

            sidedPos.Roll = 0f;

            if (controlsVec != Vec3d.Zero)
            {
                WatchedAttributes.SetFloat("josailing.sailLevel", sailLevel);
                WatchedAttributes.SetFloat("josailing.sailAngle", sailAngle);
            }

            sailLevel = WatchedAttributes.GetFloat("josailing.sailLevel", 0f);
            sailAngle = WatchedAttributes.GetFloat("josailing.sailAngle", 0f);
        }

        public virtual Vec3d SeatsToMotionSail(float dt, ref bool hasController)
        {
            int rowerCount = 0;
            double forwardAxis = 0.0;
            double sideAxis = 0.0;
            double sprintAxis = 0.0;
            EntityBehaviorSeatable behavior = GetBehavior<EntityBehaviorSeatable>();
            behavior.Controller = null;
            IMountableSeat[] seats = behavior.Seats;
            for (int i = 0; i < seats.Length; i++)
            {
                EntityBoatSeat entityBoatSeat = seats[i] as EntityBoatSeat;
                if (entityBoatSeat.Passenger == null)
                {
                    continue;
                }

                if (!(entityBoatSeat.Passenger is EntityPlayer))
                {
                    entityBoatSeat.Passenger.SidedPos.Yaw = base.SidedPos.Yaw;
                }

                if (entityBoatSeat.Config.BodyYawLimit.HasValue)
                {
                    EntityPlayer entityPlayer = entityBoatSeat.Passenger as EntityPlayer;
                    if (entityPlayer != null)
                    {
                        entityPlayer.BodyYawLimits = new AngleConstraint(Pos.Yaw + entityBoatSeat.Config.MountRotation.Y * (MathF.PI / 180f), entityBoatSeat.Config.BodyYawLimit.Value);
                        entityPlayer.HeadYawLimits = new AngleConstraint(Pos.Yaw + entityBoatSeat.Config.MountRotation.Y * (MathF.PI / 180f), MathF.PI / 2f);
                    }
                }

                if (!entityBoatSeat.Config.Controllable || behavior.Controller != null)
                {
                    continue;
                }

                hasController = true;

                EntityControls controls = entityBoatSeat.controls;
                behavior.Controller = entityBoatSeat.Passenger;
                if (!HasPaddle(entityBoatSeat.Passenger))
                {
                    entityBoatSeat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
                    entityBoatSeat.actionAnim = null;
                    continue;
                }

                if (controls.Left == controls.Right)
                {
                    StopAnimation("turnLeft");
                    StopAnimation("turnRight");
                }

                if (controls.Left && !controls.Right)
                {
                    StartAnimation("turnLeft");
                    StopAnimation("turnRight");
                }

                if (controls.Right && !controls.Left)
                {
                    StopAnimation("turnLeft");
                    StartAnimation("turnRight");
                }

                // Auto sculling handling
                if (controls.Left && controls.Right)
                {
                    autoScullEnableTimer += dt;
                    if (autoScullEnableTimer > 1f)
                    {
                        isAutoSculling = true;
                        canDisableAutoSculling = false;
                        autoScullEnableTimer = 0f;
                    }
                }
                else
                {
                    autoScullEnableTimer = 0f;
                }

                if (!controls.Left && !controls.Right)
                {
                    canDisableAutoSculling = true;
                }

                if (!controls.TriesToMove && !(controls.Sprint || controls.Jump))
                {
                    entityBoatSeat.actionAnim = null;
                    if (entityBoatSeat.Passenger.AnimManager != null && !entityBoatSeat.Passenger.AnimManager.IsAnimationActive(MountAnimations["ready"]))
                    {
                        entityBoatSeat.Passenger.AnimManager.StartAnimation(MountAnimations["ready"]);
                    }

                    continue;
                }

                if (controls.Right && !controls.Backward && !controls.Forward)
                {
                    entityBoatSeat.actionAnim = MountAnimations["backwards"];
                }
                else
                {
                    entityBoatSeat.actionAnim = MountAnimations[controls.Backward ? "backwards" : "forwards"];
                }

                entityBoatSeat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
                float rowerPower = ((++rowerCount == 1) ? 1f : 0.5f);
                if (controls.Left ^ controls.Right) // for anego: using XOR instead of the vanila OR to avoid double-presses being registered
                {
                    float keyPressed = (controls.Left ? 1 : (-1));
                    sideAxis += (double)(rowerPower * keyPressed * dt);

                    if (canDisableAutoSculling)
                    {
                        isAutoSculling = false;

                    }
                }

                if (controls.Forward ^ controls.Backward)
                {
                    float keyPressed = (controls.Forward ? 1 : (-1));

                    forwardAxis += (double)(rowerPower * keyPressed * dt * 2f);
                }

                if (controls.Sprint ^ controls.Jump)
                {
                    float keyPressed = (controls.Sprint ? 1 : -1);

                    sprintAxis += (double)(rowerPower * keyPressed * dt);
                }
            }

            if (isAutoSculling)
            {
                int scullingSide = (autoScullTimer > 1f) ? 1 : -1;
                sideAxis += (double)(scullingSide * dt / 2f);

                autoScullTimer += dt;
                autoScullTimer = autoScullTimer % 2f;
            }

            return new Vec3d(forwardAxis, sideAxis, sprintAxis);
        }

        public void updateWind()
        {
            Vec3d windVector = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Clone();
            windAngle = Math.Atan2(windVector.Normalize().Z, windVector.Normalize().X) * GameMath.RAD2DEG - 90f;
            windSpeed = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Length();

            //windSpeed = Math.Max(SailboatConfig.Current.minSpeed, windSpeed); // WIND MIN SPEED : A RETIRER SI CA MARCHE PAS
        }

        float calculateSailPower(float sailWindAngle)
        {
            //sailAttr.perfectAngle
            //sailAttr.falloffAngle

            bool shouldUseTackBonus = Math.Sign(sailWindAngle) != Math.Sign(sailAngle) && Math.Abs(sailAngle) >= sailAttr.maximumSailAngle; // Give extra tolerance to make it easier/possible to go against the wind

            //if (Api is ICoreClientAPI capi) capi.ShowChatMessage(sailWindAngle.ToString() + " against " + sailAngle.ToString() + ", sail bonus " + shouldUseTackBonus.ToString());

            float perfectAngle = sailAttr.perfectAngle + (shouldUseTackBonus ? sailAttr.fullyTackedExtraTolerance : 0f);
            float falloffAngle = sailAttr.falloffAngle + (shouldUseTackBonus ? sailAttr.fullyTackedExtraTolerance : 0f);

            if (Math.Abs(sailWindAngle) <= perfectAngle) return 1f;
            if (Math.Abs(sailWindAngle) > falloffAngle) return 0f;
            return 0f + (1f - 0f) * ((Math.Abs(sailWindAngle) - falloffAngle) / (perfectAngle - falloffAngle)); // Remap the value if in the falloff
        }
    }
    public class SailboatAttributes
    {
        public SailLevel[] sailLevels = new SailLevel[0];
        public float windSpeedMultiplier = 1.0f;
        public float minimumWindSpeed = 0.1f;
        public float scullSpeed = 1.0f;

        public float maximumSailAngle = 70f;
        public float perfectAngle = 10f;
        public float falloffAngle = 45f;
        public float fullyTackedExtraTolerance = 20.0f;

        public float speedPitchMultiplier = 1.0f;
        public float speedPitchMaximum = 15f;

        public Dictionary<string, float> rudderRotation = new Dictionary<string, float>();

        public string[] sailElements = new string[0];

        public string[] flagElements = new string[0];

        public SailboatCordage[] cordages = new SailboatCordage[0];
    }

    public class SailLevel
    {
        public float threshold = 0f;
        public string[] disableElements = new string[0];
        public string[] enableElements = new string[0];
        public Dictionary<string, float> sailSegmentsRotation = new Dictionary<string, float>();
    }

    public class SailboatCordage
    {
        public string fixedAPCode = "";
        public float ropeLength = 1.0f;

        public CordageAttachment[] cordageAttachments = new CordageAttachment[0];
    }

    public class  CordageAttachment
    {
        public string apCode = "";
        public float threshold = 0.0f;
    }
}

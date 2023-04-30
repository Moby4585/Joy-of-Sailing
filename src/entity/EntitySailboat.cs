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
using System.IO;

namespace sailboat
{
    public class EntitySailboat : Entity, IRenderer, IMountableSupplier
    {
        public EntitySailboatSeat[] Seats;

        // current forward speed
        public double ForwardSpeed = 0.0;

        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;

        public double SailAngle = 0.0;
        public double DesiredSailAngle = 0.0;
        public double SailLimit = 90.0;

        public float SailLevel = 0f;

        ModSystemBoatingSound modsysSounds;

        WeatherSystemBase weatherSystem;

        public override bool ApplyGravity
        {
            get { return true; }
        }

        public override bool IsInteractable
        {
            get { return true; }
        }


        public override float MaterialDensity
        {
            get { return 100f; }
        }

        public override double SwimmingOffsetY
        {
            get { return 0.45; }
        }

        /// <summary>
        /// The speed this boad can reach at full power
        /// </summary>
        public virtual float SpeedMultiplier => 30f;
        public virtual float RotationSpeedMultiplier => 2f;

        double windAngle = 0f;
        double windSpeed = 0f;

        double realSpeedMultiplier = 0f;

        public double RenderOrder => 0;
        public int RenderRange => 999;

        public IMountable[] MountPoints => Seats;

        public Vec3f[] MountOffsets = new Vec3f[] { new Vec3f(-1.4f, 0.2f, 0), new Vec3f(-0.1f, 0.2f, 0) };

        Shape sailboatShape;
        ICoreClientAPI capi;

        public override string GetInfoText()
        {
            string baseDesc = base.GetInfoText();
            //baseDesc += "\nAngle: " + windAngle.ToString();
            //baseDesc += "\nWind: " + World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).X.ToString() + " ; " + World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Z.ToString();

            /*float trueYaw = ((Pos.Yaw * 57.2958f) % 360f - 180f);
            float sailSpeed = (float)((SailAngle + trueYaw/2f) * (Math.Abs(SailAngle) - Math.Abs(trueYaw) > 1f ? 0f : 1f));
            double sailMultiplier = clamp(0.0, 1.0, Math.Exp(-Math.Abs(sailSpeed / 30f))) * (Math.Abs(SailAngle) - Math.Abs(trueYaw) > 1f ? 0f : 1f);
            baseDesc += "\nSail angle speed:" + sailMultiplier;
            float trueYaw = (Pos.Yaw * 57.2958f + (float)windAngle) % 360f - 180f;
            baseDesc += "\nSpeed: " + (1d + 0.5d * Math.Sin(Math.Abs(trueYaw * GameMath.DEG2RAD))).ToString();

            //baseDesc += "\n" + weatherSystem.getWeatherDataReader().GetWindSpeed(Pos.AsBlockPos.ToVec3d()).ToString();
            //baseDesc += "\n" + ((float)weatherSystem.getWeatherDataReader().GetWindSpeed(Pos.AsBlockPos.ToVec3d()) * (SailLevel / 3f) * SpeedMultiplier).ToString();*/
            return baseDesc;
        }

        public EntitySailboat()
        {
            Seats = new EntitySailboatSeat[2];
            for (int i = 0; i < Seats.Length; i++) Seats[i] = new EntitySailboatSeat(this, i, MountOffsets[i]);
            Seats[0].controllable = true;
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            weatherSystem = api.ModLoader.GetModSystem<WeatherSystemBase>();

            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "boatsim");
                modsysSounds = api.ModLoader.GetModSystem<ModSystemBoatingSound>();
            }

            // The mounted entity will try to mount as well, but at that time, the boat might not have been loaded, so we'll try mounting on both ends. 
            foreach (var seat in Seats)
            {
                if (seat.PassengerEntityIdForInit != 0 && seat.Passenger == null)
                {
                    var entity = api.World.GetEntityById(seat.PassengerEntityIdForInit) as EntityAgent;
                    if (entity != null)
                    {
                        entity.TryMount(seat);
                    }
                }
            }
        }


        public float xangle = 0, yangle = 0, zangle = 0;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Client side we update every frame for smoother turning
            if (capi.IsGamePaused) return;

            updateBoatAngleAndMotion(dt);

            long ellapseMs = capi.InWorldEllapsedMilliseconds;

            if (Swimming)
            {
                float intensity = 0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f;
                float diff = GameMath.DEG2RAD / 2f * intensity;
                xangle = GameMath.Sin((float)(ellapseMs / 1000.0 * 2)) * 8 * diff;
                yangle = GameMath.Cos((float)(ellapseMs / 2000.0 * 2)) * 3 * diff;
                zangle = -GameMath.Sin((float)(ellapseMs / 3000.0 * 2)) * 8 * diff /*- (float)AngularVelocity * 5 * Math.Sign(ForwardSpeed)*/;

                SidedPos.Pitch = 0f; // (float)ForwardSpeed * 1.3f;
            }

            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr == null) return;

            esr.xangle = xangle;
            esr.yangle = yangle;
            esr.zangle = zangle;

            if (sailboatShape != null)
            {
                esr.OverrideEntityShape = sailboatShape.Clone();
            }
            if (esr.OverrideEntityShape != null)
            {
                float yawAdjusted = (Pos.Yaw * 57.2958f + (float)windAngle) % 360f - 180f;
                DesiredSailAngle = clamp(15.0, 80.0, Math.Min(Math.Abs(SailLimit), Math.Abs(yawAdjusted))) * Math.Sign(-yawAdjusted);

                SailAngle += (DesiredSailAngle - SailAngle) * dt * 4f;

                esr.OverrideEntityShape.GetElementByName("sail").RotationZ = SailAngle;

                if (SailLevel == 0f)
                {
                    esr.OverrideEntityShape.RemoveElementByName("sail_stage_1");
                    esr.OverrideEntityShape.RemoveElementByName("sail_stage_2");
                    esr.OverrideEntityShape.RemoveElementByName("sail_stage_3");
                }
                else
                {
                    esr.OverrideEntityShape.RemoveElementByName("sail_folded");
                    if (SailLevel <= 2) esr.OverrideEntityShape.RemoveElementByName("sail_stage_3");
                    if (SailLevel <= 1) esr.OverrideEntityShape.RemoveElementByName("sail_stage_2");
                }
                //double windSpeed = weatherSystem.getWeatherDataReader().GetWindSpeed(Pos.AsBlockPos.ToVec3d());
                updateWind();
                esr.OverrideEntityShape.GetElementByName("flag").RotationY = -(Pos.Yaw * 57.2958f + windAngle) + 180f;
            }
            esr.MarkShapeModified();

            bool selfSitting = false;

            foreach (var seat in Seats)
            {
                selfSitting |= seat.Passenger == capi.World.Player.Entity;
                var pesr = seat.Passenger?.Properties?.Client.Renderer as EntityShapeRenderer;
                if (pesr != null)
                {
                    pesr.xangle = xangle;
                    pesr.yangle = yangle;
                    pesr.zangle = zangle;
                }
            }

            if (selfSitting)
            {
                modsysSounds.NowInMotion((float)Pos.Motion.Length());
            }
            else
            {
                modsysSounds.NotMounted();
            }
        }

        #region test collision
        public void tryCollide()
        {
            /*Cuboidf[] collisionBoxes = { OriginCollisionBox };
            if (collisionBoxes == null) return;
            if (SidedPos.AsBlockPos == null) return;
            if (World?.GetIntersectingEntities(SidedPos?.AsBlockPos, collisionBoxes) == null) return;
            Entity[] intersectingEntities = World?.GetIntersectingEntities(SidedPos?.AsBlockPos, collisionBoxes);

            foreach(Entity entity in intersectingEntities)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    entity.Pos.Y = Pos.Y + CollisionBox.YSize / 2f;
                    entity.OnGround = true;
                }
                else
                {
                    entity.ServerPos.Y = ServerPos.Y + CollisionBox.YSize / 2f;
                    entity.OnGround = true;
                }
            }*/
        }
        #endregion

        public void updateWind()
        {
            Vec3d windVector = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Clone();
            windAngle = Math.Atan2(windVector.Normalize().Z, windVector.Normalize().X) * GameMath.RAD2DEG;
            windSpeed = World.BlockAccessor.GetWindSpeedAt(SidedPos.XYZ).Length();
        }

        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                updateBoatAngleAndMotion(dt);
            }

            updateWind();

            //tryCollide();

            ///*
            #region tests fonctions pour collision
            Cuboidf collision = new Cuboidf((float)SidedPos.X, (float)SidedPos.Y, (float)SidedPos.Z, (float)SidedPos.X + 1f, (float)SidedPos.Y + 1f, (float)SidedPos.Z + 1f);

            World.CollisionTester.CollisionBoxList.Add(collision, (int)SidedPos.X, (int)SidedPos.Y, (int)SidedPos.Z);

            //World.NearestPlayer(1, 1, 1)?.Entity.CollisionBox.Clone();
            //*/
            #endregion

            base.OnGameTick(dt);
        }

        public override void OnAsyncParticleTick(float dt, IAsyncParticleManager manager)
        {
            base.OnAsyncParticleTick(dt, manager);

            double disturbance = Math.Abs(ForwardSpeed) + Math.Abs(AngularVelocity);
            if (disturbance > 0.01)
            {
                float minx = -3f;
                float addx = 6f;
                float minz = -0.75f;
                float addz = 1.5f;

                EntityPos herepos = Pos;
                var rnd = Api.World.Rand;
                SplashParticleProps.AddVelocity.Set((float)herepos.Motion.X * 20, (float)herepos.Motion.Y, (float)herepos.Motion.Z * 20);
                SplashParticleProps.AddPos.Set(0.1f, 0, 0.1f);
                SplashParticleProps.QuantityMul = 0.5f * (float)disturbance;

                double y = herepos.Y - 0.15;

                for (int i = 0; i < 10; i++)
                {
                    float dx = minx + (float)rnd.NextDouble() * addx;
                    float dz = minz + (float)rnd.NextDouble() * addz;

                    double yaw = Pos.Yaw + Math.Atan2(dx, dz);
                    double dist = Math.Sqrt(dx * dx + dz * dz);

                    SplashParticleProps.BasePos.Set(
                        herepos.X + Math.Sin(yaw) * dist,
                        y,
                        herepos.Z + Math.Cos(yaw) * dist
                    );

                    manager.Spawn(SplashParticleProps);
                }
            }

        }

        private void updateBoatAngleAndMotion(float dt)
        {
            if (!Swimming) return;

            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            var motion = SeatsToMotion(step);

            // Add some easing to it
            float trueYaw = (Pos.Yaw * 57.2958f + (float)windAngle) % 360f - 180f;
            float angleSpeed = (float)(SailAngle + trueYaw / 2f) * (Math.Abs(SailAngle) - Math.Abs(trueYaw) > 1f ? 0f : 1f);
            double sailMultiplier = clamp(0.0, 1.0, Math.Exp(-Math.Abs(angleSpeed / 30f))) * (Math.Abs(SailAngle) - Math.Abs(trueYaw) > 1f ? 0f : 1f); // Check that the sail is at the right angle n' stuff

            // Sailing profile calculation
            //sailMultiplier *= (Math.Abs(trueYaw) > 90f ? 1.3f : 1f); 
            sailMultiplier *= 1d + 0.3d * Math.Sin(Math.Abs(trueYaw * GameMath.DEG2RAD));

            float sailSpeed = (float)(Math.Min(windSpeed, 1.0)) * (SailLevel / 3f) * SailboatConfig.Current.windMultiplier * (float)sailMultiplier;
            sailSpeed = Math.Min(sailSpeed, SailboatConfig.Current.hullSpeed);
            realSpeedMultiplier = SailLevel == 0.0 ? (motion.X * 0.75) : (double)sailSpeed * Math.Min(dt, 0.1);


            ForwardSpeed += (realSpeedMultiplier - ForwardSpeed) * dt;
            //ForwardSpeed += (motion.X * realSpeedMultiplier - ForwardSpeed) * dt; // rowing
            //ForwardSpeed += (SailLevel > 0f ? (realSpeedMultiplier - ForwardSpeed) : 0f) * dt; // sailing
            AngularVelocity += (motion.Y * RotationSpeedMultiplier - AngularVelocity) * dt;

            var pos = SidedPos;

            if (ForwardSpeed != 0.0)
            {
                pos.Motion.Set(pos.GetViewVector().Mul((float)-ForwardSpeed).ToVec3d());
            }

            if (AngularVelocity != 0.0)
            {
                pos.Yaw += (float)AngularVelocity * dt * 30f;
                if (pos.Yaw < 0f) pos.Yaw += 360f;
            }
        }

        protected virtual bool HasPaddle(EntityAgent agent)
        {
            if (agent.RightHandItemSlot == null || agent.RightHandItemSlot.Empty) return false;
            return agent.RightHandItemSlot.Itemstack.Collectible.Attributes?.IsTrue("paddlingTool") == true;
        }

        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            foreach (var seat in Seats)
            {
                if (seat.Passenger == null && seat.controllable) SailLevel = 0f;

                if (seat.Passenger == null || !seat.controllable) continue;

                var controls = seat.controls;

                // PLACEHOLDERS TEST
                if ((controls.Forward || controls.Backward) && (SailLevel > 0f || !HasPaddle(seat.Passenger)))
                {
                    SailLimit += controls.Forward ? 30d * (double)dt : -30d * (double)dt;

                    SailLimit = clamp(15.0, 80.0, SailLimit);
                }

                if (controls.Sprint || controls.Jump)
                {
                    float dir = controls.Sprint ? 1 : -1;

                    SailLevel += dir * dt * 2f;

                    SailLevel = (float)clamp(0.0, 3.0, (float)SailLevel);
                }

                // FIN PLACEHOLDERS TEST

                if (!HasPaddle(seat.Passenger))
                {
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarBackward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarForward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarReady");
                    continue;
                }

                if (!controls.TriesToMove)
                {
                    seat.Passenger.AnimManager?.StartAnimation("crudeOarReady");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarBackward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarForward");
                    continue;
                }
                else
                {
                    seat.Passenger.AnimManager?.StartAnimation(controls.Backward ? "crudeOarBackward" : "crudeOarForward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarReady");
                }

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                if (controls.Left || controls.Right)
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }

                if ((controls.Forward || controls.Backward) && SailLevel == 0f)
                {
                    float dir = controls.Forward ? 1 : -1;

                    var yawdist = Math.Abs(GameMath.AngleRadDistance(SidedPos.Yaw, seat.Passenger.SidedPos.Yaw));
                    bool isLookingBackwards = yawdist > GameMath.PIHALF;

                    if (isLookingBackwards) dir *= -1;

                    linearMotion += str * dir * dt * 2f;
                }

                // Only the first player can control the boat
                // Reason: Very difficult to properly smoothly synchronize that over the network
                break;
            }

            return new Vec2d(linearMotion, angularMotion);
        }


        public bool IsMountedBy(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity) return true;
            }
            return false;
        }

        public Vec3f GetMountOffset(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity)
                {
                    return seat.MountOffset;
                }
            }
            return null;
        }

        double clamp(double min, double max, double val)
        {
            return Math.Min(max, Math.Max(min, val));
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode != EnumInteractMode.Interact)
            {
                return;
            }

            // sneak + click to remove boat
            if (byEntity.Controls.Sneak && IsEmpty())
            {
                foreach (var seat in Seats)
                {
                    seat.Passenger?.TryUnmount();
                }

                ItemStack stack = new ItemStack(World.GetItem(Code));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    World.SpawnItemEntity(stack, ServerPos.XYZ);
                }
                Die(EnumDespawnReason.Removed);
                return;
            }

            if (World.Side == EnumAppSide.Server)
            {
                foreach (var seat in Seats)
                {
                    if (byEntity.MountedOn == null && seat.Passenger == null)
                    {
                        byEntity.TryMount(seat);
                    }
                }

                /*Vec3d boatDirection = Vec3dFromYaw(ServerPos.Yaw);
                Vec3d hitDirection = hitPosition.Normalize();
                double hitDotProd = hitDirection.X * boatDirection.X + hitDirection.Z * boatDirection.Z;
                int seatNumber = hitDotProd > 0.0 ? 1 : 0;
                if (byEntity.MountedOn == null && Seats[seatNumber].Passenger == null)
                {
                    byEntity.TryMount(Seats[seatNumber]);
                }*/

            }
        }


        public static Vec3d Vec3dFromYaw(float yawRad)
        {
            return new Vec3d(Math.Cos(yawRad), 0.0, -Math.Sin(yawRad));
        }

        public override bool CanCollect(Entity byEntity)
        {
            return false;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(Seats.Length);
            foreach (var seat in Seats)
            {
                writer.Write(seat.Passenger?.EntityId ?? (long)0);
            }
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);

            int numseats = reader.ReadInt32();
            for (int i = 0; i < numseats; i++)
            {
                long entityId = reader.ReadInt64();
                Seats[i].PassengerEntityIdForInit = entityId;
            }
        }

        public bool IsEmpty()
        {
            return !Seats.Any(seat => seat.Passenger != null);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return base.GetInteractionHelp(world, es, player);
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            if (sailboatShape == null) sailboatShape = entityShape;

            base.OnTesselation(ref entityShape, shapePathForLogging);
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}

using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using BepInEx.Logging;
using DevInterface;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace RevivifyMeadowFix;

/*
 * Everything in this plugin is from the original Revivify creator Dual, unless
 * specified as being from Daimyo, who created the proximity-based revival and
 * made the mod Rain Meadow compatible. I am only here to combine their methods
 * to make the original CPR Revivify method work with Rain Meadow, and combine
 * the two functionality's under one mod, and to keep it updated with the game.
 *
 * I am not a Rain World modder, I've modded one other game that used a custom
 * game engine. I have game dev and programming experience which prompted me to
 * give this a shot. I make no guarantees or any other assurances about whether
 * this mod will work correctly or works as optimally as it can. With that, I
 * hope you all enjoy the work that was put into it, and have fun. Toodles.
 *
 * - Auxiar Molkhun
 */
[BepInPlugin("com.auxiar.revivifymeadowfix", "Revivify Meadow Fix", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    static readonly ConditionalWeakTable<Player, PlayerData> cwt = new();
    static PlayerData Data(Player p) => cwt.GetValue(p, _ => new());

    static PlayerGraphics G(Player p) => p.graphicsModule as PlayerGraphics;

    private static Vector2 HeartPos(Player player)
    {
        return Vector2.Lerp(player.firstChunk.pos, player.bodyChunks[1].pos, 0.38f) + new Vector2(0, 0.7f * player.firstChunk.rad);
    }

    private static bool CanRevive(Player medic, Player reviving)
    {
        // Daimyo's Proximity implementation check
        if (Options.ReviveWithProximity.Value)
        {
            return !reviving.playerState.permaDead && reviving.dead && 
                !Data(reviving).Expired && Data(reviving).deaths < Options.DeathsUntilExpire.Value &&
                Vector2.Distance(medic.firstChunk.pos, reviving.firstChunk.pos) <= Options.ReviveDistance.Value &&
                (medic.bodyMode == Player.BodyModeIndex.Stand || medic.bodyMode == Player.BodyModeIndex.Crawl ||
                 medic.bodyMode == Player.BodyModeIndex.Swimming || medic.bodyMode == Player.BodyModeIndex.CorridorClimb ||
                 medic.bodyMode == Player.BodyModeIndex.ClimbingOnBeam || medic.bodyMode == Player.BodyModeIndex.ClimbIntoShortCut ||
                 medic.bodyMode == Player.BodyModeIndex.Default || medic.bodyMode == Player.BodyModeIndex.WallClimb);
        }
        
        // Dual's original checks
        if (reviving.playerState.permaDead || !reviving.dead || reviving.grabbedBy.Count > 1 || reviving.Submersion > 0 || reviving.onBack != null
            || Data(reviving).Expired || Data(reviving).deaths >= Options.DeathsUntilExpire.Value
            || !medic.Consious || medic.grabbedBy.Count > 0 || medic.Submersion > 0 || medic.exhausted || medic.lungsExhausted || medic.gourmandExhausted) {
            return false;
        }
        bool corpseStill = reviving.IsTileSolid(0, 0, -1) && reviving.IsTileSolid(1, 0, -1) && reviving.bodyChunks[0].vel.magnitude < 6;
        bool selfStill = medic.input.Take(10).All(i => i.x == 0 && i.y == 0 && !i.thrw && !i.jmp) && medic.bodyChunks[1].ContactPoint.y < 0;
        return corpseStill && selfStill && medic.bodyMode == Player.BodyModeIndex.Stand;
    }

    private static void RevivePlayer(Player self)
    {
        Data(self).deathTime = 0;

        self.stun = 20;
        self.airInLungs = 0.1f;
        self.exhausted = true;
        self.aerobicLevel = 1;

        self.playerState.permanentDamageTracking = Mathf.Clamp01((float)Data(self).deaths / Options.DeathsUntilExhaustion.Value) * 0.6;
        self.playerState.alive = true;
        self.playerState.permaDead = false;
        self.dead = false;
        self.killTag = null;
        self.killTagCounter = 0;
        self.abstractCreature.abstractAI?.SetDestination(self.abstractCreature.pos);
    }

    public void OnEnable()
    {
        On.RainWorld.Update += ErrorCatch;
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.Player.ctor += Player_ctor;
        On.Player.Die += Player_Die;
        On.HUD.FoodMeter.GameUpdate += FixFoodMeter;
        On.Player.Update += UpdatePlr;
        On.Creature.Violence += ReduceLife;
        On.Player.CanEatMeat += DontEatPlayers;

        // Fixes corpse being dropped when pressing Grab
        On.PlayerGraphics.Update += PlayerGraphics_Update;
        
        // Functions dropped by Daimyo's mod:
        On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
        new Hook(typeof(Player).GetMethod("get_Malnourished"), getMalnourished);
        On.Player.CanIPutDeadSlugOnBack += Player_CanIPutDeadSlugOnBack;
        On.Player.GraphicsModuleUpdated += DontMoveWhileReviving;
        IL.Player.GrabUpdate += Player_GrabUpdate;
        On.Player.GrabUpdate += FixHeavyCarry;
        On.Player.HeavyCarry += FixHeavyCarry;
        On.SlugcatHand.Update += SlugcatHand_Update;
        

        /*
         * Dual's version of this hook was breaking with meadow due to meadow loading the player graphics early,
         * I'm not sure how Daimyo's fixes it, as his version gives me an error and refuses to compile (probably
         * a reference problem on my end), but this is what I tracked down as the actual problem function, removing
         * it functionally fixes the meadow incompatibility. It essentially controlled the face sprite that was
         * displayed on the corpse while being revived. I've done a poor job trying to port the logic into the
         * PlayerGraphics_DrawSprites function, but it serves its purpose and no longer breaks with Meadow.
         */
        //IL.PlayerGraphics.DrawSprites += ChangeHeadSprite;
        
    }

    private void ErrorCatch(On.RainWorld.orig_Update orig, RainWorld self)
    {
        try {
            orig.Invoke(self);
        }
        catch (Exception e) {
            Logger.LogError(e);
            throw;
        }
    }

    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig.Invoke(self);
        MachineConnector.SetRegisteredOI("auxiar.revivifymeadowfix", new Options());
    }

    private readonly Func<Func<Player, bool>, Player, bool> getMalnourished = (orig, self) => orig.Invoke(self) || Data(self).deaths >= Options.DeathsUntilExhaustion.Value;

    private bool Player_CanIPutDeadSlugOnBack(On.Player.orig_CanIPutDeadSlugOnBack orig, Player self, Player pickUpCandidate)
    {
        /*
         * Daimyo's implementation doesn't include this at all, so as not to break meadow's default no-grabbing philosophy.
         * However, while testing, this caused comatose slugpups to not be able to be placed on the player's back, hindering
         * mobility by forcing the slugpup to be carried in both hands as what is essentially a living corpse. For Gameplay
         * reasons, I'm keeping Dual's original implementation as the default, despite it going against meadow's philosophy.
         * I'll include it as a setting in-case players would like to disable it at the very least. When disabled, it only
         * applies when NOT using proximity-based revival, meaning if proximity-based revive is off, then piggybacking is
         * allowed by default
         */
        if (Options.AllowCorpsePiggyback.Value)
        {
            return orig.Invoke(self, pickUpCandidate) || (pickUpCandidate != null && self.slugOnBack != null && !Data(pickUpCandidate).Expired);   
        }
        return orig.Invoke(self, pickUpCandidate) || (!Options.ReviveWithProximity.Value && (pickUpCandidate != null && self.slugOnBack != null && !Data(pickUpCandidate).Expired));
    }

    private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig.Invoke(self, abstractCreature, world);

        if (self.dead) {
            Data(self).expireTime = int.MaxValue;
        }
    }

    private void Player_Die(On.Player.orig_Die orig, Player self)
    {
        if (!self.dead) {
            if (self.drown > 0.25f || self.rainDeath > 0.25f) {
                Data(self).waterInLungs = 1;
            }
            Data(self).deaths++;
        }
        orig.Invoke(self);
    }

    private void FixFoodMeter(On.HUD.FoodMeter.orig_GameUpdate orig, HUD.FoodMeter self)
    {
        orig.Invoke(self);

        if (self.IsPupFoodMeter) {
            self.survivalLimit = self.pup.slugcatStats.foodToHibernate;
        }
    }

    private void UpdatePlr(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig.Invoke(self, eu);
        
        const int ticksToDie = 40 * 30; // 30 seconds
        const int ticksToRevive = 40 * 10; // 10 seconds
    
        if (self.isSlugpup && Data(self).deaths >= Options.DeathsUntilComa.Value) {
            self.stun = 100;
        }

        // simplify deathTime ref
        ref float death = ref Data(self).deathTime;
        
        // Run the expiry check for both one time
        if (self.dead)
        {
            if (death > 0.1f)
            {
                Data(self).expireTime++;
            }
            else
            {
                Data(self).expireTime = 0;
            }

            if (self.room?.shelterDoor != null && self.room.shelterDoor.IsClosing)
            {
                RevivePlayer(self);
                death = 0;
            }
        }

        // If player wants to use proximity revive in place of CPR
        if (Options.ReviveWithProximity.Value)
        {   
            // Daimyo's proximity-based method
            if (self.dead)
            {
                Room room = self.room;
                foreach (AbstractWorldEntity abstractWorldEntity in ((room != null) ? room.abstractRoom.entities : null))
                {
                    AbstractCreature abstractCreature = abstractWorldEntity as AbstractCreature;
                    Player player = abstractCreature?.realizedCreature as Player;
                    if (player != null && player != self && CanRevive(player, self) && self.dangerGrasp == null)
                    {
                        /*
                         * Diamyo's implementation on when the player should be revived
                         * is dependent on the frame-rate which makes it inconsistent
                         * between players, and inconsistent at different frame rates.
                         * Playing at 10fps mean it would take 4 seconds to revive by
                         * default, whereas if you were playing at 120fps would only
                         * take 1/3 of a second. Instead, we're going to try to
                         * Frankenstein Dual's tick solution into Diamyo's timer-like
                         * solution in order to keep similar functionality, but make
                         * it more consistent in timing between the two.
                         */
                        
                        // If the player can be revived (^) start countdown to revival
                        death -= 1f / (ticksToRevive * (1f / (Options.ReviveSpeed.Value * 1.75f)));
                        //UnityEngine.Debug.Log($"Thing is being revived with value: {death}");
                        
                        // If countdown completes, revive player
                        if (death <= -1f)
                        {
                            RevivePlayer(self);
                            //UnityEngine.Debug.Log($"Revived thing with death value at: {death}");
                    
                            // automatically throw held revived thing
                            if (self.grabbedBy.FirstOrDefault()?.grabber is Player p) {
                                p.ThrowObject(self.grabbedBy[0].graspUsed, eu);
                            }
                            death = 0f;
                            return;
                        }
                    }
                }
                // If player is not being revived, count back up (get more dead)
                death += 1f / ticksToDie;
                //UnityEngine.Debug.Log($"Thing is dying with value: {death}");
                
                death = Mathf.Clamp(death, -1, 1);
            }
        }
        else // Dual's original CPR calculation
        {
            if (self.dead) {

                if (death > -0.1f)
                {
                    death += 1f / ticksToDie;
                }

                if (death < -0.5f && self.dangerGrasp == null)
                {
                    death -= 1f / ticksToRevive;

                    if (self.room?.shelterDoor != null && self.room.shelterDoor.IsClosing)
                    {
                        death = -1.1f;
                    }
                }
                
                if (death < -1) {
                    RevivePlayer(self);
                    
                    if (self.grabbedBy.FirstOrDefault()?.grabber is Player p) {
                        p.ThrowObject(self.grabbedBy[0].graspUsed, eu);
                    }
                }
    
                death = Mathf.Clamp(death, -1, 1);
            }
            else if (Data(self).waterInLungs > 0 && UnityEngine.Random.value < 1 / 40f && self.Consious) {
                Data(self).waterInLungs -= UnityEngine.Random.value / 4f;
    
                G(self).breath = Mathf.PI;
    
                self.Stun(20);
                self.Blink(10);
                self.airInLungs = 0;
                self.firstChunk.pos += self.firstChunk.Rotation * 3;
    
                int amount = UnityEngine.Random.Range(3, 6);
                for (int i = 0; i < amount; i++) {
                    Vector2 dir = Custom.RotateAroundOrigo(self.firstChunk.Rotation, -40f + 80f * UnityEngine.Random.value);
    
                    self.room?.AddObject(new WaterDrip(self.firstChunk.pos + dir * 30, dir * (3 + 6 * UnityEngine.Random.value), true));
                }
            }
            else {
                Data(self).deathTime = 0;
            }
    
            if (Data(self).deaths >= Options.DeathsUntilExhaustion.Value) {
                if (self.isSlugpup) {
                    self.slugcatStats.foodToHibernate = self.slugcatStats.maxFood;
                }
                if (self.aerobicLevel >= 1f) {
                    Data(self).exhausted = true;
                }
                else if (self.aerobicLevel < 0.3f) {
                    Data(self).exhausted = false;
                }
                if (Data(self).exhausted) {
                    self.slowMovementStun = Math.Max(self.slowMovementStun, (int)Custom.LerpMap(self.aerobicLevel, 0.7f, 0.4f, 6f, 0f));
                    if (self.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.04f) {
                        self.Stun(10);
                    }
                    if (self.aerobicLevel > 0.9f && UnityEngine.Random.value < 0.1f) {
                        self.standing = false;
                    }
                    if (!(self.lungsExhausted && self.animation != Player.AnimationIndex.SurfaceSwim)) {
                        self.swimCycle += 0.05f;
                    }
                }
            }
        }
    }

    private void ReduceLife(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
    {
        bool wasDead = self.dead;

        orig.Invoke(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);

        if (self is not Player p || !wasDead || !p.dead || !(damage > 0)) return;
        PlayerData data = Data(p);
        if (data.deathTime < 0) {
            data.deathTime = 0;
        }
        data.deathTime += damage * 0.34f;
    }

    private bool DontEatPlayers(On.Player.orig_CanEatMeat orig, Player self, Creature crit)
    {
        return crit is not Player && orig.Invoke(self, crit);
    }

    private void DontMoveWhileReviving(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
    {
        // Return early when using proximity revive to avoid visual glitches
        if (Options.ReviveWithProximity.Value)
        {
            orig.Invoke(self, actuallyViewed, eu);
            return;
        };
        
        Vector2 pos1 = default, pos2 = default, vel1 = default, vel2 = default;
        Vector2 posH = default, posB = default, velH = default, velB = default;

        foreach (var grasp in self.grasps) {
            if (grasp?.grabbed is Player p && CanRevive(self, p)) {
                posH = self.bodyChunks[0].pos;
                posB = self.bodyChunks[1].pos;
                velH = self.bodyChunks[0].vel;
                velB = self.bodyChunks[1].vel;

                pos1 = p.bodyChunks[0].pos;
                pos2 = p.bodyChunks[1].pos;
                vel1 = p.bodyChunks[0].vel;
                vel2 = p.bodyChunks[1].vel;
                break;
            }
        }
        
        /*
         * Dual's original function uses this invoke in the middle of the function so that the positions and
         * velocities get stored before the update, which holds the corpse in place. Unfortunately, because
         * the function is bound at all, when grabbing a corpse you'll begin to slide away with it. It's not
         * looking like I can fix this without worse glitches occurring, so sorry about that.
         */
        orig.Invoke(self, actuallyViewed, eu);

        if (pos1 != default) {
            foreach (var grasp in self.grasps) {
                if (grasp?.grabbed is Player p && CanRevive(self, p)) {
                    self.bodyChunks[0].vel = velH;
                    self.bodyChunks[1].vel = velB;
                    self.bodyChunks[0].pos = posH;
                    self.bodyChunks[1].pos = posB;
                        
                    p.bodyChunks[0].vel = vel1;
                    p.bodyChunks[1].vel = vel2;
                    p.bodyChunks[0].pos = pos1;
                    p.bodyChunks[1].pos = pos2;   
                    break;
                }
            }
        }
    }

    private void Player_GrabUpdate(ILContext il)
    {
        try {
            ILCursor cursor = new(il);
            // Move after num11 check and ModManager.MSC
            cursor.GotoNext(MoveType.After, i => i.MatchStloc(8));
            cursor.Index++;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc_S, il.Body.Variables[8]);
            cursor.EmitDelegate(UpdateRevive);
            cursor.Emit(OpCodes.Brfalse, cursor.Next);
            cursor.Emit(OpCodes.Pop); // pop "ModManager.MSC" off stack
            cursor.Emit(OpCodes.Ret);
        }
        catch (Exception e) {
            Logger.LogError(e);
        }
    }

    private bool UpdateRevive(Player self, int grasp)
    {
        // We want to return [false] early if using proximity so that the corpse doesn't slide while being held
        if (Options.ReviveWithProximity.Value) return false;
        
        PlayerData data = Data(self);

        if (self.grasps[grasp]?.grabbed is not Player reviving || !CanRevive(self, reviving)) {
            data.Unprepared();
            return false;
        }

        Vector2 heartPos = HeartPos(reviving);
        Vector2 targetHeadPos = heartPos + new Vector2(0, Mathf.Sign(self.room.gravity)) * 25;
        Vector2 targetButtPos = heartPos - new Vector2(0, reviving.bodyChunks[0].rad);
        float headDist = (targetHeadPos - self.bodyChunks[0].pos).magnitude;
        float buttDist = (targetButtPos - self.bodyChunks[1].pos).magnitude;

        if (data.animTime < 0 && (headDist > 22 || buttDist > 22)) {
            return false;
        }

        self.bodyChunks[0].vel += Mathf.Min(headDist, 0.4f) * (targetHeadPos - self.bodyChunks[0].pos).normalized;
        self.bodyChunks[1].vel += Mathf.Min(buttDist, 0.4f) * (targetButtPos - self.bodyChunks[1].pos).normalized;

        PlayerData revivingData = Data(reviving);
        int difference = self.room.game.clock - revivingData.lastCompression;

        if (data.animTime < 0) {
            data.PreparedToGiveCpr();
        }
        else if (self.input[0].pckp && !self.input[1].pckp && difference > 4) {
            Compression(self, grasp, data, reviving, revivingData, difference);
        }

        AnimationStage stage = data.Stage();

        if (stage is AnimationStage.Prepared or AnimationStage.CompressionRest) {
            self.bodyChunkConnections[0].distance = 14;
        }
        if (stage is AnimationStage.CompressionDown) {
            self.bodyChunkConnections[0].distance = 13 - data.compressionDepth;
        }
        if (stage is AnimationStage.CompressionUp) {
            self.bodyChunkConnections[0].distance = Mathf.Lerp(13 - data.compressionDepth, 15, (data.animTime - 3) / 2f);
        }

        if (data.animTime > 0) {
            data.animTime++;
        }
        if (Data(reviving).compressionsUntilBreath > 0) {
            if (data.animTime >= 20)
                data.PreparedToGiveCpr();
        }
        else if (data.animTime >= 80) {
            data.PreparedToGiveCpr();
        }

        return false;
    }

    private static bool disableHeavyCarry = false;
    private void FixHeavyCarry(On.Player.orig_GrabUpdate orig, Player self, bool eu)
    {
        try {
            orig.Invoke(self, eu);
            disableHeavyCarry = true;
        }
        finally {
            disableHeavyCarry = false;
        }
    }
    private bool FixHeavyCarry(On.Player.orig_HeavyCarry orig, Player self, PhysicalObject obj)
    {
        return !(disableHeavyCarry && obj is Player p && CanRevive(self, p)) && orig.Invoke(self, obj);
    }

    private static void Compression(Player self, int grasp, PlayerData data, Player reviving, PlayerData revivingData, int difference)
    {
        if (self.slugOnBack != null) {
            self.slugOnBack.interactionLocked = true;
            self.slugOnBack.counter = 0;
        }

        if (self.grasps[grasp].chunkGrabbed == 1) {
            self.grasps[grasp].chunkGrabbed = 0;
        }

        if (reviving.AI != null) {
            reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceLike(10f);
            reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceTempLike(10f);
            reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceKnow(0.5f);
        }

        for (int i = reviving.abstractCreature.stuckObjects.Count - 1; i >= 0; i--) {
            if (reviving.abstractCreature.stuckObjects[i] is AbstractPhysicalObject.AbstractSpearStick stick && stick.A.realizedObject is Spear s) {
                s.ChangeMode(Weapon.Mode.Free);
            }
        }

        data.StartCompression();
        self.AerobicIncrease(0.5f);

        revivingData.compressionsUntilBreath--;
        if (revivingData.compressionsUntilBreath < 0) {
            revivingData.compressionsUntilBreath = 1000;//(int)(8 + UnityEngine.Random.value * 5);
        }

        bool breathing = revivingData.compressionsUntilBreath == 0;
        float healing = difference switch {
            < 75 when breathing => -1 / 10f,
            < 100 when breathing => 1 / 5f,
            < 8 => -1 / 30f,
            < 19 => 1 / 40f,
            < 22 => 1 / 15f,
            < 30 => 1 / 20f,
            _ => 1 / 40f,
        };
        data.compressionDepth = difference switch {
            < 8 => 0.2f,
            < 19 => 1f,
            < 22 => 4.5f,
            < 30 => 3.5f,
            _ => 1f
        };
        if (data.compressionDepth > 4) self.Blink(6);
        revivingData.deathTime -= healing * Options.ReviveSpeed.Value;
        revivingData.lastCompression = self.room.game.clock;

        if (revivingData.waterInLungs > 0) {
            revivingData.waterInLungs -= healing * 0.34f;

            float amount = data.compressionDepth * 0.5f + UnityEngine.Random.value - 0.5f;
            for (int i = 0; i < amount; i++) {
                Vector2 dir = Custom.RotateAroundOrigo(new Vector2(0, 1), -30f + 60f * UnityEngine.Random.value);

                reviving.room.AddObject(new WaterDrip(reviving.firstChunk.pos + dir * 10, dir * (2 + 4 * UnityEngine.Random.value), true));
            }
        }
    }
    
    private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
    {
        orig.Invoke(self);
        
        PlayerData data = Data(self.player);

        float visualDecay = Mathf.Max(Mathf.Clamp01(data.deathTime), Mathf.Clamp01((float)data.deaths / Options.DeathsUntilExhaustion.Value) * 0.6f);
        if (self.malnourished < visualDecay) {
            self.malnourished = visualDecay;
        }

        // Don't continue with CPR animation if using Proximity
        if (Options.ReviveWithProximity.Value) return;

        if (self.player.grasps.FirstOrDefault(g => g?.grabbed is Player)?.grabbed is not Player reviving) {
            return;
        }
        
        AnimationStage stage = data.Stage();

        if (stage == AnimationStage.None) return;

        Vector2 starePos = stage is AnimationStage.Prepared or AnimationStage.CompressionDown or AnimationStage.CompressionUp or AnimationStage.CompressionRest
            ? HeartPos(reviving)
            : reviving.firstChunk.pos + reviving.firstChunk.Rotation * 5;

        self.LookAtPoint(starePos, 10000f);

        if (stage is AnimationStage.CompressionDown) {
            // Push reviving person's head and butt upwards
            PlayerGraphics graf = G(reviving);
            graf.head.vel.y += data.compressionDepth * 0.5f;
            graf.NudgeDrawPosition(0, new(0, data.compressionDepth * 0.5f));
            if (graf.tail.Length > 1) {
                graf.tail[0].pos.y += 1;
                graf.tail[0].vel.y += data.compressionDepth * 0.8f;
                graf.tail[1].vel.y += data.compressionDepth * 0.2f;
            }
        }
    }
    
    private void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig.Invoke(self, sLeaser, rCam, timeStacker, camPos);

        var player = self.player;
        if (player == null) return;
        if (sLeaser?.sprites == null || sLeaser.sprites.Length <= 9) return;
        
        if (player.grabbedBy?.Count == 1 && player.grabbedBy[0].grabber is Player medic && CanRevive(medic, player) && Data(medic).animTime >= 0) {
            sLeaser.sprites[9].y += 6;
            sLeaser.sprites[3].rotation -= 50 * Mathf.Sign(sLeaser.sprites[3].rotation);
            sLeaser.sprites[3].scaleX *= -1;
            
            sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceStunned");

            return;
        }

        if (sLeaser.sprites[9]?.element?.name == "FaceDead" && Data(player).deathTime < -0.6f) {
            sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceStunned");
        }
    }

    private void SlugcatHand_Update(On.SlugcatHand.orig_Update orig, SlugcatHand self)
    {
        orig.Invoke(self);

        // Exit early if using Proximity Revive
        if (Options.ReviveWithProximity.Value) return;
        
        Player player = ((PlayerGraphics)self.owner).player;
        PlayerData data = Data(player);

        if (player.grasps.FirstOrDefault(g => g?.grabbed is Player)?.grabbed is not Player reviving) {
            return;
        }
        
        AnimationStage stage = data.Stage();

        if (stage == AnimationStage.None) return;

        Vector2 heart = HeartPos(reviving);
        Vector2 heartDown = HeartPos(reviving) - new Vector2(0, data.compressionDepth);

        if (stage is AnimationStage.Prepared or AnimationStage.CompressionRest) {
            self.pos = heart;
        }
        else if (stage == AnimationStage.CompressionDown) {
            self.pos = heartDown;
        }
        else if (stage == AnimationStage.CompressionUp) {
            self.pos = Vector2.Lerp(heartDown, heart, (data.animTime - 3) / 2f);
        }
    }
}

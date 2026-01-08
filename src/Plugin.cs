using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace RevivifyMeadowSupport;

/*
 * A lot of this plugin is from the original Revivify creator Dual, unless
 * specified as being from Daimyo, who created the proximity-based revival that
 * worked with Rain Meadow. I am only here to combine their methods
 * to make the original CPR Revivify compatible with Rain Meadow, and combine
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

[BepInPlugin("com.auxiar.revivifymeadowsupport", "Revivify Meadow Support", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    static readonly ConditionalWeakTable<Player, PlayerData> Cwt = new();
    static PlayerData Data(Player p) => Cwt.GetValue(p, _ => new());

    static PlayerGraphics G(Player p) => p.graphicsModule as PlayerGraphics;

    private static Vector2 HeartPos(Player player)
    {
        return Vector2.Lerp(player.firstChunk.pos, player.bodyChunks[1].pos, 0.38f) + new Vector2(0, 0.7f * player.firstChunk.rad);
    }

    // Prevent GetMalnourished from dangling later (IDE kept yelling at me about it)
    private Hook _getMalnourishedHook;

    private static bool CanRevive(Player medic, Player reviving, bool usingProximity)
    {
        // Check common checks once
        if (medic == reviving || medic.room == null || reviving.room == null || medic.room != reviving.room || medic.enteringShortCut != null
            || medic.inShortcut || reviving.enteringShortCut != null || reviving.inShortcut || reviving.playerState.permaDead
            || !reviving.dead || Data(reviving).Expired || Data(reviving).deaths >= Options.DeathsUntilExpire.Value)
            return false;
        
        // Daimyo's Proximity implementation check
        if (usingProximity)
            return Vector2.Distance(medic.firstChunk.pos, reviving.firstChunk.pos) <= Options.ReviveDistance.Value &&
                   (medic.bodyMode != Player.BodyModeIndex.Dead && medic.bodyMode != Player.BodyModeIndex.Stunned);
        
        // Dual's CPR checks
        if (reviving.grabbedBy.Count > 1 || reviving.Submersion > 0 || reviving.onBack != null
            || !medic.Consious || medic.grabbedBy.Count > 0 || medic.Submersion > 0 || medic.exhausted || medic.lungsExhausted || medic.gourmandExhausted) 
            return false;
        
        bool corpseStill = reviving.IsTileSolid(0, 0, -1) && reviving.IsTileSolid(1, 0, -1) && reviving.bodyChunks[0].vel.magnitude < 6;
        bool selfStill = medic.input.Take(10).All(i => i is { x: 0, y: 0, thrw: false, jmp: false }) && medic.bodyChunks[1].ContactPoint.y < 0;
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
        On.Player.CanIPutDeadSlugOnBack += Player_CanIPutDeadSlugOnBack;
        On.Player.GraphicsModuleUpdated += DontMoveWhileReviving;
        //IL.Player.GrabUpdate += DEPRECATED_Player_GrabUpdate;
        On.Player.GrabUpdate += Player_GrabUpdate;
        On.Player.HeavyCarry += FixHeavyCarry;
        On.SlugcatHand.Update += SlugcatHand_Update;
        
        _getMalnourishedHook = new Hook(typeof(Player).GetMethod("get_Malnourished"), _getMalnourished);

        /*
         * Dual's Original Reviviy used ChangeHeadSprite() to adjust the head of the slugcat being revived. This
         * caused conflicts with Rain Meadow, and unfortunately had to be dropped for the sake of compatibility.
         */
        //IL.PlayerGraphics.DrawSprites += ChangeHeadSprite;

    }

    // Neither mod previously used an OnDisable function, figured I'd implement it to dot my i's
    public void OnDisable()
    {
        On.RainWorld.Update -= ErrorCatch;
        On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
        On.Player.ctor -= Player_ctor;
        On.Player.Die -= Player_Die;
        On.HUD.FoodMeter.GameUpdate -= FixFoodMeter;
        On.Player.Update -= UpdatePlr;
        On.Creature.Violence -= ReduceLife;
        On.Player.CanEatMeat -= DontEatPlayers;

        // Fixes corpse being dropped when pressing Grab
        On.PlayerGraphics.Update -= PlayerGraphics_Update;
        
        // Functions dropped by Daimyo's mod:
        On.PlayerGraphics.DrawSprites -= PlayerGraphics_DrawSprites;
        On.Player.CanIPutDeadSlugOnBack -= Player_CanIPutDeadSlugOnBack;
        On.Player.GraphicsModuleUpdated -= DontMoveWhileReviving;
        //IL.Player.GrabUpdate -= DEPRECATED_Player_GrabUpdate;
        On.Player.GrabUpdate -= Player_GrabUpdate;
        On.Player.HeavyCarry -= FixHeavyCarry;
        On.SlugcatHand.Update -= SlugcatHand_Update;
        
        // Disposes of the malnourished hook and clears reference
        _getMalnourishedHook.Dispose();
        _getMalnourishedHook = null;

        /*
         * Dual's Original Reviviy used ChangeHeadSprite() to adjust the head of the slugcat being revived. This
         * caused conflicts with Rain Meadow, and unfortunately had to be dropped for the sake of compatibility.
         */
        //IL.PlayerGraphics.DrawSprites -= ChangeHeadSprite;
    }

    private readonly Func<Func<Player, bool>, Player, bool> _getMalnourished = (orig, self) => orig.Invoke(self) || Data(self).deaths >= Options.DeathsUntilExhaustion.Value;

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
        MachineConnector.SetRegisteredOI("auxiar.revivifymeadowsupport", new Options());
    }

    private bool Player_CanIPutDeadSlugOnBack(On.Player.orig_CanIPutDeadSlugOnBack orig, Player self, Player pickUpCandidate)
    {
        /*
         * Daimyo's implementation doesn't include this at all, so as not to break meadow's default no-grabbing philosophy.
         * However, while testing, this caused comatose slugpups to not be able to be placed on the player's back, hindering
         * mobility by forcing the slugpup to be carried in both hands as what is essentially a living corpse. For Gameplay
         * reasons, I'm keeping Dual's original implementation as the default, despite it going against meadow's philosophy.
         * I'll include it as a setting in-case players would like to disable it at the very least. The setting only works
         * if using Proximity revive, meaning if proximity-based revive is off, then piggybacking is allowed regardless of setting
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
        
        Room room = self.room;
        
        // puts slugpups into comas
        if (self.isSlugpup && Data(self).deaths >= Options.DeathsUntilComa.Value) self.stun = 100;

        // simplify deathTime ref
        ref float death = ref Data(self).deathTime;
        
        // Run the expiry check for both once and revive if shelter door is closing
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
            
            bool beingRevived = false;
            
            // If we're near life and not in direct danger, keep reviving organically
            if (death < -0.55f && self.dangerGrasp == null)
            {
                death -= 1f / ticksToRevive;
                beingRevived = true;
            } 

            bool isLocal = self.controller == null;
            bool isAI = self.AI != null && self.isNPC;
            
            // If player wants to use proximity revive
            if (Options.ReviveWithProximity.Value && (isLocal || isAI))
            {
                if (room == null) return;
                foreach (AbstractWorldEntity entity in room.abstractRoom.entities)
                {
                    // check if self is NPC (not the local player) to prevent revival from happening over the network
                    beingRevived = entity is AbstractCreature { realizedCreature: Player player } && CanRevive(player, self, true) && self.dangerGrasp == null; 
                    if (!beingRevived) continue;
                    // tick player closer to revival
                    death -= (Options.ReviveSpeed.Value * 1.6f) / ticksToRevive;
                    // Break so having multiple players in our vicinity doesn't cause us to revive faster
                    break;
                    //UnityEngine.Debug.Log($"Thing is being revived with value: {death}");
                }
            }
            
            // Running the CPR revival check on the client always ensures that CPR can be performed to speed up revival for proximity revive players
            if (self.grabbedBy.FirstOrDefault()?.grabber is Player medic) RespondToMedic(self, medic);

            // Revive if we're in a shelter, the door is closing, and we're not in the mouth of something dangerous
            if (room?.shelterDoor is { IsClosing: true } && self.dangerGrasp == null) death = -1.1f;

            if (death < -1 && (isLocal || isAI))
            {
                RevivePlayer(self);
                
                // tell reviver to throw us because we're alive (assumes singleplayer/jolly)
                if (self.grabbedBy.FirstOrDefault()?.grabber is Player p) {
                    p.ThrowObject(self.grabbedBy[0].graspUsed, eu);
                }
            }
            
            if (death >= -0.3f && !beingRevived) death += 1f / ticksToDie;
        }
        else
        {
            death = 0;
        }
        
        // Clamp deathTime value between -1 or 1
        death = Mathf.Clamp(death, -1, 1);
        
        // Water in lungs particle spawn check
        if (Data(self).waterInLungs > 0 && UnityEngine.Random.value < 1 / 40f && self.Consious) {
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
        
        // Moved outside of CPR check to apply it to proximity revival as well
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
        Vector2 pos1 = default, pos2 = default, vel1 = default, vel2 = default;
        Vector2 posH = default, posB = default, velH = default, velB = default;

        foreach (var grasp in self.grasps) {
            if (grasp?.grabbed is Player p && CanRevive(self, p, false)) {
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
        
        orig.Invoke(self, actuallyViewed, eu);

        if (pos1 != default) {
            foreach (var grasp in self.grasps) {
                if (grasp?.grabbed is Player p && CanRevive(self, p, false)) {
                    self.bodyChunks[0].pos = posH;
                    self.bodyChunks[1].pos = posB;
                    self.bodyChunks[0].vel = velH;
                    self.bodyChunks[1].vel = velB;

                    p.bodyChunks[0].pos = pos1;
                    p.bodyChunks[1].pos = pos2;
                    p.bodyChunks[0].vel = vel1;
                    p.bodyChunks[1].vel = vel2;
                    break;
                }
            }
        }
    }

    /* Deprecated, doesn't seem like it was really needed in place of the new functionality in new Player_GrabUpdate()
    private void DEPRECATED_Player_GrabUpdate(ILContext il)
    {
        try {
            ILCursor cursor = new(il);
            // Move after num11 check and ModManager.MSC
            cursor.GotoNext(MoveType.After, i => i.MatchStloc(8));
            cursor.Index++;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc_S, il.Body.Variables[8]);
            cursor.EmitDelegate(DEPRECATED_UpdateRevive);
            cursor.Emit(OpCodes.Brfalse, cursor.Next);
            cursor.Emit(OpCodes.Pop); // pop "ModManager.MSC" off stack
            cursor.Emit(OpCodes.Ret);
        }
        catch (Exception e) {
            Logger.LogError(e);
        }
    }
    */

    private static bool _disableHeavyCarry;
    private void Player_GrabUpdate(On.Player.orig_GrabUpdate orig, Player self, bool eu)
    {
        try {
            orig.Invoke(self, eu);
            /*
             * This if statement is used in place of the old IL PlayerGrabUpdate hook, which was causing the corpse
             * to slide when using the proximity revive method. To me, it just seemed unnecessary, and so it's been
             * deprecated, and the old FixHeavyCarry() function has been renamed to Player_GrabUpdate() to be consistent
             * with the naming conventions and how functions are connected (using On instead of IL)
             */
            for (int i = 0; i < self.grasps.Length; i++)
            {
                if (self.grasps[i] != null)
                {
                    UpdateRevive(self, i);
                }
            }
            _disableHeavyCarry = true;
        }
        finally {
            _disableHeavyCarry = false;
        }
    }
    
    private bool FixHeavyCarry(On.Player.orig_HeavyCarry orig, Player self, PhysicalObject obj)
    {
        return !(_disableHeavyCarry && obj is Player p && CanRevive(self, p, Options.ReviveWithProximity.Value)) && orig.Invoke(self, obj);
    }
    
    private void UpdateRevive(Player self, int grasp)
    {
        PlayerData data = Data(self);

        if (self.grasps[grasp]?.grabbed is not Player reviving || !CanRevive(self, reviving, false)) {
            data.Unprepared();
            return;
        }

        Vector2 heartPos = HeartPos(reviving);
        Vector2 targetHeadPos = heartPos + new Vector2(0, Mathf.Sign(self.room.gravity)) * 25;
        Vector2 targetButtPos = heartPos - new Vector2(0, reviving.bodyChunks[0].rad);
        float headDist = (targetHeadPos - self.bodyChunks[0].pos).magnitude;
        float buttDist = (targetButtPos - self.bodyChunks[1].pos).magnitude;

        if (data.animTime < 0 && (headDist > 22 || buttDist > 22)) {
            return;
        }

        self.bodyChunks[0].vel += Mathf.Min(headDist, 0.4f) * (targetHeadPos - self.bodyChunks[0].pos).normalized;
        self.bodyChunks[1].vel += Mathf.Min(buttDist, 0.4f) * (targetButtPos - self.bodyChunks[1].pos).normalized;

        PlayerData revivingData = Data(reviving);
        int difference = self.room.game.clock - revivingData.lastCompression;

        if (data.animTime < 0) {
            data.PreparedToGiveCpr();
        }
        else if (self.input[0].pckp && !self.input[1].pckp && difference > 4) {
            PerformCompression(self, grasp, data, reviving, revivingData, difference);
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
    }

    private static void PerformCompression(Player self, int grasp, PlayerData data, Player reviving, PlayerData revivingData, int difference)
    {
        // Don't let piggybacked scugs interfere with compression
        if (self.slugOnBack != null) {
            self.slugOnBack.interactionLocked = true;
            self.slugOnBack.counter = 0;
        }

        // Make sure we're holding their chest
        if (self.grasps[grasp].chunkGrabbed == 1) {
            self.grasps[grasp].chunkGrabbed = 0;
        }

        // Releases spears stuck in corpse
        for (int i = reviving.abstractCreature.stuckObjects.Count - 1; i >= 0; i--) {
            if (reviving.abstractCreature.stuckObjects[i] is AbstractPhysicalObject.AbstractSpearStick stick && stick.A.realizedObject is Spear s) {
                s.ChangeMode(Weapon.Mode.Free);
            }
        }

        data.StartCompression();
        self.AerobicIncrease(0.5f);
        
        // Medic needs to keep track of this for timing reasons regardless of online play or not
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
        
        // Medic also needs to keep track of this for timing
        revivingData.lastCompression = self.room.game.clock;
        
        revivingData.deathTime -= healing;
        healing *= Options.ReviveSpeed.Value;

        if (revivingData.waterInLungs > 0) {
            revivingData.waterInLungs -= healing * 0.34f;

            float amount = data.compressionDepth * 0.5f + UnityEngine.Random.value - 0.5f;
            for (int i = 0; i < amount; i++) {
                Vector2 dir = Custom.RotateAroundOrigo(new Vector2(0, 1), -30f + 60f * UnityEngine.Random.value);
                reviving.room.AddObject(new WaterDrip(reviving.firstChunk.pos + dir * 10, dir * (2 + 4 * UnityEngine.Random.value), true));
            }
        }
        
        bool isAI = self.AI != null && self.isNPC;
        if (!isAI) return;
        
        // This might not actually work assuming it needs the Host to apply the settings. Should only apply to slugpups or other AI
        reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceLike(10f);
        reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceTempLike(10f);
        reviving.State.socialMemory.GetOrInitiateRelationship(self.abstractCreature.ID).InfluenceKnow(0.5f);
    }
    
    private void RespondToMedic(Player self, Player medic)
    {
        /*
         * Since we're running the revival locally, we have to run a lot of the same checks
         * as the medic to ensure we can actually be revived, otherwise the medic carrying us
         * over a pole and spamming grab might trigger the revival for ourself, etc... so we
         * need a bit of redundancy to make sure there's no mistaken revivals. We also have
         * to make sure we don't rely on any of the data from the medic, we have to infer all
         * of the data locally since we can't see the medic's PlayerData
         */
        
        if (!CanRevive(medic, self, false)) return;
        
        PlayerData myData = Data(self);
        PlayerData medicData = Data(medic);

        // Calculate timing
        int difference = self.room.game.clock - myData.lastCompression;

        // Detect if the medic is grabbing (performing CPR) and respond accordingly
        if (medic.input[0].pckp && !medic.input[1].pckp && difference > 4)
        {
            ReceiveCompression(self, myData, medicData, difference);
        }
    }
    
    private static void ReceiveCompression(Player reviving, PlayerData revivingData, PlayerData medicData, int difference)
    {
        // Releases spears stuck in corpse
        for (int i = reviving.abstractCreature.stuckObjects.Count - 1; i >= 0; i--) {
            if (reviving.abstractCreature.stuckObjects[i] is AbstractPhysicalObject.AbstractSpearStick stick && stick.A.realizedObject is Spear s) {
                s.ChangeMode(Weapon.Mode.Free);
            }
        }
        
        revivingData.compressionsUntilBreath--;
        if (revivingData.compressionsUntilBreath < 0) {
            revivingData.compressionsUntilBreath = 1000;
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
        medicData.compressionDepth = difference switch {
            < 8 => 0.2f,
            < 19 => 1f,
            < 22 => 4.5f,
            < 30 => 3.5f,
            _ => 1f
        };

        // ReviveSpeed will be based on the corpse player's setting in Meadow
        healing *= Options.ReviveSpeed.Value;

        revivingData.deathTime -= healing;
        
        revivingData.lastCompression = reviving.room.game.clock;

        // Handle water in lungs
        if (revivingData.waterInLungs > 0) {
            revivingData.waterInLungs -= healing * 0.34f;

            float amount = UnityEngine.Random.value - 0.5f;
            for (int i = 0; i < amount; i++) {
                Vector2 dir = Custom.RotateAroundOrigo(new Vector2(0, 1), -30f + 60f * UnityEngine.Random.value);
                reviving.room.AddObject(new WaterDrip(reviving.firstChunk.pos + dir * 10, dir * (2 + 4 * UnityEngine.Random.value), true));
            }
        }
        
        // if ReceiveCompression is called, it's assumed we're in the middle of a compression (CompressionDown) so let's animate that based on our locally calculated medic compressionDepth
        // Push reviving person's head and butt upwards
        CorpseAnimateCompression(reviving, medicData);
    }


    private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
    {
        orig.Invoke(self);

        PlayerData data = Data(self.player);

        if (self.player.grasps.FirstOrDefault(g => g?.grabbed is Player)?.grabbed is not Player reviving) {
            return;
        }

        AnimationStage stage = data.Stage();

        if (stage == AnimationStage.None) return;

        Vector2 starePos = stage is AnimationStage.Prepared or AnimationStage.CompressionDown or AnimationStage.CompressionUp or AnimationStage.CompressionRest
            ? HeartPos(reviving)
            : reviving.firstChunk.pos + reviving.firstChunk.Rotation * 5;

        self.LookAtPoint(starePos, 10000f);

        // Corpses have to handle their own animations locally, but the medic needs to simulate them to see it as well
        if (stage is AnimationStage.CompressionDown) {
            CorpseAnimateCompression(reviving, data);
        }
    }
    
    private void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig.Invoke(self, sLeaser, rCam, timeStacker, camPos);
        
        var player = self.player;
        if (player == null) return;

        // Functionally doesn't work as expected, may need to abandon
        ApplyPlayerVisualDecay(self);

        // just a sanity check, implemented while trying to figure all this out
        if (sLeaser?.sprites is not { Length: > 9 }) return;

        if (player.grabbedBy?.Count == 1 && player.grabbedBy[0].grabber is Player medic &&
            CanRevive(medic, player, false) && Data(medic).animTime >= 0)
        {
            sLeaser.sprites[9].y += 6;
            sLeaser.sprites[3].rotation -= 50 * Mathf.Sign(sLeaser.sprites[3].rotation);
            sLeaser.sprites[3].scaleX *= -1;

            // Somehow need to switch "headnumber" to 7 here in order to make the mod 1:1
        }

        if (sLeaser.sprites[9]?.element?.name == "FaceDead" && Data(player).deathTime < -0.6f)
        {
            sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceStunned");
        }
    }

    private void SlugcatHand_Update(On.SlugcatHand.orig_Update orig, SlugcatHand self)
    {
        orig.Invoke(self);
        
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

    private static void ApplyPlayerVisualDecay(PlayerGraphics self)
    {
        PlayerData data = Data(self.player);
        
        float visualDecay = Mathf.Max(Mathf.Clamp01(data.deathTime), Mathf.Clamp01((float)data.deaths / Options.DeathsUntilExhaustion.Value) * 0.6f);
        if (self.malnourished < visualDecay) {
            self.malnourished = visualDecay;
        }
    }

    private static void CorpseAnimateCompression(Player self, PlayerData data)
    {
        PlayerGraphics graf = G(self);
        graf.head.vel.y += data.compressionDepth * 0.5f;
        graf.NudgeDrawPosition(0, new(0, data.compressionDepth * 0.5f));
        if (graf.tail.Length > 1) {
            graf.tail[0].pos.y += 1;
            graf.tail[0].vel.y += data.compressionDepth * 0.8f;
            graf.tail[1].vel.y += data.compressionDepth * 0.2f;
        }
    }
}
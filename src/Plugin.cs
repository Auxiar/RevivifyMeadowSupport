using BepInEx;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;
using static RevivifyMeadowSupport.Helpers;

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

    // Prevent GetMalnourished from dangling later (IDE kept yelling at me about it)
    private Hook _getMalnourishedHook;
    
    private static bool CanRevive(Player medic, Player reviving, bool usingProximity)
    {
        // Check common checks once
        if (medic == reviving || medic.room == null || reviving.room == null || medic.room != reviving.room || medic.enteringShortCut != null
            || medic.inShortcut || reviving.enteringShortCut != null || reviving.inShortcut || reviving.playerState.permaDead
            || !reviving.dead || Data(reviving).Expired || (Data(reviving).deaths >= Options.DeathsUntilExpire.Value && Options.DeathsUntilExpire.Value > 0))
            return false;
        
        // Daimyo's Proximity implementation check
        if (usingProximity || (Options.AllowPupsToReviveYou.Value && medic.isSlugpup))
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
        // Air in lungs needed to be improved, if drowned and revived through proximity, you'd almost always immediately drown again
        self.airInLungs = 0.35f;
        self.exhausted = true;
        self.aerobicLevel = 1;
        
        self.playerState.permanentDamageTracking = Options.DeathsUntilExhaustion.Value == 0 ? 0 : Mathf.Clamp01((float)Data(self).deaths / Options.DeathsUntilExhaustion.Value) * 0.6;
        self.playerState.alive = true;
        self.playerState.permaDead = false;
        self.dead = false;
        self.killTag = null;
        self.killTagCounter = 0;
        self.abstractCreature.abstractAI?.SetDestination(self.abstractCreature.pos);

        // Borrowed from the MouseDrag revive function, thanks Maxi and Sekken!
        // https://github.com/woutkolkman/mousedrag/tree/master
        ResetGameOver(self);
    }

    public void OnEnable()
    {
        //On.RainWorld.Update += ErrorCatch;
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
        //On.RainWorld.Update -= ErrorCatch;
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

    private readonly Func<Func<Player, bool>, Player, bool> _getMalnourished = (orig, self) => orig.Invoke(self) || (Data(self).deaths >= Options.DeathsUntilExhaustion.Value && Options.DeathsUntilExhaustion.Value > 0);

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
        
        // fabled arena toggle!
        if (DisableInArena(self)) return orig.Invoke(self, pickUpCandidate);
        
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
        return orig.Invoke(self, pickUpCandidate) || (Options.ReviveMode.Value == "CPR" && (pickUpCandidate != null && self.slugOnBack != null && !Data(pickUpCandidate).Expired));
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

    private static void FixFoodMeter(On.HUD.FoodMeter.orig_GameUpdate orig, HUD.FoodMeter self)
    {
        orig.Invoke(self);

        if (self.IsPupFoodMeter) {
            self.survivalLimit = self.pup.slugcatStats.foodToHibernate;
        }
    }
    
    // Moved these out of the UpdatePlr function to not constantly be redeclaring them
    private const int TicksToDie = 40 * 30; // 30 seconds
    private const int TicksToRevive = 40 * 10; // 10 seconds

    private void UpdatePlr(On.Player.orig_Update orig, Player self, bool eu)
    {
        orig.Invoke(self, eu);
        
        // fabled arena toggle!
        if (DisableInArena(self)) return;
        
        Room room = self.room;
        if (room == null) return;
        
        PlayerData data =  Data(self);
        
        // puts slugpups into comas
        if (self.isSlugpup && data.deaths >= Options.DeathsUntilComa.Value && Options.DeathsUntilComa.Value > 0) self.stun = 100;

        // simplify deathTime ref
        ref float death = ref data.deathTime;
        
        // Run the expiry check for both once and revive if shelter door is closing
        if (self.dead)
        {
            if (death > 0.1f)
            {
                data.expireTime++;
            }
            else
            {
                data.expireTime = 0;
            }
            
            bool beingRevived = false;
            
            // If we're near life and not in direct danger, keep reviving organically
            if (death < -0.55f && self.dangerGrasp == null)
            {
                death -= 1f / TicksToRevive;
                beingRevived = true;
            } 

            // If player wants to use proximity revive or wants pups to revive them
            if ((Options.ReviveMode.Value == "Proximity" || Options.AllowPupsToReviveYou.Value) && (IsLocal(self) || IsAI(self)))
            {
                foreach (AbstractWorldEntity entity in room.abstractRoom.entities)
                {
                    if (entity is not AbstractCreature { realizedCreature: Player player }) continue;
                    bool medicIsPup = player.isSlugpup;
                    beingRevived = CanRevive(player, self, true) && self.dangerGrasp == null;
                    if (Options.AllowPupsToReviveYou.Value) beingRevived = beingRevived && medicIsPup;
                    if (!beingRevived) continue;
                    // Let pups revive faster
                    float reviveAmount = Options.ReviveSpeed.Value * 1.6f / TicksToRevive;
                    reviveAmount *= medicIsPup ? 1.5f : 1;
                    
                    string pupOrNot = medicIsPup ? "Pup" : "Player";
                    Log($"{pupOrNot} is proximity reviving you! Score: {reviveAmount}");
                    
                    death -= reviveAmount;
                    break;
                }
            }
            
            // Running the CPR revival check on the client always ensures that CPR can be performed to speed up revival for proximity revive players
            if (self.grabbedBy.FirstOrDefault()?.grabber is Player medic) RespondToMedic(self, medic);

            // Revive if we're in a shelter, the door is closing, and we're not in the mouth of something dangerous
            if (room.shelterDoor is { IsClosing: true } && self.dangerGrasp == null) death = -1.1f;

            switch (death)
            {
                case < -1 when IsLocal(self) || IsAI(self):
                {
                    // Only call revive player on self - medic has no authority to call it
                    RevivePlayer(self);
                    
                    // BUG: doesn't really work as expected, never fires even in SP
                    // tell reviver to throw us because we're alive (assumes singleplayer/jolly)
                    if (self.grabbedBy.FirstOrDefault()?.grabber is Player p) {
                        p.ThrowObject(self.grabbedBy[0].graspUsed, eu);
                    }

                    break;
                }
                case >= -0.3f when !beingRevived:
                    death += 1f / TicksToDie;
                    break;
            }
        }
        else
        {
            death = 0;
        }
        
        // Clamp deathTime value between -1 or 1
        death = Mathf.Clamp(death, -1, 1);
        
        // Water in lungs particle spawn check
        if (data.waterInLungs > 0 && UnityEngine.Random.value < 1 / 40f && self.Consious) {
            data.waterInLungs -= UnityEngine.Random.value / 4f;
    
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
        if (data.deaths < Options.DeathsUntilExhaustion.Value || Options.DeathsUntilExhaustion.Value <= 0) return;
        
        if (self.isSlugpup) {
            self.slugcatStats.foodToHibernate = self.slugcatStats.maxFood;
        }
            
        switch (self.aerobicLevel)
        {
            case >= 1f:
                data.exhausted = true;
                break;
            case < 0.3f:
                data.exhausted = false;
                break;
        }

        // exhausted logic
        if (data.exhausted) ApplyExhaustion(self);
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
        if (DisableInArena(self))
        {
            orig.Invoke(self, actuallyViewed, eu);
            return;
        }
        
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
        if (DisableInArena(self))
        {
            orig.Invoke(self, eu);
            return;
        }
        
        try {
            orig.Invoke(self, eu);
            /*
             * This if statement is used in place of the old IL PlayerGrabUpdate hook, which was causing the corpse
             * to slide when using the proximity revive method. To me, it just seemed unnecessary, and so it's been
             * deprecated, and the old FixHeavyCarry() function has been renamed to Player_GrabUpdate() to be consistent
             * with the naming conventions and how functions are connected (using On instead of IL)
             */
            PlayerData data = Data(self);
            
            bool canReviveCorpse = false;
            
            foreach (Creature.Grasp grasp in self.grasps)
            {
                // moved this out here to simplify/slightly optimize checks (maybe)
                if (grasp?.grabbed is not Player corpse) continue;
                canReviveCorpse = CanRevive(self, corpse, false);
                UpdateRevive(self, corpse, self.grasps.IndexOf(grasp));
                break;
            }

            if (!canReviveCorpse)
            {
                data.Unprepared();
            }
            
            _disableHeavyCarry = true;
        }
        finally {
            _disableHeavyCarry = false;
        }
    }
    
    // I have very little understanding of what this method is actually doing, so whether it is functioning correctly or not is beyond me
    private bool FixHeavyCarry(On.Player.orig_HeavyCarry orig, Player self, PhysicalObject obj)
    {
        return orig.Invoke(self, obj) &&
               (DisableInArena(self) ||
                !(_disableHeavyCarry &&
                  obj is Player p &&
                  CanRevive(self, p, Options.ReviveMode.Value == "Proximity"))) ;
    }
    
    private void UpdateRevive(Player medic, Player corpse, int grasp)
    {
        PlayerData medicData = Data(medic);

        Vector2 heartPos = HeartPos(corpse);
        Vector2 targetHeadPos = heartPos + new Vector2(0, Mathf.Sign(medic.room.gravity)) * 25;
        Vector2 targetButtPos = heartPos - new Vector2(0, corpse.bodyChunks[0].rad);
        float headDist = (targetHeadPos - medic.bodyChunks[0].pos).magnitude;
        float buttDist = (targetButtPos - medic.bodyChunks[1].pos).magnitude;

        if (medicData.animTime < 0 && (headDist > 22 || buttDist > 22)) {
            return;
        }

        medic.bodyChunks[0].vel += Mathf.Min(headDist, 0.4f) * (targetHeadPos - medic.bodyChunks[0].pos).normalized;
        medic.bodyChunks[1].vel += Mathf.Min(buttDist, 0.4f) * (targetButtPos - medic.bodyChunks[1].pos).normalized;

        PlayerData revivingData = Data(corpse);
        int difference = medic.room.game.clock - revivingData.lastCompression;

        if (medicData.animTime < 0) {
            medicData.PreparedToGiveCpr();
        }
        else if (medic.input[0].pckp && !medic.input[1].pckp && difference > 4) {
            Compression(medic, corpse, medicData, Data(corpse), difference, grasp);
        }

        AnimationStage stage = medicData.Stage();

        switch (stage)
        {
            case AnimationStage.CompressionDown:
                medic.bodyChunkConnections[0].distance = 13 - medicData.compressionDepth;
                break;
            case AnimationStage.CompressionUp:
                medic.bodyChunkConnections[0].distance = Mathf.Lerp(13 - medicData.compressionDepth, 15, (medicData.animTime - 3) / 2f);
                break;
            default:
                medic.bodyChunkConnections[0].distance = 14;
                break;
        }

        if (medicData.animTime > 0) {
            medicData.animTime++;
        }
        if (Data(corpse).compressionsUntilBreath > 0) {
            if (medicData.animTime >= 20)
                medicData.PreparedToGiveCpr();
        }
        else if (medicData.animTime >= 80) {
            medicData.PreparedToGiveCpr();
        }
    }

    // Combined perform/receive compression into a singular function - less chance for behavior to desync between clients
    // Also removed a lot of the authority checks - theoretically for the best experience, everything should be simulated between the two
    private static void Compression(Player medic, Player corpse, PlayerData medicData, PlayerData corpseData, int difference, int grasp = 0)
    {
        // Don't let piggybacked scugs interfere with compression
        LockPupInteraction(medic);
        // Make sure we're holding their chest
        GrabChest(medic, grasp);
        // Releases spears stuck in corpse (if there happens to be any, usually we grab them out first)
        ReleaseSpears(corpse);

        medicData.StartCompression();
        medic.AerobicIncrease(0.5f);
        
        corpseData.compressionsUntilBreath--;
        if (corpseData.compressionsUntilBreath < 0) {
            corpseData.compressionsUntilBreath = 1000;//(int)(8 + UnityEngine.Random.value * 5);
        }

        bool breathing = corpseData.compressionsUntilBreath == 0;
        
        float healing = HealingMath(breathing, difference);

        // normalize compression amount to 0-1 for score
        Log($"Compression Happened! Score: {healing * 100}");
        
        medicData.compressionDepth = CompressionDepthMath(difference);
        
        if (medicData.compressionDepth > 4) medic.Blink(6);
        
        healing *= Options.ReviveSpeed.Value;
        corpseData.deathTime -= healing;
        
        corpseData.lastCompression = medic.room.game.clock;

        HandleWaterInLungs(corpse, corpseData, medicData, healing);
        
        CorpseAnimateCompression(corpse, medicData);
            
        // Since the corpse is receiving, they should have authority over their state (hopefully) - otherwise the host needs to set this for it to propogate
        corpse.State.socialMemory.GetOrInitiateRelationship(medic.abstractCreature.ID).InfluenceLike(10f);
        corpse.State.socialMemory.GetOrInitiateRelationship(medic.abstractCreature.ID).InfluenceTempLike(10f);
        corpse.State.socialMemory.GetOrInitiateRelationship(medic.abstractCreature.ID).InfluenceKnow(0.5f);
    }

    private static void HandleWaterInLungs(Player corpse, PlayerData corpseData, PlayerData medicData, float healing)
    {
        if (corpseData.waterInLungs <= 0) return;
        
        corpseData.waterInLungs -= healing * 0.34f;

        float amount = medicData.compressionDepth * 0.5f + UnityEngine.Random.value - 0.5f;
        for (int i = 0; i < amount; i++) {
            Vector2 dir = Custom.RotateAroundOrigo(new Vector2(0, 1), -30f + 60f * UnityEngine.Random.value);
            corpse.room.AddObject(new WaterDrip(corpse.firstChunk.pos + dir * 10, dir * (2 + 4 * UnityEngine.Random.value), true));
        }
    }
    
    private void RespondToMedic(Player corpse, Player medic)
    {
        /*
         * Since we're running the revival locally, we have to run a lot of the same checks
         * as the medic to ensure we can actually be revived, otherwise the medic carrying us
         * over a pole and spamming grab might trigger the revival for ourself, etc... so we
         * need a bit of redundancy to make sure there's no mistaken revivals. We also have
         * to make sure we don't rely on any of the data from the medic, we have to infer all
         * of the data locally since we can't see the medic's PlayerData
         */

        if (!CanRevive(medic, corpse, false)) return;
        
        PlayerData corpseData = Data(corpse);
        PlayerData medicData = Data(medic);

        // Calculate timing
        int difference = corpse.room.game.clock - corpseData.lastCompression;

        // Detect if the medic is grabbing (performing CPR) and respond accordingly
        if (medic.input[0].pckp && !medic.input[1].pckp && difference > 4)
        {
            Compression(medic, corpse, medicData, corpseData, difference);
        }
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

        if (DisableInArena(player)) return;

        // Functionally isn't replicated over the network by Meadow, still applies locally
        ApplyPlayerVisualDecay(self);

        // just a sanity check, implemented while trying to figure all this out
        if (sLeaser?.sprites is not { Length: > 9 }) return;

        // Check to see if we being revived/being compressed
        if (player.grabbedBy?.Count == 1 && player.grabbedBy[0].grabber is Player medic &&
            CanRevive(medic, player, false) && Data(medic).animTime >= 0)
        {
            sLeaser.sprites[9].y += 6;
            sLeaser.sprites[3].rotation -= 50 * Mathf.Sign(sLeaser.sprites[3].rotation);
            sLeaser.sprites[3].scaleX *= -1;

            // Sets face to eyes-closed instead of x/dead eyes - testing isLocal to see if Meadow replicates it for us
            if (IsLocal(player))
            {
                sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName(
                    self.DefaultFaceSprite(sLeaser.sprites[9].scaleX, 7)
                );   
            }
        }

        // We're going to try only changing the face at the local level and see if Meadow replicates it for us
        if (sLeaser.sprites[9]?.element?.name == "FaceDead" && Data(player).deathTime < -0.6f && IsLocal(player))
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
        
        float visualDecay = Mathf.Max(Mathf.Clamp01(data.deathTime), Mathf.Clamp01((float)data.deaths / (Options.DeathsUntilExhaustion.Value == 0 ? 999 : Options.DeathsUntilExhaustion.Value)) * 0.6f);
        if (self.malnourished < visualDecay) {
            self.malnourished = visualDecay;
        }
    }
}
using System;
using RWCustom;
using UnityEngine;

namespace RevivifyMeadowSupport;

sealed class Helpers
{
    public static void Log(string text)
    {
        UnityEngine.Debug.Log(text);
    }

    public static bool IsLocal(Player p) => p.controller == null;
    public static bool IsAI(Player p) => p.AI != null && p.isNPC;
    public static PlayerGraphics G(Player p) => p.graphicsModule as PlayerGraphics;

    public static Vector2 HeartPos(Player p) => Vector2.Lerp(p.firstChunk.pos, p.bodyChunks[1].pos, 0.38f) + new Vector2(0, 0.7f * p.firstChunk.rad);
    
    public static float HealingMath(bool breathing, int difference) => difference switch {
        < 75 when breathing => -1 / 10f,
        < 100 when breathing => 1 / 5f,
        < 8 => -1 / 30f,
        < 19 => 1 / 40f,
        < 22 => 1 / 15f,
        < 30 => 1 / 20f,
        _ => 1 / 40f,
    };

    public static float CompressionDepthMath(int difference) => difference switch {
            < 8 => 0.2f,
            < 19 => 1f,
            < 22 => 4.5f,
            < 30 => 3.5f,
            _ => 1f
    };
    
    public static void LockPupInteraction(Player self)
    {
        if (self.slugOnBack == null) return;
        
        self.slugOnBack.interactionLocked = true;
        self.slugOnBack.counter = 0;
    }

    public static void GrabChest(Player self, int i)
    {
        if (self.grasps[i].chunkGrabbed == 1) self.grasps[i].chunkGrabbed = 0;
    }

    public static bool DisableInArena(Player self)
    {
        // Automatically disable functionality in Arena mode if desired
        Room room = self.room;
        if (room == null) return false;
        
        // If we're not in an arena, we can return early
        bool inArena = room.game.IsArenaSession;
        if (!inArena) return false;
        
        bool inSandbox = room.game.GetArenaGameSession.arenaSitting.sandboxPlayMode;

        // Little tricky, at this point, we only want to know if we're in an actual arena (competitive) game.
        // Since we return early if we are not in an arena, we know we can only be in competitive (or challenge, etc...) if we are not in sandbox.
        inArena = !inSandbox;
        
        return
            (Options.DisableInArena.Value && inArena) ||
            (Options.DisableInSandbox.Value && inSandbox);
    }

    public static void ReleaseSpears(Player self)
    {
        // Releases spears stuck in corpse
        for (int i = self.abstractCreature.stuckObjects.Count - 1; i >= 0; i--) {
            if (self.abstractCreature.stuckObjects[i] is AbstractPhysicalObject.AbstractSpearStick stick && stick.A.realizedObject is Spear s) {
                s.ChangeMode(Weapon.Mode.Free);
            }
        }
    }

    public static void ApplyExhaustion(Player self)
    {
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

    public static void ResetGameOver(Player self)
    {
        Room room = self.room;
        if (room == null) return;
        
        RainWorldGame game = self.room.game;
        if (game == null) return;
        
        //campaign
        if (game.cameras == null) return;
        foreach (RoomCamera cam in game.cameras)
        {
            if (cam.hud?.textPrompt == null) continue;
            cam.hud.textPrompt.gameOverMode = false;
        }

        //sandbox & challenges
        if (game.arenaOverlay == null || game.session is not ArenaGameSession ags) return;
        
        game.arenaOverlay.ShutDownProcess();
        game.manager?.sideProcesses?.Remove(game.arenaOverlay);
        game.arenaOverlay = null;
        ags.sessionEnded = false;
        ags.challengeCompleted = false;
        ags.endSessionCounter = -1;
    }

    public static void CorpseAnimateCompression(Player corpse, PlayerData medicData)
    {
        PlayerGraphics g = G(corpse);
        g.head.vel.y += medicData.compressionDepth * 0.5f;
        g.NudgeDrawPosition(0, new(0, medicData.compressionDepth * 0.5f));
        
        if (g.tail.Length <= 1) return;
        
        g.tail[0].pos.y += 1;
        g.tail[0].vel.y += medicData.compressionDepth * 0.8f;
        g.tail[1].vel.y += medicData.compressionDepth * 0.2f;
    }
}
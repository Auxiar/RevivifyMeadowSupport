# Fixing Dual's Revivify Mod to Work With Rain Meadow While Adding Daimyo's Proximity-Based Revival as an Option
___

~~Phew, what a mouthful.~~


## This mod aims to resolve any conflicts between Rain Meadow, the Rain World Multiplayer Framework (and more) mod and Dual's original CPR-based Revivify mod.

While attempting to play Rain Meadow, I had an issue where the original Revivify mod by Dual would sometimes work, sometimes not, and was disappointed by that. I was also disappointed by the lack of updates, but ultimately I understand that much. After a point there's only so much more that you can do to a mod before you have to call it done, and even then keeping it updated with the game becomes a hassle.

An attempt to make it Meadow-compatible was made by a user of the name Daimyo, and as far as I'm aware, that worked just fine. But unfortunately, they also fundamentally changed the way that the mod worked, switching from a CPR-based revival method to a proximity-based one. That might be fine for some people, but I wasn't happy with it.

So I underwent the process of figuring out how to make Dual's original CPR-based Revivify mod Rain Meadow compatible. Ultimately, it seems like it's just a few functions revolving around updating the player's sprites, and I believe I've resolved those issues just fine. However, I also noticed that Daimyo hasn't updated their proximity revivify mod in a while either, once again leaving Revivify fans without any updates for current versions.

Realizing that there are probably some people who prefer the proximity-based revival over the CPR version, I took the time to try and combine the two methods in the now Rain Meadow compatible mod. Ultimately it was pretty easy, there was just a few tricky bits surrounding Diamyo's original implementation, as it ***seemed*** like the time to revive was based on your framerate instead of on a constant time-based function, and a few other tweaks and edits from the original as well. I attempted to rectify this and also brought the time to revive a player more in-line with how long it might take to perform CPR on them, so I hope that everyone can appreciate and enjoy these changes, especially online with their friends.

### A full list of any/all changes made in this version of the mod:
* Now works with Rain Meadow (in all supported modes, load **before** Rain Meadow, or **under** it in the Remix list)
* Toggle between Daimyo's Proximity-based revival method, or Dual's original CPR-based revival method in the Remix menu
* Toggle whether or not corpses/comatose slugs can be piggy-backed in the Remix menu (doesn't seem to apply to player corpses in Meadow unfortunately)
* Daimyo's proximity-based revival has been modified to work as follows:
  * Revive Speed Multiplier Remix option has been made to function the same as in the original mod (higher is faster)
  * No longer based on framerate (hopefully, close enough anyways)
  * Default time to revive tuned to be more in-line with CPR-based time to revive
  * Leaving the proximity causes the dead to become deader (the longer the corpse is dead, the longer they'll take to revive)
  * Entering the proximity causes the dead to become more alive until they are considered alive and revive
  * Corpses should automatically be thrown away once revived
  * I don't think corpse expiration was implemented on Daimyo's implementation, this has been reimplemented (can be disabled with the Remix setting)

### Some notes/known issues:
* In my testing, Revivify - Meadow Fix has to load **before** Rain Meadow, this means it must go **under** it in the Remix Menu
* Due to what was causing the break in Meadow support, some of the facial expressions might not function exactly like they did in either mod
* The Corpse piggyback setting only applies in Singleplayer/Jolly Co-op, as Meadow has its own piggyback rules that don't support corpses  

Other than the mentioned notes, it should be a pretty similar experience regardless of which mod you're more familiar with using. I've done my best to make them as 1:1 as possible, and while I wasn't able to get 100% of the way there, I'd call it about 90-95%. Either way, I hope you enjoy the work that's gone into it!

### Installation:
You can find this mod on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3632170621) if you'd prefer to install it that way, but if you'd like to install it manually, you can download this repository as a zip file and extract the contents into your `[RainWorldInstallDirectory]/RainWorld_Data/StreamingAssets/mods/` folder. Structure should look something like this, everything not listed here is technically optional! You only need the `plugins` folder, `modinfo.json`, and `thumbnail.png` files inside the folder for the mod for your game to correctly detect the mod:
```
mods/
├─ devtools/
├─ expedition/
├─ ...
└─ Revivify-MeadowFix/
   ├─ plugins/
   │  └─ newest/
   │     └─ RevivifyMeadowFix.dll
   ├─ modinfo.json
   └─ thumbnail.png
```

## Apart from the listed changes, the majority of the code is from the original Revivify mod by Dual, source code can be found on [Github](https://github.com/Dual-Iron/revivify/tree/master). Any code from Daimyo's version of the mod came from decompiling the version found on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3396726904). My contributions are minimal at best, and the majority of the credit should go towards Dual and Daimyo.
## I'm not asking for any recognition or credit for the code or what it attempts to accomplish, I just wanted the original Revivify mod to be compatible with Rain Meadow. I intend to keep the mod updated with game versions, but ideally it should be pretty version agnostic.

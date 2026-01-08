# Fixing Dual's Revivify Mod to Work With Rain Meadow While Adding Daimyo's Proximity-Based Revivify as an Option
~~Phew, what a mouthful.~~
___

## This mod aims to resolve any conflicts between Rain Meadow, the Rain World Multiplayer Framework (and more) mod and Dual's original CPR-based Revivify mod, and tack on Daimyo's proximity revival as an option.

While attempting to play Rain Meadow, I noticed that Dual's original Revivify mod just didn't work with it. When attempting to CPR my brother or friends, the client would simply not respond. I was also disappointed by the lack of updates over the last two years, but seeing as how it still worked flawlessly in singleplayer, I can understand why it hasn't been. After a point there's only so much more that you can do to a mod before you have to call it done, and even then keeping it updated with the game becomes a hassle, especially if it's functionally unnecessary.

Eventually, an attempt to make it Meadow-compatible was made by a user of the name Daimyo, and as far as I'm aware, that worked just fine. But unfortunately, they also fundamentally changed the way that the mod worked, switching from a CPR-based revival method to a proximity-based one. That might be fine for some people, but I wasn't happy with it, and it also stopped receiving updates after its initial release.

So I underwent the process of figuring out how to make Dual's original CPR-based Revivify mod Rain Meadow compatible. Ultimately, it required deprecating a few IL hook functions which were directly conflicting with Meadow, and then restructuring a bit of the mod so that the corpse had authority over when it revived based on the actions (or proximity) of the medic character attempting to revive them. So, Dual's CPR now works in Meadow, and Daimyo's proximity is bundled on as a toggleable remix option.

Because of *how* Dual's Revivify needed to be fixed, it came with a few interesting side effects I didn't see coming until near the end of writing it all up. Since it's purely a client-side mod, it does not require other players to have the mod in order for ***you*** to be revivable. Players without the mod can simply grab hold of your corpse, tap the grab button as if they were performing CPR, and while they might be missing the animation for it, you will begin to revive exactly as expected. Same goes for proximity, as long as you have it enabled, a player only needs to stand next to you for a certain time in order for your character to magically raise from the dead. All settings for proximity, proximity distance, revive speed, etc... are all based on the corpse being revived's settings (or, if you're reviving an NPC/Slugpup, whoever is doing the reviving). I've also decided to allow CPR even if the corpse prefers Proximity as a way to speed up the proximity revive (even though it doesn't take very long by default), since isolating the two different logics was already cumbersome by this point.
___
### You can find all the details about the mod on its [Steam Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=3632170621) and can install it from there  

Other than the mentioned notes, it should be a pretty similar experience regardless of which mod you're more familiar with using. I'm aware that this mod could be used to cheat in Meadow story or arena lobbies, and I encourage you to consider adding the mod to your BannedMods or HighImpactMods list accessible from the Rain Meadow Remix Menu if you're planning on hosting open servers, and I will communicate with the Rain Meadow team about potentially adding it as a default to one of these lists. Either way, I hope you enjoy the work that's gone into it!
___
### If you'd like to leave a review of the mod's features, suggest new features, or submit a bug report, you can do so in this [Google Form](https://docs.google.com/forms/d/e/1FAIpQLSckQoZlJm6t8fAAI-6n1Blb13eVsfMALTdLg8wiONRBxIC5Ag/viewform?usp=header). If you don't have an answer for a question then feel free to leave it blank.
___
### Installation:
You can find this mod on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3632170621) if you'd prefer to install it that way, but if you'd like to install it manually, you can download this repository as a zip file and extract the contents into your `[RainWorldInstallDirectory]/RainWorld_Data/StreamingAssets/mods/` folder, structured like below. Everything not listed here is technically optional! You only need the `plugins` folder, `modinfo.json`, and `thumbnail.png` files inside the folder for the mod for your game to correctly detect the mod:

```
mods/
├─ devtools/
├─ expedition/
├─ ...
└─ Revivify-MeadowSupport/
   ├─ plugins/
   │  └─ newest/
   │     └─ RevivifyMeadowSupport.dll
   ├─ modinfo.json
   └─ thumbnail.png
```
___
## Apart from the listed changes, the majority of the code is from the [Original Revivify](https://steamcommunity.com/sharedfiles/filedetails/?id=2950327774) mod by Dual, found on [Github](https://github.com/Dual-Iron/revivify). Any code from Daimyo's version of the mod came from decompiling the version found on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3396726904). I added the Meadow support for Dual's CPR method and reorganized/optimized the two methods to work well with each other, but my contributions are minimal at best, and the majority of the credit should go towards Dual and Daimyo for both versions of the original mods. If you like this mod, go like theirs, share the love and all that.
## I'm not asking for any recognition or credit for the mod despite the work put in, I just wanted the original Revivify mod to be compatible with Rain Meadow. I intend to keep the mod updated with game versions and maybe implement highly requested features as options, we'll see.
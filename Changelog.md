# Changelog
## A rolling file containing changes made for each version of the mod being pushed (for as long as I remember to write them). Changes made will be published here and in the built-in Steam changelogs.

---
### Version 1.1:
**User Facing:**
* Changed `Revive With Proximity` checkbox option into: `Your Revival Method` with a dropdown, making it clearer which revival method you are currently using, and making it clearer that it's **your** revival method
* Moved Revive Method option to the top of the options list
* Renamed the obtuse `Deaths until revival isn't possible` option to: `Deaths until permadeath`, I think it gets the picture across more clearly
* Added a `Disable in Arena` toggle - when enabled (checked, on, etc...), the mod stops working in an arena game - defaults to `On`
* Added a `Disable in Sandbox` toggle - mainly for singleplayer or local play, lets you keep the mod disabled in arena, but keep it on in sandbox if you want to for some reason. Defaults to `On`
* Added `Allow Pups to Revive You` toggle - technically they already could if using proximity, this just formalizes it and lets you turn it on when using CPR for revival. Defaults to `On` and uses your settings for proximity range. It's also lets them revive you 50% faster than your revive speed is set to. This does NOT alter their behavior, it just lets them revive you
* Renamed the long `Time until bodies expire, in minutes` option to: `Time until permadeath`, bringing more clarity to what it actually means (hopefully)
* Added functionality to disable most of the settings (death exhaustion, permadeath, etc...) by setting them to 0 in case you don't like them
* Added descriptions to every option to better explain what they do, how they work, let you know you can disable them, etc...
* Proximity settings are now hidden in the menu if `Your Revival Method` option is set to `CPR` - unless you have `Allow Pups to Revive You` turned on, in which case the proximity distance matters for them!
* Faces are now client-driven, meaning no more "asleep" corpses that can't be revived! - problem originally pointed out by ultim8dragon 

**Backend:**
* Combined the perform and receive compression functions into a singular compression function - should've done this from the start, but now behavior should be more consistent between clients
* Split the code a bit into a Helpers.cs file to make code easier to find rather than digging through 800+ lines to find what I need
* Disabled the OnUpdate error catch that was originally implemented by Dual - from my testing, everything works fine, making it an unnecessary strain on the system (even if probably negligible)
* Renamed some vars referring to the players to better tell at a glance who's who in the functions and unify naming convention across the plugin

**Steam Page:**
* Reduced a bunch of words, said a lot less, got to the point more
* Added version section alerting people to the current version as well as where to find the change notes
* Removed the options section, the in-game description is where that all needed to be anyway
* Added a note to check out Revivify Omni by Wonky in case some users would prefer their mod over mine
* Added gifs that demonstrate different situations surrounding the mod, what happens reviving someone who doesn't have it, reviving someone who has it but you don't, etc...

### Fun Fact!
The proximity-based revival already allowed slugpups to revive scugs, including other pups, this whole time! Good luck getting them to stand around you long enough for revival to happen, especially if something killed you and they flee for their own safety 
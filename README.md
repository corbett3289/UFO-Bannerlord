# UFO's Cheat Mods Bundle for Bannerlord [1.4.8] - No War Sails

This branch packages the maintained Bannerlord v1.4.8 update without a War Sails/NavalDLC dependency. It is a separate Workshop module from every War Sails edition and from the maintained v1.4.7 no-War-Sails item. Only one UFO edition may be enabled at a time.

All gameplay features and compatibility fixes are shared with the maintained v1.4.8 edition. The original mod and bundled work remain credited to UFOdestiny and the original authors below.

> **Testing status:** The maintainer uses the War Sails edition, so this no-War-Sails build has not been campaign gameplay-tested. It compiles and passes the isolated compatibility audit without any NavalDLC reference or dependency, but should still be treated as an early public test release.

My original goal was to learn how to make mods. It just so happened that the 'crush through' feature I wanted stopped working, so I decompiled it and combined it with several well-known mods. I noticed that many people were also sad about some wanted features no longer working, so I decided to release this mod directly. 

# Original Mods

I want to give full credit to the original authors of the mods.
- [Bannerlord Cheats Reload](https://www.nexusmods.com/mountandblade2bannerlord/mods/6446)
- [Xorberax's Legacy](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462)
- [Hero Enhancement](https://www.nexusmods.com/mountandblade2bannerlord/mods/4827)
- [招募灭国后的流亡家族 (Recruit Exile Clans)](https://steamcommunity.com/sharedfiles/filedetails/?id=3255329103)
- [Keep Your Daughters](https://www.nexusmods.com/mountandblade2bannerlord/mods/5148)
- [Super Throwing Collection](https://steamcommunity.com/sharedfiles/filedetails/?id=2885230883)
- [loongspear](https://steamcommunity.com/sharedfiles/filedetails/?id=3017866291)
- [拥有《穿透/穿盾/破盾/击倒/爆炸》功能的箭矢 (Super OP Arrows)](https://bbs.mountblade.com.cn/download_1580.html)

# Steam Workshop
- [Original UFO's Cheat Mods Bundle](https://steamcommunity.com/sharedfiles/filedetails/?id=3583201039)
- [Maintained War Sails 1.4.8 edition](https://steamcommunity.com/sharedfiles/filedetails/?id=3781136815)
- [Maintained no-War-Sails 1.4.7 edition](https://steamcommunity.com/sharedfiles/filedetails/?id=3768535938)

# Language Support
- Bundled by this fork: English, Chinese, and a generic fallback.
- Russian and Portuguese community localization files are not bundled, maintained, supported, or playtested by this fork.
- Missing or invalid selected language files fall back to bundled English as of v1.0.7. Original Russian translation credit: [MaG3ro](https://steamcommunity.com/id/MaG3ro).

# Setting Fixes
- As of v1.0.8, disabling `One Hit Kill` or `Party One Hit Kill` in a campaign overrides an older enabled Global value. New campaigns still inherit the Global defaults.
- As of v1.0.9, `Automatically Acquire Both Perk Branches` applies its Player, Clan, and All Heroes scopes correctly. Eligible perks are checked on the campaign-wide daily hero tick, and a campaign's `No One` choice overrides an older Global value.
- As of v1.0.10, forcing enemy troops to `Killed` preserves one unconscious survivor in lordless parties such as bandits, avoiding invalid all-dead rosters during post-battle cleanup.

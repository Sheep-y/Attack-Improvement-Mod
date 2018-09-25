# AIM - Attack Improvement Mod 3.0 Development #
For BATTLETECH 1.2.0

- [Features Overview](#features-overview)
- [Installation](#installation)
- [Configuration](#configuration)
- [Settings](#settings)
  * [User Interface Settings](#user-interface-settings)
  * [Targeting Line and Facing Ring Settings](#targeting-line-and-facing-ring-settings)
  * [Called Shot Settings](#called-shot-settings)
  * [Melee and DFA Settings](#melee-and-dfa-settings)
  * [Individual Modifier Settings](#individual-modifier-settings)
  * [Net Hit Modifier Settings](#net-hit-modifier-settings)
  * [Hit Roll Settings](#hit-roll-settings)
  * [Hit Chance Preview Settings](#hit-chance-preview-settings)
  * [Critical Hit Settings](#critical-hit-settings)
  * [Hit Resolution Settings](#hit-resolution-settings)
  * [Attack Log Settings](#attack-log-settings)
- [Compatibilities](#compatibilities)
- [The Story of AIM](#the-story-of-aim)
- [Learn to Mod](#learn-to-mod)
- [Credits](#credits)


AIM is a BattleTech mod that fixes, enhances, and customise your combat experience, such as coloured facing rings and targeting lines, tune down roll correction, show distance numbers, or a detailed attack log.  The default settings aim to preserve the balance of vanilla game.

This mod does *not* modify game data.  Saves made with this mod on will *not* be affected by disabling or removing this mod.

# Features Overview

**ALL features can be enabled or disabled individually.**

## Bug Fixes and HUD Enhancements ##

* Fix Grey head disease, Multi-Target back out, and 0 hp unit/locations.
* Line of fire fixed and stylised: Dotted = indirect, Cyan = flank, Green = rear.
* Coloured nameplate, facing ring, and floating armour bar.
* Coloured weapon loadout with individual damage, melee damage, and alpha damage.
* Damaged Structure Display fixed and enhanced. Enemy injuries shown.
* Show heat, instability, distance, movement numbers.
* Show ammo count in paper doll hover.
* Show weapon range (in meters) and properties such as +50% crit.
* Show base hit chance in accuracy modifier popup.
* Show mechwarrior stats in hover/right-click hint.
* Post-move to-hit penalties and heat factored in action preview.
* Press F1 to F4 to select mech. Hold Alt to enable friendly fire.
* (Optional) Show Mech Tonnage.
* (Optional) Show Corrected Hit chance and Called Shot Chance.

## Mechanic Enhancements ##

* Unlock hit chance stepping (make odd Gunnery useful).
* Smart indirect fire when direct fire is obstructed.
* Called shots cluster around called mech location.
* Precise hit distribution that improves SRM and MG called shot.
* More melee modifiers, and fixes the absent of stood up penalty.
* Flank and rear attack Bonus.
* Allow net bonus hit modifiers.
* Allow negative height modifier.
* Remove melee position locking.
* Ammo loader AI that balance ammo usage to minimise explosion.
* Auto jettison useless ammo.

## New Critical Hit System ##

* Skip criting the dead mechs.
* Vehicle and turret critical hits.
* Critical hits follow damage transfer.
* Prevent critical hit on locations with intact structure.
* Give normal crit chance to NPC allies.  Enemies adjustable.
* (Optional) Adjust normal critical hit chances and mix/max cap.
* (Optional) Allow Through Armour Critical hit (TAC).
* (Optional) Allow critical hit reroll and location transfer.
* (Optional) Allow multiple critical hits per weapon.

## Other Adjustables ##

* Tabular attack log that can be opened directly in Excel.
* Old attack logs are archived and auto-deleted in background.
* Adjustable roll correction strength, default halved.
* Adjustable miss streak breaker.
* Adjustable base hit chances.
* Adjustable hit chance stepping and min/max cap.
* Adjustable attack modifier list.
* Control display precision of hit chance and called shot chance.


# Installation

1. Install [BTML and ModTek](https://github.com/janxious/ModTek/wiki/The-Drop-Dead-Simple-Guide-to-Installing-BTML-&-ModTek-&-ModTek-mods).
2. [Download this mod](https://github.com/Sheep-y/Attack-Improvement-Mod/releases), extract in the mod folder. i.e. You should see `BATTLETECH\Mods\AttackImprovementMod\mod.json`
3. Launch the game. The mod will creates a "settings.json" and a mod log in the same folder as `mod.json`.
4. Open `settings.json` to see mod settings.  If you want to change it, restart game to apply changes.


# Configuration

When the mod is loaded, it will read `settings.json` and validate its.
If the setting file does not exist, it will be created.

Several presets are bundled with this mod.  You may copy or rename a setting to `settings.json` to apply it.

* `settings.default.json` - Out of box default.  Game is subtly enhanced, such as lower roll correction, hit stepping and melee position unlock, flanking bonus, and critical hit on non-mechs.
* `settings.spartan.json` - Enable diminishing modifier, more bonus and penalties, through armour crit, crit reroll, enemy ammo balancing and jettison, and disables roll correction and streak breaker.
* `settings.ui-enhance.json` - Only enable game fixes and user interface enhancements.  Game mechanic is not changed beyond bug fixes.
* `settings.fix-only.json` - Only enable game fixes for a bug-free, pure vanilla experience.
* `settings.log-only.json` - An old preset that should be deleted.  Use fix-only instead.

Note that `settings.json` is auto-managed.  Old settings will be upgraded and removed, out of range settings will be corrected, and the formats and comments cannot be changed.
You can only change setting values.

This mod is designed to run as fast as it can be.
Disabled features won't slow down the game and does not modify any code, and heavy calculations are cached or optimised.

Because of high number of features and flexibility, bugs may slip through tests.
Please report them on [GitHub](https://github.com/Sheep-y/Attack-Improvement-Mod/issues) or [Nexus](https://www.nexusmods.com/battletech/mods/242?tab=bugs).


# Settings

These settings can be changed in `settings.json`.


## User Interface Settings

**Hotkeys**

> Setting: `FunctionKeySelectPC`  (true/false, default true)
>
> When true, F1 to F4 keys can be used to select player mechs.
>
> The keys will stop to work if they are already binded.
> Also, because the keys are hard-coded and not keybinds, this will not change game settings or game profile.
<br>

> Setting: `AltKeyFriendlyFire`  (true/false, default true)
>
> When true, friendly units may be targeted by attack when pressing Alt.


**Nameplates**

> Setting: `NameplateColourPlayer`  (color string, default "#BFB")<br>
> Setting: `NameplateColourEnemy`  (color string, default "#FBB")<br>
> Setting: `NameplateColourAlly`  (color string, default "#8FF")<br>
> Setting: `FloatingArmorColourPlayer`  (color string, default "#BFB")<br>
> Setting: `FloatingArmorColourEnemy`  (color string, default "#FBB")<br>
> Setting: `FloatingArmorColourAlly`  (color string, default "#8FF")<br>
>
> When non-empty, change colours of nameplate text and armour bars, making it easier to tell friends from foes.



**Paper Dolls**

> Setting: `FixPaperDollRearStructure`  (true/false, default true)
>
> The rear structures of the paper dolls are displayed incorrectly because of a typo in game code.
> When true, fix the bug.
<br>

> Setting: `ShowUnderArmourDamage `  (true/false, default true)
>
> When true, armour of damaged location will have a striped pattern instead of solid.

> Both settings apply to all paper dolls: selection panel, target panel, called shot popup, mech bay, deploy, post-mission report etc.



**Mech Info**

> Setting: `FixHeatPreview`  (true/false, default true)
>
> When true, previewed move destination's terrain will be factored in heat preview, plus any heat effects en-route.
> For example, moving into water will predict more cooldown and vice versa.
<br>

> Setting: `ShowNumericInfo`  (true/false, default true)
>
> When true, display heat, stability, movement, and distance numbers in the selection panel (bottom left) and targeting panel (top center), and predicts post action numbers.
> Prediction numbers are supplied by the game and is subject to all its quirks and bugs and mods, such as `FixHeatPreview`.
<br>

> Setting: `ShowUnitTonnage`  (true/false, default false)
>
> When true, show mech and vehicle tonnage in selection and target panel.
>
> Duplicates with Extended Information, but AIM override it and use a shorter form for mechs to fit `ShowNumericInfo`.
> Default false because the short form may overwhelm inexperienced players.
<br>


**MechWarrior Info**

> Setting: `ShowEnemyWounds`  (format string, default "{0}, Wounds {1}")<br>
> Setting: `ShowNPCHealth`  (format string, default "{0}, HP {2}/{3}")<br>
>
> When non-empty, NPCs who are wounded will have its injuries or health displayed.
> {0} is pilot name, {1} is injury, {2} is (health - injury), and {3} is health.
>
> Note that enemy's health is unknown to players by design.  {2} and {3} will show "?" when used.
> If the format does not starts with {0}, it will be always active.
> Otherwise, the wounds or health only display when the NPC is wounded.
<br>

> Setting: `ShortPilotHint`  (format string, default "G:{3} P:{4} G:{5} T:{6}")
>
> When non-empty, replace mechwarrior's summary hint. The parameters are: <br>
> {0}, {1}, {2} - Wound, Health - Wound, and Health. <br>
> {3}, {4}, {5}, {6} - Gunnery, Piloting, Gut, and Tactic.
>
> This only applies to the one-line hint that pops up on mouseover and right-click.
> The original hint, which shows only wounds and health, will be preserved when the hint is fully expanded (i.e. when no mech is selected).
>
> If the line is too long, for example when HP is included, the line will wrap.


**Weapon Info**

> Setting: `ColouredLoadout`  (true/false, default true) <br>
> Setting: `ShowDamageInLoadout`  (true/false, default true) <br>
>
> When true, loadout list of the targeting computer will be coloured by weapon type and postfixed with weapon damage.
<br>

> Setting: `ShowAlphaDamageInLoadout`  (format string, default "Damage {2} + Long {3}")
>
> When non-empty, loadout list label of the targeting computer will be changed to show total weapon damage.
>
> Parameters: <br>
> `{0}` - Sum of damage of all ranged weapons.
> `{1}` - Sum of damage of all support-range weapons. (Range <= 90)
> `{2}` - Sum of damage of all close-range weapons. (Range 91 to 360)
> `{3}` - Sum of damage of all long-range weapons. (Range > 360)
> `{4}` - Sum of damage of close and long range weapons. (Range > 90)
<br>

> Setting: `ShowMeleeDamageInLoadout`  (true/false, default true)
>
> When true, loadout list of the targeting computer will have melee and dfa entry.
> Their damage will always be displayed regardless of `ShowDamageInLoadout`.
<br>

> Setting: `ShowAmmoInTooltip`  (true/false, default true)<br>
> Setting: `ShowEnemyAmmoInTooltip`  (true/false, default false)<br>
>
> When true, show ammo count in the component lists when you mouseover a location on the paper doll.
>
> The main purpose is to allow you to see the state of each ammo bin and tell whether they are at risk of exploding.
<br>

> Setting: `ShowWeaponProp`  (true/false, default true)<br>
> Setting: `WeaponRangeFormat`  (string, default "Min {0} : Long {2} : Max {4}")<br>
>
> These two settings override the weapon information displayed when you mouseover a weapon in combat.
>
> `ShowWeaponProp` overrides the full weapon name with weapon properties, if the weapon is rare (+ to +++).
> For example, an "M Laser++" may display "+1 ACC, +25% CRIT" instead of "MEDIUM LASER".
>
> `WeaponRangeFormat` replaces the weapon range with actual meters when non-empty.
> The string {0} to {4} will be replaced by a weapon's MinRange, ShortRange, MediumRange, LongRange, and MaxRange.
> Most of them are unused by vanilla game, so this mod use MinRange, MediumRange, and MaxRange by default.
> But if a mod is installed that make use of them, the range display can be customised, such as "{0}:{1}:{2}:{3}:{4}".


**Multi-Target**

> Setting: `FixMultiTargetBackout`  (true/false, default true)
>
> The game's Muti-Target back out (escape/right click) is bugged
> Backing out from first target will cancel the action, and second back out will always cancel the whole thing (regardless of target).
>
> When true, this mod will make Multi-Target back out from selected targets one by one as expected.



## Targeting Line and Facing Ring Settings

**LoS Widths**

> Setting: `LOSWidth`  (0 to 10, default 2, game default 1)
>
> Set width of all targeting lines (direct, indirect, blocked, can't attack etc.).  Game default is 1  Mod default is 2.
<br>

> Setting: `LOSWidthBlocked`  (0 to 10, default 1.5, game default 0.75)
>
> Set width of obstructed part of an obstructed targeting lines, which is normally thinner than other lines by default.  Game default is 0.75.  Mod default is 1.5.
<br>

> When the mod "Firing Line Improvement" is detected, these settings will be disabled to avoid conflicts.


**LoS Styles and Colours**

> Setting: `LOSIndirectDotted`  (default true, game default false)<br>
> Setting: `LOSNoAttackDotted`  (default true)<br>
> Setting: `LOSMeleeDotted`  (default false)<br>
> Setting: `LOSClearDotted`  (default false)<br>
> Setting: `LOSBlockedPreDotted`   (default false)<br>
> Setting: `LOSBlockedPostDotted`  (default false)<br>
> Setting: `LOSMeleeColors`  (default "")<br>
> Setting: `LOSClearColors`  (default "")<br>
> Setting: `LOSBlockedPreColors`   (default "#D0F")<br>
> Setting: `LOSBlockedPostColors`  (default "#C8E")<br>
> Setting: `LOSIndirectColors`  (default "")<br>
> Setting: `LOSNoAttackColors`  (default "")<br>
>
> When non-empty, set the colour and style of various targeting lines.
> Obstructed lines has two parts. The part before obstruction is Pre, and the part after is Post.
>
> Colours are either empty or in HTML hash syntax.  For example `"#F00"` = red, `"#0F0"` = green, `"#00F"` = blue, `"#FFF"` = white, `"#888"` = grey, `"#000"` = black.
> Four parts means RGBA, while three parts mean full opacity RGB.  Supports full and short form. e.g. #28B = #2288BB = #2288BBFF.
>
> Colours and only colours can also vary by attack direction, separated by comma.  The directions are Front, Left, Right, Rear, and Prone, in this order.
> If less colours are specified than direction, the missing directions will use the last colour.
> For example "red,cyan,cyan,green" will result in front red, side cyan, and back/prone green.
>
> When the mod "Firing Line Improvement" is detected, these settings will be disabled to avoid conflicts.
> Note that Firing Line Improvement does not have directional styling.


**Facing Rings Colours**

> Setting: `FacingMarkerPlayerColors`  (default "#FFFA,#CFCA,#CFCA,#AFAC,#FF8A")<br>
> Setting: `FacingMarkerEnemyColors`  (default "#FFFA,#FCCA,#FCCA,#FAAC,#FF8A")<br>
> Setting: `FacingMarkerTargetColors`  (default "#F41F,#F41F,#F41F,#F41F,#F41F")<br>
>
> When non-empty, change the colours of each arc for friends, foes, and targeted arc during attack.  The colours are for Front, Left, Right, Rear, and Prone.


**Widths of Obstruction Marker**

> Setting: `LOSMarkerBlockedMultiplier`  (0 to 10, default 1.5)
>
> Scale the obstruction marker of targeting lines, the "light dot" that split the obstructed line into two. 2 means double width and height, 0.5 means half-half.
> Set to 1 to leave at game default.  Set to 0 will not remove them from game but will effectively hide them.
>
> When the mod "Firing Line Improvement" is detected, this setting will be disabled to avoid conflicts.


**Refine or Roughen Fire Arc and Jump Arc**

> Setting: `ArcLinePoints`  (2 to 1000, default 48, game default 18)
>
> To some sharp eyes, it is easy to see the hard corners of the arc of indirect targeting lines.
> Lines are quick to draw, so this mod will happily improves their qualities for you.
> Set to 2 to make them flat like other lines.  Set to 18 to leave at game default.
>
> When the mod "Firing Line Improvement" is detected, this setting will be disabled to avoid conflicts.


**Fix LoS Inconsistency**

> Setting: `FixLosPreviewHeight`  (true/false, default true)
>
> Walk and Jump will sometimes predicts different Line of Sight, because their preview height is slightly different from each other.
> When true, they will be made the same.



## Called Shot Settings


**Fix Grey Head Disease**

> Setting: `FixGreyHeadDisease`  (true/false, default true)
>
> When true, confine the grey head disease to the boss so that it does not spread around.
>
> Otherwise, when anyone (friend or foe) attacks a headshot immune character, all attacks from the same direction will never hit any head ever again.
> Every one's head will be grey.  I call it the grey head disease.  It lasts until you load a game, which resets the hit tables.
<br>

> Setting: `FixBossHeadCalledShotDisplay`  (true/false, default true)
>
> When true, boss head is always unselectable in called shot.
>
> Did you know that grey head in called shot popup is actually a bug?  (I hope it is a bug.)
> If you try to call shot the boss before any headshot-immune unit is attacked, such as right after a load, the head is actually selectable!
> This is most apparent with FixGreyHeadDisease on, since the head will be always available.
>
> If `FixGreyHeadDisease` is true but `FixBossHeadCalledShotDisplay` is false, the boss's head will be selectable for called shot, but you will never hit the head.


**Enable Clustering Called Shot**

> Setting: `CalledShotUseClustering` (true/false, default true)
>
> When true, called shot has a higher chance to hit adjacent locations.
>
> For example, head called shot would bias the head, but also the three torsos to a lesser degree.
>
> This is the default behaviour on and before game version 1.0.4, which was bugged and caused very low called head shot chances since head is excluded from clustering.
> The bug is one of the driving forces of this mod's initial creation.  This mod can recreate the clustering effect without the bug.
>
> Note that this does not apply to Vehicle called shot; vehicles have too few locations to have meaningful clustering.


**Adjust Called Shot Weight**

> Setting: `MechCalledShotMultiplier`  (0 to 1024.0, default 0.33)
>
> When clustering called shot is enabled, chance to hit called locations will be amplified by clustering weight.  This setting let you tune called shot's weight multipliers.
> Default is 0.33 to counter the effect of CalledShotClusterStrength.  Set to 1.0 would leave original multiplier unchanged, while 0.0 will removing it leaving only clustering effect (if enabled).
<br>

> Setting: `VehicleCalledShotMultiplier`  (0 to 1024.0, default 0.75)
>
> Unmodified called shot is pretty powerful on vehicles because of its low number of locations.  This setting tries to balance that.


**Update and Format Called Shot Display**

> Setting: `ShowRealMechCalledShotChance`  (true/false, default true)<br>
> Setting: `ShowRealVehicleCalledShotChance`  (true/false, default true)
>
> When true, the popups will reflect modded hit distribution such as clustering and multiplier effect.
<br>

> Setting: `CalledChanceFormat`  (string, default "")
>
> Use Microsoft C# [String.Format](https://docs.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2008/0c899ak8(v=vs.90) syntax to format called shot location chances.
>
> Set to "{0:0.0}%" to always show one decimal, or "{0:0.00}%" for two decimals.
> Leave empty to round them to nearest integer.
>
> Replace the old "ShowDecimalCalledChance" setting in mod version 1.0.


## Melee and DFA Settings


**Unlock Melee and DFA Positioning**

> Setting: `IncreaseMeleePositionChoice`  (true/false, default true)<br>
> Setting: `IncreaseDFAPositionChoice`  (true/false, default true)
>
> When true, melee and DFA can use all available positions, instead of nearest three.  Compatible with MeleeMover.
<br>

> Setting: `UnlockMeleePositioning`  (true/false, default true)
>
> When true, player units may move to another melee position when target is already in melee range.
> When the mod "MeleeMover" is detected, this setting will be disabled to avoid conflicts.


**Adjust Max Height Offset**

> Settings: MaxMeleeVerticalOffsetByClass  (comma separated positive number, default "8,11,14,17")
>
> When non-empty, adjust `MaxMeleeVerticalOffset` by the class of the attacker or target, whoever is at the lower ground.
> Value is for Light, Medium, Heavy, and Assault mechs.  Non-mechs are considered Light for the purpose of this setting.
>
> Game default is all 8.  Mod default allows bigger mechs to hit and be hit across higher height differences.
>
> This setting applies to both Melee and DFA.
> If a value is missing or invalid, it will be set to the same as lighter class, or "8" if it is the first value.



## Individual Modifier Settings


**Base Hit Chance**

> Setting: `BaseHitChanceModifier` (-10.0 to 10.0, default 0)<br>
> Setting: `MeleeHitChanceModifier` (-10.0 to 10.0, default 0)
>
> Increase or decrease base hit chance of ranged/melee attacks.
> e.g. -0.05 to lower base accuracy by 5%, 0.1 to increase it 10%.


**Directional Modifiers**

> Setting: `ToHitMechFromFront`  (-20 to 20, default 0)<br>
> Setting: `ToHitMechFromSide`  (-20 to 20, default -1)<br>
> Setting: `ToHitMechFromRear`  (-20 to 20, default -2)<br>
> Setting: `ToHitVehicleFromFront`  (-20 to 20, default 0)<br>
> Setting: `ToHitVehicleFromSide`  (-20 to 20, default -1)<br>
> Setting: `ToHitVehicleFromRear`  (-20 to 20, default -2)<br>
>
> Determine the modifier for attacking from side or rear.
> Effective only if "Direction" is in the modifier lists.


**Height Modifier**

> Setting: `AllowLowElevationPenalty`  (true/false, default true)
>
> When true, attacking from low ground to high ground will incur an accuracy penalty that is the exact reverse of attacking from high ground to low.
> Game default is false.


**Indirect Fire**

> Setting: `SmartIndirectFire`  (true/false, default true)
>
> When true, indirect fire will be used for indirect-fire-capable weapons,
> if line of fire is obstructed and indirect penalty is less than obstructed penalty.


**Jumped Modifier**

> Settings: `ToHitSelfJumped` (-20 to 20, default 0)
>
> The game has self moved modifier and self sprint modifier in `CombatGameConstants.json`, but not self jumped modifier.
> You may set it with this mod if you want to.  It will be factored in attack preview.
>
> Effective only if "Jumped" is in the modifier lists.

      public bool ColouredLoadout = true;

      [ JsonComment( "Show weapon damage in weapon loadout list in targetting computer.  Default true." ) ]
      public bool ShowDamageInLoadout = true;

      [ JsonComment( "Show alpha/melee&dfa damage in weapon loadout list in targetting computer.  Default \"Alpha {2}+{3}\"." ) ]
      public string ShowAlphaDamageInLoadout = "Damage {2} + Long {3}";

      [ JsonComment( "Show melee & dfa damage in weapon loadout list in targetting computer.  Default ." ) ]
      public bool ShowMeleeDamageInLoadout = true;



## Net Hit Modifier Settings

**Allow Net Bonus Modifier**

> Setting: `AllowNetBonusModifier`  (true/false, default true)
>
> When true, total modifier of an attack can be a net bonus that increases the hit chance beyond the attacker's base hit chance
> (but still subjects to 95% cap unless lifted by the `MaxFinalHitChance` settings).
> Game default is false.
>
> When the net modifier is a bonus, it will use the same handling as penalty but reversed.
> Default is stepped, which means first 10 modifiers are ±5% each, and subsequence modifiers are ±2.5% each.


**Unlock Modifier Stepping and Range**

> Setting: `HitChanceStep`  (0.0 to 1.0, default 0)
>
> The game will round down final hit chance to lower 5% by default.
> This affects some calculations, such as rendering odd gunnery stats and piloting stats less effective then they should be.
> Set this to 0 to remove all stepping.  Set it to 0.005 will step the accuracy by 0.5%, and so on.
<br>

> Setting: `MaxFinalHitChance`  (0.1 to 1.0, default 0.95)<br>
> Setting: `MinFinalHitChance`  (0.0 to 1.0, default 0.0)
>
> Use this to set max and min hit chance after all modifiers but before roll correction.
> Note that 100% hit chance may still miss if roll correction is enabled.


**Diminishing Hit Chance Modifier**

> Setting: `DiminishingHitChanceModifier`  (true/false, default false)
>
> Set this to true to enable diminishing return of modifiers, instead of simple add and subtract.
> As a result, small penalties have a bigger effect, but very large penalties become more bearable.
<br>

> Setting: `DiminishingBonusPowerBase`  (default 0.8)<br>
> Setting: `DiminishingBonusPowerDivisor`  (default 6)<br>
> Setting: `DiminishingPenaltyPowerBase`  (default 0.8)<br>
> Setting: `DiminishingPenaltyPowerDivisor`  (default 3.3)<br>
>
> Bonus formula is "2-Base^(Modifier/Divisor)". <br>
> Example: +3 Bonus = 0.8^(3/6) = -1.1055728 = 110%. <br>
> Thus +3 Bonus @ 80% To Hit = 80% x 110% = 88% final to hit.
>
> Penalty formula is "Base^(Modifier/Divisor)". <br>
> Penalty Example: +6 Penalty = 0.8^(6/3.3) = 0.6665 = 66.7%. <br>
> Thus +6 Penalty @ 80% To Hit = 80% x 66.7% = 53% final to hit.
<br>

> Setting: `DiminishingBonusMax`  (default 16)
> Setting: `DiminishingPenaltyMax`  (default 32)
>
> The modifiers are pre-calculated to run faster.  These settings determine how many results are cached.
> Modifiers beyond the max will be regarded as same as max.


**Change Modifiers List**

> Setting: `RangedAccuracyFactors`  (comma separated value)<br>
> Setting: `MeleeAccuracyFactors`  (comma separated value)<br>
>
> A list of hit modifiers of ranged / melee and DFA attacks.  Leave empty to keep unchanged.  Order and letter case does not matter.
>
> Since this feature will override both mouseover display and actual modifier calculation, this will fix the bug that SelfStoodUp is displayed in melee mouseover but not counted in net modifier.
>
> Ranged default is "ArmMounted, Direction, Height, Indirect, Inspired, Jumped, LocationDamage, Obstruction, Precision, Range, Refire, SelfHeat, SelfStoodUp, SelfTerrain, SensorImpaired, SensorLock, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrain, Walked, WeaponAccuracy, WeaponDamage".
> Melee default is "Direction, DFA, Height, Inspired, Jumped, SelfChassis, SelfHeat, SelfStoodUp, SelfTerrainMelee, Sprint, TargetEffect, TargetEvasion, TargetProne, TargetShutdown, TargetSize, TargetTerrainMelee, Walked, WeaponAccuracy".
>
> Options:<br>
> **ArmMounted** - (Ranged) Apply arm mounted modifier if weapon is mounted on an arm. (Melee) Apply arm mount bonus if the punching arm is intact and the attack is not DFA and not against prone mech or vehicle. <br>
> **Direction** - Apply bonus if attack is made from the target's side or rear. <br>
> **DFA** - (Melee) Apply DFA penalty if attack is DFA. <br>
> **Height** - (Ranged) Apply height modifier.  (Melee) Apply one level of height modifier if height different is at least half of melee reach.  DFA height difference is calculated like ranged weapon - from pre-flight attacker position to target position. <br>
> **Indirect** - (Ranged) Apply indirect fire penalty. <br>
> **Inspired** - Apply inspired bonus. <br>
> **Jumped** - Apply jumped penalty after jump, if any. <br>
> **LocationDamage** - (Ranged) Apply location damage penalty, if any. <br>
> **Obstruction** - Apply obstructed penalty. <br>
> **Precision** - (Ranged) Apply Precision Strike bonus. <br>
> **Range** - (Ranged) Apply range penalty. <br>
> **Refire** - Apply refire penalty.  (Melee) Should be 0 by default but can be changed in weapon data files. <br>
> **SelfChassis** - (Melee) Apply chassis modifier. <br>
> **SelfHeat** - Apply overheat penalty. <br>
> **SelfStoodUp** - Apply stood up penalty. <br>
> **SelfTerrain** - Apply self terrain penalty as if this is a ranged attack. <br>
> **SelfTerrainMelee** - Apply self terrain penalty as if this is a melee attack. <br>
> **SensorImpaired** - Apply sensor impaired penalty. <br>
> **SensorLock** - Apply sensor lock bonus. <br>
> **Sprint** - Apply sprint penalty, if somehow you can attack after sprint. <br>
> **TargetEffect** - Apply target effect penalty such as gyro. <br>
> **TargetEvasion** - Apply target evasion penalty.  Melee attacks ignore up to 4 evasion by default. <br>
> **TargetProne** - Apply target prone penalty. <br>
> **TargetShutdown** - Apply target shutdown bonus. <br>
> **TargetSize** - Apply target size penalty. <br>
> **TargetTerrain** - Apply target terrain's ranged penalty. <br>
> **TargetTerrainMelee** - Apply target terrain's melee penalty. <br>
> **Walked** - Apply self walked penalty, default 0 but can be changed in game configuration file. <br>
> **WeaponAccuracy** - Apply weapon accuracy, TTS, and mod bonus. <br>
> **WeaponDamage** - (Ranged) Apply weapon damaged penalty.
>
> The modifier system is designed to be moddable.
> Patch `ModifierList.GetCommonModifierFactor`, `ModifierList..GetRangedModifierFactor`, and/or `ModifierList.GetMeleeModifierFactor` to add new modifiers.



## Hit Roll Settings

**Adjust Roll Correction**

> Setting: `RollCorrectionStrength`  (0.0 to 2.0, default 0.5)
>
> It is no secret that the game fudge all hit rolls, called a "correction".
> As a result, real hit chances are shifted away from 50%, for example 75% becomes 84% while 25% becomes 16%.
> This can create a rift between what you see and what you get, especially on low chance shots.
>
> This mod does not aim to completely disable roll adjustment, and thus default to half its strength.
> You can set the strength to 0 to disable it, 1 to use original formula, 2 to amplify it, or any value between 0 and 2.
>
> If the "True RNG Hit Rolls" mod is detected, this setting will be switched to 0 for consistency.


(Advanced) **Adjust Miss Streak Threshold**

> Setting: `MissStreakBreakerThreshold`  (0.0 to 1.0, default 0.5)
>
> In addition to roll adjustment, the game also has a "miss streak breaker".
> Whenever you miss an attack of which uncorrected hit chance > 50%, the streak breaker will adjust your hit chance up on top of roll correction.
> The bonus accumulates until you land a hit (regards of hit chance), at which point it resets to 0.
>
> This setting let you adjust the threshold.  0.75 means it applies to attack of which hit chance > 75% (excluding 75%).
> Set to 0 enable it for all attacks, or set to 1 to disable it.  Default is 0.5 which is the game's default.
>
> If the "True RNG Hit Rolls" mod is detected, this setting will be switched to 1 for consistency.


(Advanced) **Adjust Miss Streak Bonus**

> Setting: `MissStreakBreakerDivider`  (-100.0 to 100.0, default 5.0)
>
> For every miss that crosses the streak breaker threshold, the threshold is deduced from hit chance, then divided by 5.
> The result is then added as streak breaker bonus.
>
> Set this setting to a positive number to override the divider.
> For example at threshold 0.5 and divider 3, a 95% miss result in (95%-0.5)/3 = 15% bonus to subsequence shots until hit.
> Default is 5 which is the game's default.
>
> Set this setting to zero or negative integer to replace it with a constant value.
> For example -5 means each triggering miss adds 5% bonus, and -100 will make sure the next shot always hit.



## Hit Chance Preview Settings


> Setting: `ShowBaseHitchance`  (true/false, default true)
>
> Show the mechwarrior's base hit chance in modifier tooltip.


> Setting: `ShowNeutralRangeInBreakdown`  (true/false, default false)
>
> When true, show range category in modifier tooltip even the range has no modifiers.
> In unmodded vanilla this will be the "Short Range".
> Because this differs from "Optimal Range" used in vanilla mech bay, this setting is disabled by default.


> Setting: `FixSelfSpeedModifierPreview`  (true/false, default true)
>
> If moved/sprint/jumped modifier is non-zero, this mod can patch the game to factor them in during action planning.


(Advanced) **Show Corrected Hit Chance**

> Setting: `ShowCorrectedHitChance`  (true/false, default false)
>
> It's one thing to fudge the rolls.  It is another to let you know.
> Set this to true to show the corrected hit chance in weapon panel.
>
> When the "Real Hit Chance" mod is detected, this settings will be switched to on and overrides that mods.


**Format Hit Chance** (default "")

> Setting: `HitChanceFormat`  (free string, default "")
>
> Use Microsoft C# [String.Format](https://docs.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2008/0c899ak8(v=vs.90) syntax to format weapon hit chances.
>
> Set to "{0:0.0}%" to always show one decimal, or "{0:0.00}%" for two decimals.
> When empty AND HitChanceStep is 0, will use "{0:0.#}%" to optionally show hit chance to one decimal point.
>
> Replace the old "ShowDecimalHitChance" setting in mod version 1.0.



## Critical Hit Settings


**Skip Criting the Dead Mech**

> Setting: `SkipCritingDeadMech`  (true/false, default true)
>
> When true, critical hits are not rolled for dead units.
> This is mainly designed to prevent crit messages from flooding over cause of death.
> As a side effect, slightly more components will be salvageable instead of being destroyed.


**NPC Crit Multipliers**

> Setting: `CritChanceEnemy` (0 or above, default 0.2) <br>
> Setting: `CritChanceAlly` (0 or above, default 1) <br>
>
> Override the crit chance set by AICritChanceBaseMultiplier in CombatGameConstants.json.
> Game default is 0.2 which lowers all NPCs' crit chance to 20% of original.
>
> Set to 1 for same crit chance as players, or set to 0 to prevent enemies or allies from dealing crit.


**Non-Mech Critical Hit**

> Setting: `CriChanceVsVehicle` (0 or above, default 0.75) <br>
> Setting: `CritChanceVsTurret` (0 or above, default 0.6) <br>
>
> When non-zero, enable critical hit on vehicles and turrets.
> The numbers are multipliers of crit chance, e.g. 0.5 to set to half normal crit chance.
>
> Because of the very high armour to structure ration of vehicles and especially turrets,
> The numbers will not matter much unless `CritChanceFullArmor` is higher than zero.
> 
> In case of that, the default chance is lower than normal,
> for a more consistent crit level relative to mech since vehicles and turrets do not have empty slots to blunt crits.
<br>

> Setting: `AmmoExplosionKillTurret`  (true/false, default true) <br>
> Setting: `AmmoExplosionKillVehicle`  (true/false, default true) <br>
>
> When true, turrets and vehicles will be destroyed when any of their ammo explodes.
> Otherwise, the game does not expects this to happen and has no code to kill the unit.
>
> As a bonus, "XXX Ammo Destroyed" message will be suppressed when ammo explosion happens, if either option is on.
> (Mech ammo explosion included, if one of the crit system replacing options is enabled.)


**Crit Follows Damage Transfer**

> Setting: `CritFollowDamageTransfer`  (true/false, default true)
>
> When true, critical hits will be rolled on last damaged location, i.e. they follows damage transfer.
>
> In un-modded game, critical hit is checked only on the rolled hit location and does not follow damage transfer.
> For example, when a laser hits a destroyed arm and damages a side torso, crit is not rolled since the arm is already destroyed.


**Fix False Positive Crits**

> Setting: `FixFullStructureCrit`  (true/false, default true)
>
> When true, critical hit does not happens on locations with intact structure.
>
> In un-modded game, critical hit is rolled on all location that is hit and has zero armour.
> This means crit is rolled even if the weapon reduces the armour to exactly zero and did not do any structural damage.
> When this happens, a crit slot will be rolled half the time, since minimal crit chance is 50%!
>
> This setting is off when through armour critical (below) is on, in which case zero armour uses the through armour rules.


**Through Armour Critical Hits**

> Setting: `ThroughArmorCritThreshold`  (0 to 1000, default 9)
>
> Each weapon must deal this much damage to a location in an attack for through armour critical hit to be checked.
>
> Default is 9 which is 3 MG hits, 3 LRM hits, or 2 SRM hits to the same location when un-braced and un-covered.
>
> If the number is between 0 and 1 (exclusive), the threshold is a fraction of the max armour of the location.
> e.g. 0.2 means a weapon must do as much damage as 20% of the max armour of the location.
>
> If the number is between 0 and -1 (inclusive), the threshold is a fraction of the current armour of the location.
> e.g. 0.2 means a weapon must do as much damage as 20% of the current armour of the location.
>
> Note: If `CritFollowDamageTransfer` is false, damage transfer will not be counted on the final damaged location,
> and may cause a multi-shot weapon to fail to reach threshold when it actually did.
> (Single-shot weapon won't even check for crit when damage transfer happens - that is what the settings do.)
>
> Also, in order to keep the code simple, fraction of armour is not exact.
> Shots with partially transferred damage are counted in full and can more easily meet the threshold.
> Fraction of current armour is also more easily skewed when damage is partially transferred.
> But generally speaking, the imprecision has limited impact and align with the spirit of through armour crit.
<br>

> Setting: `CritChanceZeroArmor`  (0 to 2, default 0)<br>
> Setting: `CritChanceFullArmor`  (-1 to 1, default 0)<br>
>
> The two settings together determine the range of through armour base critical chance.
> `CritChanceZeroArmor` is the max chance, and `CritChanceFullArmor` is the min chance.
> When `CritChanceZeroArmor` is 0, through armour critical hit is disabled.
>
> For a fixed crit chance, set both numbers to same.
> Classic BattleTech has the chance at around 3%, or 0.03.
>
> When the numbers are different, the crit chance increases in proportion to armour damage.<br>
> Example: When zero = 0.4 and Full = 0, a location with half armour has a 20% base crit chance.<br>
> Example: When zero = 0.2 and Full = -0.1, crit may happens after armour is reduced to 2/3 or below.<br>
>
> When through armour critical hit happens and it is logged by this mod's Attack Log,
> the Max HP column logs the max armour of the location instead of max structure.


**Normal Crit Chance**

> Setting: `CritChanceZeroStructure` (0 to 2, default 1)<br>
> Setting: `CritChanceFullStructure` (-1 to 1, default 0)<br>
> Setting: `CritChanceMin` (0 to 1, default 0.5)<br>
> Setting: `CritChanceMax` (0 to 1, default 1)<br>
>
> These settings can be used to change the normal critical hit chance on structurally damaged locations.
> CritChanceZeroStructure and CritChanceFullStructure decides the base range relative to structure%,
> then CritChanceMin and CritChanceMax is applied to cap the chance.
>
> The default values are the same as game's default.
> Using defaults, a location with 75/100 structure has 25% crit chance, raised to 50% by `CritChanceMin`.
> This becomes the base crit chance of the location, to be modified by crit multipliers.


**Critical Hit Reroll**

> Setting: `CritIgnoreDestroyedComponent` (true or false, default false)<br>
> Setting: `CritIgnoreEmptySlots` (true or false, default false)<br>
>
> When true, a successful critical hit will ignore destroyed components and/or empty slots.
> These settings simulate Classic BattleTech's crit reroll that happens when the crit slot is invalid.


**Critical Hit Location Transfer**

> Setting: `CritLocationTransfer` (true or false, default false)
>
> When true and a successful critical hit happens on a location that has nothing to crit,
> usually because of `CritIgnoreDestroyedComponent` and `CritIgnoreEmptySlots`,
> the crit will transfer to the next location using damage transfer rule.
>
> For best simulation of Classic BattleTech's crit, please also set `CritFollowDamageTransfer` to true.


**Multiple Critical Hits from Single Shot**

> Setting: `MultupleCrits` (true or false, default false)
>
> In Classic BattleTech, critical hits may damage multiple components.
> This setting recreate that feel in the context of BattleTech's crit system.
>
> When true, a successful crit roll is deduced from the crit chance.
> This leftover crit chance is then rolled again, and repeated until a crit roll fails.



## Hit Resolution Settings


**Balance Ammo Consumption**

> Setting: `BalanceAmmoConsumption`  (true/false, default true)<br>
> Setting: `BalanceEnemyAmmoConsumption`  (true/false, default false)
>
> When true, mechs will draw ammo in an intelligent way to minimise chance of ammo explosion.
> After that is done, the AI will then minimise risk of losing ammo to crits and destroyed locations.
>
> The AI is pretty smart, but it can't shift ammo, so manually spreading ammo around can help it does its job.


**Auto Jettison Ammo**

> Setting: `AutoJettisonAmmo`  (true/false, default true)<br>
> Setting: `AutoJettisonEnemyAmmo`  (true/false, default false)
>
> When true, mechs will jettison useless ammo at end of its turn,
> provided it has not moved, is not prone, and is not shutdown.
> (The jettison doors are at the rear, so no prone jettisons.)
>
> This can happens if all weapons that use that kind of ammo are destroyed mid-fight,
> or if the mech was deployed with new ammo installed but not the weapon yet.
> Jettisoning the ammo will make sure they won't explode when hit.


**Precise Hit Location Distribution**

> Setting: `FixHitDistribution`  (true/false, default true)
>
> Set to true to increase hit location precision, specifically to improve the hit distribution of SRM and MG called shots.
>
> Game version 1.1 introduced degrading called shot effect for SRM and MG,
but because the code that determine hit distribution is not designed for fraction called shot weight, the actual distribution is slightly bugged.
<br>

**Kill Zombies**

> Setting: `KillZeroHpLocation`  (true/false, default true)
>
> Set to true to prevent locations and units from surviving at 0 HP.
>
> Some units have non-integer hp (usually turrets), and an attack may dealt non-integer damage (e.g. cover).  As a result, this may result in zombie locations and units that are at 0 structure but not dead.
>
> This mod can detect these cases and boosts the final damage just enough to finish the job.



## Attack Log Settings

**Log Level**

> Setting: `AttackLogLevel`  ("None", "Attack", "Shot", "Location", "Damage", "Critical", or "All", default "All")
>
> When not "None", the mod will writes an attack log in the mod's folder, called `Log_Attack.csv` by default.
>
> The levels are progressive.  Attack info is fully included by Shot level, Shot info is fully included by Location level, etc.
> The deeper the level, the more the mod needs to eavesdrops and the higher the chance things will go wrong because of game update or interference from other mods.
>
> **None** - It is a bug if you see a log file at this log level.  That or the file is a ghost that comes back to haunt you, in which case you should seek the church.
>
> **Attack** - Time, Attacker (team, pilot, mech), Target (team, pilot, mech), Direction, Range, Combat Id, and Action Id. The Ids can be used to consolidate data by-combat or by-action.
> For example a Multi-Target attack will log two or three different targets with the same Action Id.
>
> **Shot** - For each shot, log the Weapons, Weapon Template, Weapon Id, Attack Roll, Hit Chance, related info, and either Hit or Miss.
> Weapon Id is unique *per mech*, and can be combined to consolidate data by weapon.  This level and above also logs overheat damage and DFA self-damage.
>
> **Location** - Location Roll, Hit Table, Called Shot, and the Hit Location.
>
> **Damage** - Damage, Final Damaged Location, and Armor/HP of this location. 
> Damage is determined in a different phase from hit and location, and is a rather complicated info to log.
>
> **Critical** - Crit Location, Crit Roll, Crit Slot, Crit Component, and the result of the crit.
> Crit is determined in yet another phase, so the log code is *very fun* to write.
>
> **All** - same as Critical for now.  More info may be added in the future, though I am not sure I wouldn't go crazy.
> Would you believe logging is the most complicated feature of this mod?
>
> Default was "None" in mod version 1.0 and 2.0, but mod 2.1 switched to a multi-thread logging system so it now defaults to "All".


(Advanced) **Log Options**

> Setting: `AttackLoFormat`  ("csv", "tsv", "txt", default "csv")
>
> Set the format and extension of attack log.  Default is "csv" which can be opened directly by Excel.
<br>

> Setting: `AttackLogArchiveMaxMB`  (0 to 1 million, default 4)
>
> When the game first enter combat every launch, old attack log is archived through rename.
> Then log exceeding this size limit will be deleted in the background.
<br>

> Setting: `LogFolder`  (string, default "")
>
> Set path of mod log and attack log.  Default is empty which will use mod folder.



# Compatibilities

* BattleTech 1.0 - AIM 1.0.1.
* BattleTech 1.1 - AIM 1.0.1 to 2.1.2.
* BattleTech 1.2 - AIM 2.2 to 2.5.

AIM is aware of some other mods and will behave differently in their present to meet player expectations.

Mod settings changed by these behaviours are not saved.  If you want to replace them with AIM, you may need to change AIM settings.

**[Firing Line Improvement](https://www.nexusmods.com/battletech/mods/135)**
AIM will skip line styling and arc point adjustment to not crash line drawings.

**[MeleeMover](https://www.nexusmods.com/battletech/mods/226)**
AIM will skip its own melee unlock to preserve sprint range melee.

**[Real Hit Chance](https://www.nexusmods.com/battletech/mods/90)**
AIM will enable corrected hit chance display and override this mod, since it does not support AIM features such as adjustable correction strength.

**[True RNG Hit Rolls](https://www.nexusmods.com/battletech/mods/100)**
AIM will disable its own adjustable roll correction and miss streak breaker.

**[CBT Movement](https://github.com/McFistyBuns/CBTMovement)**
AIM will log a warning that jumping modifier feature is duplicated and one of them should be zero.

**[MechEngineer](https://github.com/CptMoore/MechEngineer)**
AIM will bridge crit code with MechEngineer for component crit immunity and multi-stage crit components to work as expected.

The first thing to check when you suspect any compatibility problems with the game or with other mods is to remove or disable the mods.

You can also check the mod log (`BATTLETECH\Mods\AttackImprovementMod\Log_AIMAttackImprovementMod.log`), BTML log (`BATTLETECH\Mods\*.log`), and the game's own log (`BATTLETECH\`).
The keyword is "Exception".  It is almost always followed by lots of code.
If you see *any* exception with "AttackImprovementMod" in the code below it, please [file an issue](https://github.com/Sheep-y/Attack-Improvement-Mod/issues/new) and attach the log.


# The Story of AIM

If you asked me whether I would play a hardcore mech game like BattleTech, a month before I started doing this mod, my answer would be no.

This is my first serious game modding attempt, and is totally unexpected.

One day in summer 2018 when I pay the online game shops a visit, I noticed that one single game is is on the top of top sellers on GOG, Humble Store, and Steam.
The game is BATTLETECH.  It is not exactly my cup of tea, but I don't see that happens often either.
The game has just been launched, and Steam has a pretty big discount in my local currency.

By the time I finished the campaign, I have written a [GameFAQ guide](https://gamefaqs.gamespot.com/pc/205058-battletech/faqs/75955) and a [data miner](https://github.com/Sheep-y/Sheep-y.github.io/tree/dev/battletech/parser) in Node.js.

That is *almost* the end.

Except that I can't shake the feeling that the called hit chance is wrong.

So, I modded LRM and SRM to fire 500 shots and did some light testing, for the guide.
The first few tests fails my own testing standard, but the result is obvious: the numbers are VERY wrong, in game version 1.0.x.

After I improved my methodology, I did some [large scale tests](https://steamcommunity.com/app/637090/discussions/0/1697176044370891096/) totaling over 60k missiles against live King Crabs.  For a while it was a hot topic on Steam.

The result is not pretty.  And I don't mean to the crabs.

Called shot at the head not only has lower chance to hit the head than non-called shots, but the head is biased in virtually all attacks and has double chance to be hit than intended by normal attacks.

Baffled by the result, I learned how to use programing tools to see game code.  Finding the bugs is the easy part.
Fixing it is the hard part, since I knew *nothing* about BattleTech, paper or code, and I *never* hijacked any code before.

I started by injecting loggings into the system, to learn the process.
These logs later become the Attack Log feature.
How about the roll correction?  A mod kills it.  Can I tune it down instead? (Play with formulas in Excel.) Ok, let's do that.
Hmm, now I need to show the modified hit chance. (Re-learn algebra and coded perhaps the most complicated formula in all BattleTech mods.)

Eventually I fixed the two bugs I intended to fix, plus fixing vehicle called shot. (Fixed in 1.2.0 beta, two months after AIM is released.)
When I am mostly done, game updates 1.1 landed just the day before I plan to release, and it changed how called shot works.
Took me two days to update the mod, before I went back to enjoy the game.

Or so I hoped.

In reality, I now see game bugs everywhere, and many many ways to improve the game.
I want to fix Multi-Target back out.  I want to fix the paper doll.  I want to see more information.  I want to colour the lines and open source it.
LadyAlekto of RogueTech also *helpfully* reported more game bugs in form of feature wishes.

All the bug fixes and new features and enhancements is much bigger than what I originally envisioned.
Even as I tie up AIM version 2.0, the ever expanding idea list grow at an even greater pace.
If there is an end in sight, it is an abrupt one when a game that I like better come out, such as Phoenix Point.

This is the story of how I went from "stay away from BattleTech" to "wrote 3500 lines of BattleTech mod" in three month's time.
Now let me see whether Paradox is hiring remote freelancer.


# Learn to Mod

The [source code](https://github.com/Sheep-y/Attack-Improvement-Mod/) of this mod is open and free.
You can take it, modify it, and even release it, provided that you include *your* code when you distribute your work, and don't claim my work as yours.
License: [AGPL v3](https://www.gnu.org/licenses/agpl-3.0.en.html)

Follow these steps to see game code and learn how BATTLETECH mod works:

1. Install [Visual Studio](https://www.visualstudio.com/downloads/) which is free.  This mod is developed with VS 2017 Community Edition.  You only need the ".NET desktop development" workload.
1. Down and Run [DnSpy](https://github.com/0xd4d/dnSpy/releases)
1. Select File > Open, and find `BATTLETECH\BattleTech_Data\Managed\Assembly-CSharp.dll`.  This will load the assembly.  It contains most BATTLETECH code.
1. You may now search and browse the assembly!  For example in Edit > Search you can type "GetCorrectedRoll" to find the method.  Clicking on it will show you the roll correction code.
1. Right click on method name (or any identifier) and click "Analyse".  It'll help you find where and how the method is called, or its override hierarchy.
1. Analyse is very fast because it works with compiled code.  It is not 100% accurate, though, sometimes you'll want to exported code and search there instead.
1. Left click the assembly, then File > Export to Project.  This will decompile all code which you can browse with other editor or IDE.
1. Head to the [ModTek wiki](https://github.com/janxious/ModTek/wiki/) to learn how to make a mod.  For a code mode like this one, you need to compile a [dll](https://github.com/janxious/ModTek/wiki/Writing-ModTek-DLL-mods) and perhaps a [`mod.json`](https://github.com/janxious/ModTek/wiki/The-mod.json-Format).
1. The code of this mod is available on GitHub: https://github.com/Sheep-y/Attack-Improvement-Mod/ and you can also find other people's mods such as [Firing Line Improvement](https://github.com/janxious/BTMLColorLOSMod) or [MeleeMover](https://github.com/Morphyum/MeleeMover) and we all use different licenses.
1. You may notice that this patch manually calls Harmony to patch things, instead of using annotations.  This gives me much finer control.  Read [Harmony wiki](https://github.com/pardeike/Harmony/wiki) to learn how it works.


# Credits

* LogGuiTree code taken from CptMoore's [MechEngineer](https://github.com/CptMoore/MechEngineer/blob/v0.8.27/source/Features/MechLabSlots/GUILogUtils.cs#L99).

* Thanks Mpstark (Michael Starkweather) for making BTML and ModTek and various mods and release them to the public domain.
* Thanks LadyAlekto for various feature requests and cool proposals such as melee modifiers and ammo jettison.
* And many more players on the BattleTechGame discord that gave comments and ideas.

Despite feature overlap with some mods, this mod does not reference or use their code due to lack of license, and in most cases the approaches are different.
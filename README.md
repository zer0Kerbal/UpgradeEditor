# Upgrade Editor for Kerbal Space Program 1.3.1

This plugin is aimed at making the part/module upgrades feature introduced in 1.2 about as flexible as RealFuels engine
and RCS upgrades: configurable per part. Note that **the plugin doesn't add any upgrades**. If you want to have them in 
your game you need to download other mods that implement the upgrade feature. As two examples of applications of this 
Add-On that could broaden choices for modders, it makes upgrades designed as mutually exclusive alternatives with tradeoffs
far more useful and it also makes upgrades for engines/RCS as switchable propellant choices for the same part a lot more 
practical because they can be reverted back. 

## Dependencies

- ModuleManager (Included)

## Installation

- Unzip the UpgradeEditor folder to your Kerbal Space Program's GameData folder
- If you don't have ModuleManager 2.8.1 or higher already, unzip it to GameData too.

## Features

#### Upgrade Editor Interface

- This allows to customize which upgrades are applied to placed parts in all modes (Career, Science and Sandbox)
- After placing a part in the editor, click on "Upgrade Editor" in the Part's right-click menu.
- "Toggle All" will either disable or enable all upgrades at once. "Always Enable" ignores custom settings.
- When you're finished with a part, just close the Upgrade Editor by clicking on "Close". Check "Show Upgraded Stats".
- To reset settings, use the reset & close button in the menu, which will enable again all upgrades before closing the UI.

#### Persistent Upgrade Settings

- When you save a craft, all settings for upgrades are also saved for every individual part in the craft file.

#### Upgrade Ignore List

- Some upgrades aren't worth disabling, like max diameter increases for procedural parts. Check UpgradesToIgnore.cfg for details.

#### R&D tech tree feature

- In the nodes part list, upgrades have a pale green background to better differentiate them from parts.

## Download

Latest Version can always be found in this link:

https://drive.google.com/open?id=1H_AVY98KVk13zlYE22b8yo8xr8ugjArr

Older versions are here:

https://drive.google.com/open?id=1Zlj9p-RIceb1yP-43V-8SjG_30FIP_fk

## Source

Right here, in the src folder and also in the links above.

As for UpgradesGUI, which this initially was an attempt to update for KSP 1.3.1 but ended becoming wholly different:

https://github.com/gotmachine/UpgradesUIExtensions

#### Disclaimer

This is a work in progress. A lot needs to be improved before it gets even close to completed. This plugin was tested
on Engines and RCS where, with the aid of the MM patches, it worked both in Editor and Flight, but there are almost 
certainly bugs beyond the mentioned ones here.

#### License

GPL 3.0 except for RDColoredUpgradeIcon.cs which is public domain. For a long explanation: 

https://www.gnu.org/philosophy/philosophy.html

## Changelog and bugs

#### Known issues and limitations

- This plugin was not tested for every imaginable situation where it could be of use.

- There is no in-game interface to edit the ignore list of upgrades.

- Terrible support for part symmetry. First this won't apply automatically to all symmetrical counterparts of the selected part yet. Then
  there are bugs: if you open the Upgrade Editor Menu in a part, change its upgrade settings then alter its symmetry, argument out of range 
  exception spam will happen and the Upgrade Editor button may disappear in all affected parts. Doesn't happen when picking a new part for 
  symmetry from the left part menu, which inherits upgrade settings from latest edited similar with Upgrade Editor, and can also be avoided 
  by saving and reloading the vessel after using the Upgrade Editor Menu to only then apply symmetry.

#### v0.3.4 for KSP 1.3.1

- Fixed crippling bug with RCS part upgrades by disabling the OnAwake() command for ModuleRCS and RCSFX. Now works correctly instead of 
  having no RCS thrust at all.
  
- Fixed major bug where the first part with modified upgrade settings in a vessel loaded in VAB/SPH didn't have its upgrades properly applied.

- Fixed lack of reset to original Part Stats modified by PartStatsUpgradeModule upon disabling relevant upgrades.

- Fixed occasional bug where an Engine or RCS with upgrades that did not change propellant type would not have such reverted back during the load of 
  a craft file if upgrades that changed such settings were disabled, depending on the quantity and position of the involved parts in a saved craft.

- Toggle All Upgrades now is set to false by default, which makes any part picked from the left menu in the Editor inherit the same settings
  last applied to a similar part through the Upgrade Editor.

#### v0.3.3 for KSP 1.3.1

- Added capability of loading original part configs without upgrades and removed no longer necessary ModuleManager workaround patches.

- Added an Ignore List so upgrades that are utterly pointless to disable like diameter increases for procedural parts won't show up.

#### v0.3.2.5 for KSP 1.3.1

- Added full persistence for upgrade settings. Saved crafts now store which upgrades should not be enabled for every part in them.

#### v0.3.2 for KSP 1.3.1

- First test release, inheriting only the R&D tech tree highlight from UpgradesGUI and relying on wholly new code for everything else.
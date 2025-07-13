# Saving a structure

While holding the Structure Saver Tool (object ID `SceneBuilder:StructureSaverTool`), left-click one of the corners of your structure to select the first point, then left-click the opposite corner to select the second point. With both points set you can click and drag a point to make adjustments.

Once you are happy with the dimensions, right-click to open the save menu. Enter a name for your scene and then click Save. The structure will be saved *server side* as a `.json` file in the following locations:

- Windows: `C:\Users\%userprofile%\AppData\LocalLow\Pugstorm\Core Keeper\<platform>\<platform id>\mods\SceneBuilder\Saved`

## Setting loot tables

You can use the Structure Loot Tool (object ID `SceneBuilder:StructureVoid`) to set an object's inventory or drop loot table. A list of loot table IDs and their contents can be found [here](https://core-keeper.fandom.com/wiki/Loot_table_IDs).

## Excluding tile columns

Structure Void (object ID `SceneBuilder:StructureVoid`) is an item that you can place on tile columns to exclude them from a saved structure. (e.g. they will be replaced with natural terrain when generated)

## What is/isn't saved/limitations

- Modded objects are supported
- Structures have a maximum size of 64x64
- Object directions, paint colors and sign contents are saved
- Inventory contents are saved, but aux data (pet color, pet name, cattle transport box contents) will be lost
- Creatures are saved, but only in their default state (e.g. current HP will be lost)
- Dropped items aren't saved

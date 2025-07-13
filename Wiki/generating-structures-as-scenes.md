# Generating structures as scenes

After saving a structure, the next step is to make it generate in-game. Inside your main mod folder, create the following folders:

- `Data/SceneBuilder/<mod id>/Structures`
- `Data/SceneBuilder/<mod id>/Scenes`

You structure files should be placed inside the `Structures` folder, and scene files inside the `Scenes` folder.

## Scene file

Scene files are `.json` files (similar to structures) that determine how a structure generates. You'll need one for every structure. They use the following format:

| Key | Data type | Description |
| --- | --- | --- |
| `structure` | `string` | The id of the structure associated with the scene. Should be `<mod id>:<structure name>`. |
| `generation` | `GenerationSettings` | Determines how the scene generates. |

### GenerationSettings

| Key | Data type | Description |
| --- | --- | --- |
| `canFlip` | `string` | The directions the scene can be randomly flipped, must be `None`, `Horizontal`, `Vertical` or `HorizontalAndVertical`. Forced to `None` for scenes that can generate in dungeons. |
| `random` | `RandomGenerationSettings` | Settings for generating the scene randomly in the world. |
| `dungeon` | `DungeonGenerationSettings[]` | Settings for generating the scene in existing dungeons. |

### RandomGenerationSettings

| Key | Data type | Description |
| --- | --- | --- |
| `maxOccurrences` | `integer` | Maximum amount of times the scene can generate per world. |
| `biomesToSpawnIn` | `string[]` | A list of biome IDs the scene can generate in. Valid biome IDs are: `Slime` (Undergrounds), `Larva` (Clay Caves), `Stone` (Forgotten Ruins), `Nature` (Azeos' Wilderness), `Sea` (Sunken Sea), `Desert` (Desert of Beginnings), `Crystal` (Shimmering Frontier) and `Passage`. An empty list means the scene can generate in all biomes. |

### DungeonGenerationSettings

| Key | Data type | Description |
| --- | --- | --- |
| `internalName` | `string` | Internal name of the dungeon the scene should generate in. |
| `sceneGroupIndex` | `integer` | Internal scene group index that the scene should belong to. (a list of dungeons and their scene groups can be found below) |

### Examples

```JSON
{
	"structure": "MoreScenes:RuinsArcTemple",
	"generation": {
		"canFlip": "Horizontal",
		"random": {
			"maxOccurrences": 1,
			"biomesToSpawnIn": [
				"Slime"
			]
		}
	}
}
```

```JSON
{
	"structure": "MoreScenes:CityAquarium",
	"generation": {
		"dungeon": [
			{
				"internalName": "CityDungeon",
				"sceneGroupIndex": 0
			}
		]
	}
}
```

## Vanilla dungeons and scene groups

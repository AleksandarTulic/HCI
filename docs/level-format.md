# Level format

Levels live in `WhereAmI/Assets/levels/` as `level-NN.xml`. They are loaded in file-name order,
so `level-01.xml` is the first level. To add a level, add a new file with the next number.

All positions and sizes are in world units. One unit is one pixel of the background image.
`x` and `y` are the top-left corner, and `y` grows downwards.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Level name="Foggy Meadow" width="7040" height="1024"
       background="backgrounds/level-01.png" timeLimit="180">
  <PlayerStart x="100" y="100" />

  <Ground x="0" y="800" w="1376" h="224" />
  <Spikes x="4480" y="800" w="352" h="32" />
  <RewardBox x="608" y="544" coins="1" />
  <RewardBox x="6099" y="480" coins="3" hidden="true" />
  <Coin x="851" y="448" />
  <Enemy kind="ghost" x="320" y="736" range="500" />
  <MovingPlatform x="1450" y="700" w="160" h="28" toX="2020" toY="700" speed="150" />
  <Checkpoint x="3700" y="704" />
  <Hint x="0" y="0" w="300" h="1024" text="Hold ← → or A, D to walk." />
  <Goal x="6848" y="480" w="64" h="64" />
</Level>
```

## `<Level>` attributes

| Attribute      | Meaning                                                                 |
|----------------|-------------------------------------------------------------------------|
| `name`         | Shown in the HUD.                                                       |
| `width`        | Level width. The player cannot leave the level horizontally.            |
| `height`       | Level height. Falling below it costs a life.                            |
| `background`   | Optional. Painted image behind the level, relative to `Assets/`. Without it the level is drawn from its shapes: dark ground with grass, thorny spikes and a glowing gate. |
| `timeLimit`    | Seconds available. When the timer reaches 0, the player loses a life.   |

## Elements

| Element       | Attributes                      | Behaviour                                                                 |
|---------------|---------------------------------|---------------------------------------------------------------------------|
| `PlayerStart` | `x`, `y`                        | Required, exactly one. Where the player appears.                          |
| `Ground`      | `x`, `y`, `w`, `h`              | Solid from every side.                                                    |
| `Spikes`      | `x`, `y`, `w`, `h`              | Touching them costs a life.                                               |
| `RewardBox`   | `x`, `y`, `coins`, `hidden`     | 64×64, solid. Hitting it from below gives one coin until it is empty. A `hidden="true"` box is invisible and not solid until it is hit from below. |
| `Coin`        | `x`, `y`                        | 32×32 pickup, worth 1 point.                                              |
| `Enemy`       | `kind`, `x`, `y`, `range`       | Walks left and right over `range` units centred on `x`. Jump on it to defeat it; any other touch costs a life. `kind` is `ghost` (100 points) or `scribble` (200 points). |
| `MovingPlatform` | `x`, `y`, `w`, `h`, `toX`, `toY`, `speed` | Solid platform gliding between `x,y` and `toX,toY` at `speed` units per second, easing at both ends. Carries the player standing on it. |
| `Checkpoint`  | `x`, `y`                        | 40×96 lantern post. Touching it lights it; after losing a life the player comes back at the last lit one, with a full timer. |
| `Hint`        | `x`, `y`, `w`, `h`, `text`      | Invisible area. While the player is inside, `text` is shown at the bottom of the screen. |
| `Goal`        | `x`, `y`, `w`, `h`              | At least one required. Touching it finishes the level.                    |

Losing a life does not reset the level: collected coins, defeated enemies and opened boxes stay that way.

To try a level directly, start the game with `--level N`, for example
`dotnet run --project WhereAmI -- --windowed --level 3`.

Press **F3** in the game to see every hitbox. That makes it easy to line objects up with the background.

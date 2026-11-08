# Where Am I?

A 2D side-scrolling platformer in a black-and-white silhouette style, written in C# with WPF.

A small dark creature wakes up in a foggy, unfamiliar world. Collect coins, hit the `!` boxes,
stomp on ghosts and avoid spikes. Reach the end of each level before the time runs out.

> **Status:** being refactored. The game was originally written in 2021 as a learning project
> for a Human-Computer Interaction course. It is being moved to .NET 10, restructured, and made
> to run at any screen resolution.

![Gameplay](docs/images/game.png)

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and run

```sh
dotnet run --project src/WhereAmI
```

## Controls

| Key                    | Action                          |
|------------------------|---------------------------------|
| Left arrow / A         | Walk left                       |
| Right arrow / D        | Walk right                      |
| Up arrow / W / Space   | Jump                            |
| Space / Enter          | Play again after the game ends  |
| M                      | Music on/off                    |
| F3                     | Show hitboxes                   |
| Esc                    | Quit                            |

## Project layout

```
src/WhereAmI/
  Core/        Game loop (fixed 120 Hz update) and asset paths
  Game/        Game rules: world, player, enemies, pickups, session (no WPF code)
  Input/       Keyboard to game action mapping
  Levels/      XML level loader
  Rendering/   Sprite cache, animations and the surface that draws the world
  Audio/       Music and sound effects
  Assets/      Sprites, backgrounds, audio and levels
docs/          Screenshots and the level format
tools/         Developer scripts
```

Levels are plain XML files, described in [docs/level-format.md](docs/level-format.md).

## Levels

| Level 1 | Level 2 |
|---------|---------|
| ![Level 1](docs/images/map0.png) | ![Level 2](docs/images/map1.png) |

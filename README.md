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

## Controls (current)

| Key         | Action             |
|-------------|--------------------|
| Left arrow  | Walk left          |
| Right arrow | Walk right         |
| Space       | Stop / restart     |
| Up arrow    | Jump               |
| Esc         | Quit               |

## Project layout

```
src/WhereAmI/          Game source (WPF, .NET 10)
src/WhereAmI/Assets/   Sprites, sounds, music and level data
docs/images/           Screenshots
```

## Levels

| Level 1 | Level 2 |
|---------|---------|
| ![Level 1](docs/images/map0.png) | ![Level 2](docs/images/map1.png) |

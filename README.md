# Obsydian Showcase

Visual demo applications for the [Obsydian Engine](https://github.com/Vallerya-HQ/obsydian-engine).

## Demos

| Demo | Description |
|------|-------------|
| **HelloWindow** | Minimal window with colored quad and keyboard input |
| **EngineDemo** | Tilemap rendering, camera following, tile collision, text rendering, UI, scene management |
| **FullDemo** | Particles, dialogue system, screen transitions, game clock, debug overlay, render layers |

## Setup

The engine is referenced as a sibling directory (no submodules):

```
Vallerya-HQ/
  obsydian-engine/    # Engine source
  obsydian-showcase/  # This repo
```

## Build & Run

```bash
# Build all demos
dotnet build

# Run individual demos
dotnet run --project src/Showcase.HelloWindow
dotnet run --project src/Showcase.EngineDemo
dotnet run --project src/Showcase.FullDemo
```

## Engine Pin

The `.engine-commit` file pins the tested engine SHA. After testing with a new engine version:

```bash
./pin-engine
```

## License

See [LICENSE](LICENSE).

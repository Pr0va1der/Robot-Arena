# Robot Arena

> Working title · A compact third-person arena shooter prototype built with Unity.

Robot Arena is a short single-player combat experience in which the player controls a physics-driven combat robot, destroys patrol units and survives a timed reinforcement wave.

The project was created as a gameplay and AI programming exercise and is presented here as part of my game-development portfolio.

## Gameplay

- Physics-based, camera-relative movement
- Twin-barrel projectile shooting
- Defensive braking, jumping and a recoil-style ultimate ability
- Enemy patrol, detection, chase and ranged-combat states
- NavMesh navigation and waypoint routes
- Health, damage, respawn, pause, victory and defeat systems
- Initial arena encounter followed by a timed enemy wave

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move relative to the camera |
| `Mouse` | Rotate the camera |
| `Left Mouse Button` | Fire |
| `Space` | Jump |
| `Ctrl` | Brake |
| `Q` | Ultimate / recoil impulse |
| `Esc` | Pause |

## Tech stack

- Unity `2022.3.56f1` (LTS)
- C#
- Unity PhysX / Rigidbody physics
- Unity AI Navigation and NavMesh
- Animator state machines
- Cinemachine
- TextMesh Pro and UGUI

## Code highlights

- Shared `IDamageable` contract for player and enemy damage handling
- Event-based health-bar updates
- Animator-driven enemy state behaviours for idle, patrol and combat
- Wave manager with spawn limits, timer and UI counters
- Separate components for movement, shooting, health, detection and menus

The main gameplay code is located in [`Assets/Scripts`](Assets/Scripts).

## Project structure

```text
Assets/
├── Controllers/       Animator controllers
├── Effects/           Gameplay effects
├── Models/            Player model and textures
├── Prefabs/           Robots, projectiles, props and UI
├── Scenes/            Title screen and gameplay arena
├── Scripts/           Gameplay C# source code
└── Textures/UI/       Interface artwork
Packages/              Unity package manifest and lock file
ProjectSettings/       Unity project configuration
```

## Running the project

### Play the Windows build

1. Install [Git LFS](https://git-lfs.com/).
2. Clone the repository normally so that LFS downloads the game data.
3. Run [`Build/Game.exe`](Build/Game.exe).

Keep `Game.exe`, `Game_Data`, `MonoBleedingEdge`, `UnityPlayer.dll`, and
`UnityCrashHandler64.exe` together in the `Build` directory. Windows may show a
SmartScreen warning because this portfolio build is not code-signed.

### Open the Unity project

1. Install Unity Hub and Unity Editor `2022.3.56f1`.
2. Make sure [Git LFS](https://git-lfs.com/) is installed.
3. Clone the repository.
4. Open the repository directory as a project in Unity Hub.
5. Open `Assets/Scenes/Title Screen.unity` and enter Play Mode.

The scenes included in Build Settings are:

1. `Assets/Scenes/Title Screen.unity`
2. `Assets/Scenes/SampleScene.unity`

## Project status

This is a playable portfolio prototype rather than a finished commercial release. Current development priorities include refining projectile physics, improving combat feedback and audio, expanding enemy variety, and polishing the UI.

## Third-party assets

The repository contains third-party Unity assets, including TextMesh Pro resources, a particle-effect pack, and a stylized skybox. Those assets remain subject to their original authors' terms. The gameplay code in `Assets/Scripts` is the project's original implementation.

## Author

Developed by **DinEv**.

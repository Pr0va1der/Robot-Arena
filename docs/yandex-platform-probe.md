# Yandex Games integration

The project uses PluginYG2 behind the project-owned `Platform Services Adapter`. Gameplay code depends on the normalized adapter contract; only the PluginYG2 backend references `YG2` directly.

## Fixed integration target

- Unity: `2022.3.56f1` (`dd0c98481d00`).
- PluginYG2: `v2.0092`.
- Official package: <https://max-games.ru/public/pluginYG2/other/PluginYG2.unitypackage>.
- Package SHA-256: `8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3`.
- Imported production scope: PluginYG2 core, Yandex Games platform support, and EnvirData. Examples and unrelated optional product modules are not part of the integration.

`Assets/PluginYourGames/Resources/SettingsYG2.asset` keeps PluginYG2 automatic project mutation disabled. In particular, automatic Game Ready, automatic pause handling, automatic settings application, and automatic define-symbol management are disabled. The release build command remains the authority for release-only WebGL settings.

## Project-owned runtime boundary

`RobotArenaPlatformServices` is installed before the first scene and owns the adapter lifetime. Its backend is selected at compile time:

- `ROBOTARENA_PLUGINYG2` selects `RobotArenaPluginYG2Backend`.
- Without that symbol, the migration-only `RobotArenaPlatformProbeBackend` wraps the legacy probe.

The adapter exposes SDK status, environment, language, capabilities, Game Ready
state, and platform pause events. Game Ready state distinguishes a request from a
confirmed call; the PluginYG2 backend never reports confirmation merely because it
dispatched the official API call. The title menu marks the interactive boundary
once; the adapter then sends Game Ready once the backend reports an initialized
SDK. Missing or timed-out SDK data remains a usable guest mode.

PluginYG2 is the only SDK initializer after cutover. `RobotArenaPluginYG2Template` contains one `/sdk.js` loader and one `YaGames.init()` call, while PluginYG2 receives its initialization data through its normal template insertion points.

Platform pause is routed through the existing `PauseCoordinator`/`PauseMenu` lifecycle. The project owns `Time.timeScale`, audio, cursor, and gameplay pause state; PluginYG2 does not apply those policies automatically.

## Release build and local verification

Use **Robot Arena → Build WebGL release package**. The command selects `PROJECT:RobotArenaPluginYG2` temporarily, builds the enabled scenes, validates the archive budget, writes `Build/WebGL/RobotArenaRelease-upload.zip`, and restores the saved Unity project settings.

The local lifecycle check is:

```text
node Tools/RobotArenaMusicLifecycleSmoke.js --build Build/WebGL/RobotArenaRelease --enter-session --output Build/WebGL/RobotArenaMusicLifecycleSmoke-pluginyg2.json
```

The current licensed Unity release candidate passed the package gate: 20,965,688 uncompressed bytes against the 80,000,000-byte budget, with zero texture-policy changes. The local browser smoke verifies Unity loading plus the focus/music lifecycle. Its `/sdk.js` response is an inert placeholder with no `YaGames` implementation, so local results are not evidence of SDK initialization or Yandex lifecycle delivery. Direct callback testing is available only as the explicitly synthetic `--synthetic-platform-pause-cycles` diagnostic.

The release command validates the official integration before building from
`Tools/RobotArenaPluginYG2Integration.json`: PluginYG2 must match the pinned
version and provenance hash, the PluginYG2/Yandex/EnvirData WebGL defines must be
enabled, and the production template must contain one `/sdk.js` loader and one
`YaGames.init()` call. It validates the post-processed artifact as well, including
the official lifecycle and EnvirData insertion markers. The release report records
those integration coordinates and the artifact validation result.

In the Yandex draft, successful real-SDK integration is identified by the
structured runtime messages `[RobotArena.Platform] PluginYG2 SDK ready`,
`PluginYG2 Game Ready requested`, and `PluginYG2 platform pause=True/False`,
together with the official SDK's own Game Ready outcome. The request message is
not itself a confirmation. The browser template also emits a
`[RobotArena.PluginYG2.Transport]` capability diagnostic by checking entry-point
presence only; it does not call Player, leaderboard, or advertising APIs in this
migration. The backend's `[RobotArena.Platform]` report is the canonical
acceptance evidence.
These messages originate after data and events pass through the official `YG2`
APIs into `RobotArenaPluginYG2Backend`; they must be captured together with the
successful Yandex `/sdk.js` network request.

The remaining production gate is the Yandex Games draft smoke in issue #38. It must confirm SDK/environment delivery, guest behavior, title-menu readiness, pause/resume, and the same music invariants in the hosted draft. The migration parent issue remains open until that human-controlled check is recorded.

## Legacy probe during migration

`RobotArenaPlatformProbe` remains in the repository only to support rollback and migration comparison. With `ROBOTARENA_PLUGINYG2` enabled it does not install or initialize its own runtime path, so two SDK initializers cannot be active in the release build. It may be removed after issue #38 is accepted and the final draft smoke has been recorded.

The old probe build command and template are historical diagnostics, not the production release path. Do not add new gameplay dependencies on the probe or call its JavaScript bridge directly.

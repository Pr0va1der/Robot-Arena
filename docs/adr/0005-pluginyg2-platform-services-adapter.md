---
status: accepted
---

# Use PluginYG2 behind the Platform Services Adapter

The project will replace the custom Yandex Games Bridge with the pinned PluginYG2 `v2.0092` integration, while keeping gameplay independent of the vendor SDK through a project-owned Platform Services Adapter. The code cutover is implemented behind `ROBOTARENA_PLUGINYG2`; production adoption is gated by the Yandex Games draft smoke in issue #38.

## Decision

- Vendor the official PluginYG2 package at `v2.0092` with SHA-256 `8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3`.
- Import only the core, Yandex Games platform support, and EnvirData module. Keep examples, advertising, authentication, storage, leaderboard, and unrelated modules out of this cutover.
- Use `RobotArenaPlatformServices` and `PlatformServicesAdapter` as the project seam. Only `RobotArenaPluginYG2Backend` may reference `YG2`; gameplay consumes normalized status, capabilities, Game Ready, and pause events.
- Disable PluginYG2 automatic Game Ready, automatic pause policy, automatic project-setting application, and automatic define-symbol management in `Assets/PluginYourGames/Resources/SettingsYG2.asset`. The title menu marks readiness once, and the existing `PauseCoordinator` remains the authority for time, audio, cursor, and gameplay pause.
- Build releases with the derived `RobotArenaPluginYG2` WebGL template. The template has one SDK loader and one initializer and retains PluginYG2 insertion points plus the existing canvas/browser compatibility behavior.
- Keep the old Bridge only as a migration rollback/comparison path. It must not initialize alongside PluginYG2 and can be deleted after the draft smoke is accepted.

## Consequences

The project gets a maintained vendor SDK boundary and a smaller gameplay-facing contract, while preserving its existing pause and music lifecycle ownership. Plugin upgrades are deliberate package changes rather than incidental editor imports; optional platform products remain separate follow-up work. Until the draft gate passes, rollback is available by disabling the PluginYG2 define and returning to the pre-migration release path.

---
status: accepted
---

# Use PluginYG2 behind the Platform Services Adapter

The project replaces the custom Yandex Games Bridge with the pinned PluginYG2 `v2.0092` integration, while keeping gameplay independent of the vendor SDK through a project-owned Platform Services Adapter. The PluginYG2 integration compiles under `ROBOTARENA_PLUGINYG2`; after the cutover, `RobotArenaPlatformServices` always selects the PluginYG2 backend, while the backend keeps a deterministic guest path when the official platform symbols are unavailable. Hosted draft acceptance is recorded in issue #38.

## Decision

- Vendor the official PluginYG2 package at `v2.0092` with SHA-256 `8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3`.
- Import only the core, Yandex Games platform support, and EnvirData module. Keep examples, advertising, authentication, storage, leaderboard, and unrelated modules out of this cutover.
- Use `RobotArenaPlatformServices` and `PlatformServicesAdapter` as the project seam. Only `RobotArenaPluginYG2Backend` may reference `YG2`; gameplay consumes normalized status, capabilities, Game Ready, and pause events.
- Use one narrow project-owned lifecycle diagnostics channel over the existing PluginYG2 `YGSendMessage` component when the vendor callback surface cannot carry evidence back to Unity. The channel has two typed messages, `RobotArenaPlatformState` and `RobotArenaYandexLifecyclePause`, and may report the terminal SDK state, actual capabilities, and an explicit Yandex pause/resume origin. It is not a second initializer, a general-purpose bridge, or a gameplay API.
- Disable PluginYG2 automatic Game Ready, automatic pause policy, automatic project-setting application, and automatic define-symbol management in `Assets/PluginYourGames/Resources/SettingsYG2.asset`. The title menu marks readiness once, and the existing `PauseCoordinator` remains the authority for time, audio, cursor, and gameplay pause.
- Build releases with the derived `RobotArenaPluginYG2` WebGL template. The template has one SDK loader and one initializer and retains PluginYG2 insertion points plus the existing canvas/browser compatibility behavior.
- Remove the legacy Bridge, probe template and migration-only build command after the accepted draft smoke. Rollback is performed through a Git commit or tag; no dormant legacy provider remains in production.

## Consequences

The project gets a maintained vendor SDK boundary and a smaller gameplay-facing contract, while preserving its existing pause and music lifecycle ownership. Plugin upgrades are deliberate package changes rather than incidental editor imports; optional platform products remain separate follow-up work. The accepted production path contains no dormant legacy provider, and rollback is available through a prior Git commit or tag.

The derived template remains the transport owner for browser-side evidence, while the backend remains the canonical owner of normalized acceptance logs and session-facing events. Vendor files are not patched to create this channel.

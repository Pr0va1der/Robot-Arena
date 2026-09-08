# Yandex Games compatibility probe

This probe is intentionally smaller than the future Platform Services Adapter. It is a diagnostic boundary that can be uploaded with the current Unity project without adding a Yandex SDK dependency to gameplay code.

## Fixed compatibility target

- Unity: `2022.3.56f1` (`dd0c98481d00`), unchanged.
- Candidate SDK: PluginYG2 `v2.0092`.
- Source: the official PluginYG2 repository and the package download linked from its release documentation: <https://github.com/JustPlay-Max/Unity-PluginYG-2> and <https://max-games.ru/public/pluginYG2/other/PluginYG2.unitypackage>.
- Downloaded package SHA-256: `8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3`.

The package was unpacked into a disposable `.scratch/pluginyg2-2.0092` directory. It is not copied into `Assets/`: the production project must not acquire the plugin's global `YG2` initialization, generated platform symbols, or WebGL template before the draft check chooses the integration channel.

As a pre-import smoke check, all 56 non-`Editor` C# files from that package were compiled against the Unity 2022.3.56f1 managed assemblies with WebGL/player defines. The compile completed without errors (three existing package warnings); this does not replace an actual Unity importer/build run.

## Fallback bridge

`RobotArenaPlatformProbe` and `RobotArenaPlatformProbe.jslib` provide the same compatibility boundaries required by the spike:

- SDK initialization through `YaGames.init()` or an already-created `window.ysdk`;
- environment/application language read;
- `game_api_pause` / `game_api_resume` callbacks;
- callable Player data probe and structural detection of the modern `ysdk.leaderboards.getEntries` boundary, plus fullscreen-ad capability detection (the ad is not shown automatically);
- one guarded `LoadingAPI.ready()` call after the title menu has had a frame to build its interactive controls.

The bridge never blocks the title screen. If the SDK is absent or does not answer within eight seconds, the probe reports `TimedOut`/`Failed` and the game remains usable as a guest. The eventual adapter will translate these reports into the guest-mode and pause contracts from issue #6; this probe deliberately does not own gameplay pause or cloud-save policy.

The leaderboard check does not query a table, authenticate a player, or submit a score. It only checks the direct modern `ysdk.leaderboards.getEntries` method. A legacy-only `ysdk.getLeaderboards` object and malformed or missing leaderboard objects are reported as unsupported without invoking them.

`Assets/WebGLTemplates/RobotArenaYandex/index.html` loads the Yandex Games SDK before the Unity loader. The editor command `Robot Arena/Build WebGL platform probe` (or `RobotArenaPlatformProbeBuild.BuildWebGlProbe` in batch mode) temporarily selects that template and writes an upload-ready build to `Build/WebGL/RobotArenaPlatformProbe` without changing the project's saved template choice. The active build-scene list must contain `Assets/Scenes/Title Screen.unity`; the command fails early if it does not.

## Verification boundary

The package source and version are fixed above. Importing the package into an isolated Unity project was attempted with the installed Unity 2022.3.56f1 editor, but the headless editor could not acquire its existing Personal license IPC channel (`return code 199`). This is an environment limitation, not evidence that PluginYG2 is incompatible. The bridge is therefore kept as the upload-ready compatibility fallback; issue #38 must run the draft browser check and make the final channel choice before production integration.

The Node bridge harness covers modern, legacy-only, and malformed leaderboard fixtures. The modern fixture must report `SupportsLeaderboard=true` without calling the deprecated initializer or the read method; the other fixtures must report `false` without interrupting SDK initialization.

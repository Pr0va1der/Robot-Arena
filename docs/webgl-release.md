# WebGL release package

Use **Robot Arena → Build WebGL release package** in the Unity Editor. The command
builds all enabled scenes with the template declared by
`Tools/RobotArenaPluginYG2Integration.json`, creates
`RobotArenaRelease-upload.zip`, and writes a JSON report next to it. Build output
is prepared in sibling `.candidate` paths; the last successful release remains
untouched until every gate passes.

The release profile is intentionally desktop-first: it uses Brotli, DXT, engine
stripping, and WebGL managed stripping at `Low`. Before the build it applies a
WebGL-only override to every `Texture2D` (maximum 1024 px, DXT5 Crunch at quality
50). This override is stored in the texture `.meta` files, so reapplying the
texture policy is idempotent; default/Standalone import settings are not
changed. The release command cleans only the candidate output and performs a
fresh player build each time.

The gate measures the sum of uncompressed ZIP entries and fails above the
80,000,000-byte internal budget. The archive must contain exactly one root
`index.html` and all other files under `Build/`. The generated report records the
package size, per-file sizes, texture profile, stripping profile, and any gate
errors. A release is built and validated in sibling candidate paths; the final
release directory and upload archive are promoted only after every gate passes.
If a build or validation fails, the previous successful candidate remains in
place.

## Music lifecycle smoke

After creating a fresh release package, run the browser lifecycle check from the repository root:

```text
node Tools/RobotArenaMusicLifecycleSmoke.js --build Build/WebGL/RobotArenaRelease --enter-session --output Build/WebGL/RobotArenaMusicLifecycleSmoke.json
```

The check requires Node 22 or newer and Microsoft Edge. It serves the Brotli package over a local HTTP server, enables audio with a real pointer gesture, switches to a background tab during the first calm intro, restores the game focus, and instruments WebAudio buffer starts and stops. It fails on console or resource errors, same-sequence audible overlap, more than one authoritative zero-offset loop for a semantic sequence, a loop start during focus loss, or a loop start before the resumed intro has finished. WebGL backend restarts with a positive resume offset are retained as evidence and are not counted as duplicate sequence starts. With `--enter-session`, distinct WebAudio buffer identities also verify that the initial calm cue reaches its loop before the combat cue begins. The local server returns an inert `/sdk.js` placeholder only to avoid a missing-resource error; it neither defines `YaGames` nor emulates the Yandex SDK. The JSON output is music/focus evidence, not platform SDK acceptance evidence. It also records `pluginYG2Init` so a local run proves that the non-SDK path was explicitly selected (`local`, `failed`, or `timeout`) instead of silently pretending to have a Yandex SDK.

For a local diagnostic of the generated PluginYG2 transport-to-Unity path, add
`--synthetic-platform-pause-cycles 2`. This invokes the generated template
callbacks directly. It is useful for regression diagnosis, but it does not prove
that Yandex delivered `game_api_pause` or `game_api_resume`; its JSON result is
marked with `platformPauseEvidence: "synthetic-template-transport-only"`; the
synthetic cycle does not create a hosted platform-pause window.

To cover the pre-start lead-window and the combat intro in the same local run,
use the optional timing matrix:

```text
node Tools/RobotArenaMusicLifecycleSmoke.js --build Build/WebGL/RobotArenaRelease --enter-session --cover-lead-window --cover-combat-intro --focus-cycles 1 --output Build/WebGL/RobotArenaMusicLifecycleSmoke-matrix.json
```

The lead-window option moves the first focus loss immediately after the audio
gesture. The combat-intro option waits for the post-calm combat intro before
switching focus. For hosted release validation, repeat this matrix in the
Yandex Games draft and verify the same one-source, positive-offset resume and
no-early-loop invariants from the saved trace.

## Real Yandex SDK gate

The production package uses the official PluginYG2 integration. The release build
reads `Tools/RobotArenaPluginYG2Integration.json` as the single integration
manifest and fails unless its pinned PluginYG2 version, source archive SHA-256,
vendored fingerprint, required WebGL scripting defines, exactly one `/sdk.js`
loader and `YaGames.init()` across the generated files, and minimal module set are
present. After Unity and the PluginYG2 postprocessor finish, the validator checks
the generated artifact and then repeats the lifecycle checks against the upload
archive; validating the source template alone is insufficient. The generated
report records the actual integration coordinates, artifact/archive checksums,
validation stage/errors, and `releaseIsUploadReady`.

The template bounds `YaGames.init()` by eight seconds. A successful init enters
the official PluginYG2 path; a rejection, missing SDK, or timeout starts Unity in
guest mode and records the outcome in `window.__robotArenaPluginYG2`. A late SDK
resolution does not silently replace an already-started guest session.

Upload `RobotArenaRelease-upload.zip` to a Yandex Games draft and run it inside the
real platform iframe. Save console and network evidence showing:

- `/sdk.js` loaded successfully from Yandex;
- `[RobotArena.Platform] PluginYG2 SDK ready` with the draft app ID and language;
- `[RobotArena.Platform] PluginYG2 capability report=...` with the observed
  Loading API, Player, leaderboard, and fullscreen-ad entry points;
- `[RobotArena.Platform] PluginYG2 Game Ready requested` exactly once;
- the official SDK's `Game Ready` result, without treating the C# request log as
  confirmation;
- platform-delivered pause and resume logs from
  `[RobotArena.Platform] PluginYG2 platform pause=...`;
- no alternate platform initializer or general-purpose custom bridge calls. The
  explicitly documented `RobotArenaPlatformState` and
  `RobotArenaYandexLifecyclePause` messages are the single narrow project-owned
  lifecycle transport channel; their `[RobotArena.PluginYG2.Transport]` logs are
  diagnostics, not acceptance evidence.

Only this hosted draft evidence satisfies the platform SDK gate.

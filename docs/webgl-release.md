# WebGL release package

Use **Robot Arena → Build WebGL release package** in the Unity Editor. The command
cleans `Build/WebGL/RobotArenaRelease`, builds all enabled scenes with the
`RobotArenaYandex` template, creates `RobotArenaRelease-upload.zip`, and writes a
JSON report next to it.

The release profile is intentionally desktop-first: it uses Brotli, DXT, engine
stripping, and WebGL managed stripping at `Low`. Before the build it applies a
WebGL-only override to every `Texture2D` (maximum 1024 px, DXT5 Crunch at quality
50). This override is stored in the texture `.meta` files, so reapplying the
texture policy is idempotent; default/Standalone import settings are not
changed. The release command still cleans its output directory and performs a
fresh player build each time.

The gate measures the sum of uncompressed ZIP entries and fails above the
80,000,000-byte internal budget. The archive must contain exactly one root
`index.html` and all other files under `Build/`. The generated report records the
package size, per-file sizes, texture profile, stripping profile, and any gate
errors.

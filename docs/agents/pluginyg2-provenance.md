# PluginYG2 provenance

The release manifest pins PluginYG2 `v2.0092`, the upstream archive SHA-256, and a fingerprint of the imported `Assets/PluginYourGames` tree. The release build never downloads a package; it verifies the imported tree and the required module files before building.

When importing or updating the package, keep the upstream archive outside the build output and run:

```powershell
node Tools/RobotArenaPluginYG2Provenance.js --project . --archive <path-to-upstream-unitypackage>
```

The command compares the actual archive bytes with `sourceArchiveSha256` and the resulting imported tree with `vendoredFingerprint`. Update the manifest receipt only as part of a deliberate, reviewed PluginYG2 version/import change. For a release checkout without the upstream archive, the same tree check can be run with `--no-archive`; the Unity release gate performs that offline tree check automatically.

# Unity Editor and licensing

## Local installation

- The project uses Unity `2022.3.56f1`.
- The licensed Editor is installed at `D:\Programs Data\Unity\Editor\2022.3.56f1\Editor\Unity.exe`.
- The Unity CLI is available at `%LOCALAPPDATA%\Unity\bin\unity.exe`.
- Licensing is managed by Unity Hub and the Unity Licensing Client bundled with the Editor. Do not edit, copy, or commit license files, entitlement data, or tokens.
- License data is user-local under `%LOCALAPPDATA%\Unity\licenses\` and is not part of the repository.

## Checking the license

From PowerShell, use the Unity CLI rather than inspecting license files:

```powershell
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
& $unityCli --format json license
```

If the command reports `LICENSING_CLIENT_UNAVAILABLE`, it means that the current terminal cannot reach the local Unity Licensing Client (common in a restricted or sandboxed execution environment). It does not by itself mean that the license is absent. Repeat the command with host/elevated execution access so the CLI can use the local licensing IPC channel. Do not delete or replace the license files as a workaround.

## Running Unity tests

Use the CLI with the explicit licensed Editor path so the project is opened with the expected Unity version:

```powershell
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
$editorPath = 'D:\Programs Data\Unity\Editor\2022.3.56f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
$resultPath = Join-Path $env:TEMP 'robot-arena-playmode.xml'

& $unityCli --no-banner --non-interactive test $projectPath `
  --mode PlayMode `
  --filter 'RobotArena.PlayerWeapon.Tests.GunRotationTests' `
  --output $resultPath `
  --editor-path $editorPath `
  --timeout 180
```

Use `--mode EditMode` for EditMode tests, change or omit `--filter` to select another test set, and keep result files outside the repository when possible. If the test command cannot reach the licensing client, rerun the same command with host/elevated execution access before diagnosing the project or the Unity installation.

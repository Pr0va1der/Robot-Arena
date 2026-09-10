const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const templatePath = path.join(
  __dirname,
  '..',
  'Assets',
  'WebGLTemplates',
  'RobotArenaPluginYG2',
  'index.html');
const templateSource = fs.readFileSync(templatePath, 'utf8');
const releaseBuildPath = path.join(
  __dirname,
  '..',
  'Assets',
  'Editor',
  'RobotArenaWebGLReleaseBuild.cs');
const releaseBuildSource = fs.readFileSync(releaseBuildPath, 'utf8');
const backendPath = path.join(
  __dirname,
  '..',
  'Assets',
  'Scripts',
  'Platform',
  'RobotArenaPluginYG2Backend.cs');
const backendSource = fs.readFileSync(backendPath, 'utf8');
const projectSettingsPath = path.join(
  __dirname,
  '..',
  'ProjectSettings',
  'ProjectSettings.asset');
const projectSettingsSource = fs.readFileSync(projectSettingsPath, 'utf8');
const manifestPath = path.join(
  __dirname,
  'RobotArenaPluginYG2Integration.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const repositoryRoot = path.join(__dirname, '..');
const pluginVersion = fs.readFileSync(
  path.join(repositoryRoot, manifest.versionFile),
  'utf8').trim();

test('PluginYG2 template keeps one Yandex SDK loader and plugin insertion seams', () => {
  assert.equal((templateSource.match(/<script[^>]+src="\/sdk\.js"/g) || []).length, 1);
  assert.equal((templateSource.match(/YaGames\.init\(\)/g) || []).length, 1);
  assert.match(
    templateSource,
    /async function InitYSDK\(\)[\s\S]*?Promise\.race\([\s\S]*?YaGames\.init\(\)/);
  assert.match(templateSource, /PLUGIN_YG2_INIT_TIMEOUT_MS/);
  assert.match(templateSource, /window\.__robotArenaPluginYG2/);
  assert.match(templateSource, /RecordPluginYG2Capabilities/);
  assert.match(templateSource, /getPlayer/);
  assert.match(templateSource, /getLeaderboards/);
  assert.match(templateSource, /showFullscreenAdv/);
  assert.match(templateSource, /Additional init0 modules/);
  assert.match(templateSource, /Additional init1 modules/);
  assert.match(templateSource, /Additional init2 modules/);
  assert.match(templateSource, /Additional init modules/);
  assert.match(templateSource, /Additional script modules/);
  assert.match(templateSource, /Additional start modules/);
  assert.doesNotMatch(templateSource, /RobotArenaPlatformProbe|robotArenaPlatformProbeFlushPendingMessages/);
  assert.match(templateSource, /window\.unityInstance\s*=\s*unityInstance/);
});

test('PluginYG2 template lets the canvas track the host presentation area', () => {
  assert.match(
    templateSource,
    /html\s*,\s*body\s*\{[\s\S]*?width:\s*100%[\s\S]*?height:\s*100%[\s\S]*?overflow:\s*hidden[\s\S]*?\}/);
  assert.match(
    templateSource,
    /#unity-canvas\s*\{[\s\S]*?display:\s*block[\s\S]*?width:\s*100%[\s\S]*?height:\s*100%[\s\S]*?\}/);
  assert.match(
    templateSource,
    /<canvas[^>]*\bwidth=\{\{\{ WIDTH \}\}\}[^>]*\bheight=\{\{\{ HEIGHT \}\}\}/);
  assert.doesNotMatch(
    templateSource,
    /<canvas[^>]*\bstyle="[^"]*\b(?:width|height):\s*\{\{\{\s*(?:WIDTH|HEIGHT)\s*\}\}\}\s*px/);
  assert.doesNotMatch(
    templateSource,
    /^\s*matchWebGLToCanvasSize\s*:\s*false/m);
});

test('WebGL release build selects the PluginYG2 template', () => {
  assert.match(releaseBuildSource, /PROJECT:RobotArenaPluginYG2/);
  assert.doesNotMatch(releaseBuildSource, /PROJECT:RobotArenaYandex/);
});

test('WebGL release is pinned to the official PluginYG2 runtime', () => {
  assert.equal(pluginVersion, manifest.pluginVersion);
  assert.match(manifest.sourceArchiveSha256, /^[0-9A-F]{64}$/);
  assert.match(manifest.vendoredFingerprint, /^[0-9A-F]{64}$/);
  assert.equal(manifest.platform, 'YandexGamesPlatform_yg');
  for (const define of manifest.requiredDefines) {
    assert.match(projectSettingsSource, new RegExp(`WebGL:.*\\b${define}\\b`));
  }
  assert.equal(manifest.modules.sort().join(','), 'Core,EnvirData,YandexGames');
  assert.match(releaseBuildSource, /ValidatePluginYG2ReleaseConfiguration\(\)/);
  assert.match(releaseBuildSource, /ComputePluginYG2VendoredFingerprint/);
  assert.match(releaseBuildSource, /GetPluginYG2ArtifactErrors/);
});

test('platform backend uses PluginYG2 public events and APIs', () => {
  assert.match(backendSource, /YG\.YG2\.onGetSDKData\s*\+=\s*OnSdkData/);
  assert.match(backendSource, /RobotArenaPluginYG2RuntimeChannel\.StateChanged\s*\+=\s*OnRuntimeStateChanged/);
  assert.match(backendSource, /RobotArenaPluginYG2RuntimeChannel\.PlatformPauseChanged\s*\+=\s*OnPlatformPauseChanged/);
  assert.doesNotMatch(backendSource, /YG\.YG2\.onPauseGame/);
  assert.match(backendSource, /YG\.YG2\.GameReadyAPI\(\)/);
  assert.match(backendSource, /YG\.YG2\.envir\.language/);
  assert.match(backendSource, /\[RobotArena\.Platform\] PluginYG2 SDK ready/);
  assert.match(backendSource, /\[RobotArena\.Platform\] PluginYG2 Game Ready requested/);
  assert.match(backendSource, /\[RobotArena\.Platform\] PluginYG2 platform pause=/);
  assert.match(backendSource, /WithGameReadyUnavailable/);
  assert.doesNotMatch(backendSource, /DllImport|RobotArenaPlatformProbe/);
});

test('template reports transport evidence through one explicit lifecycle channel', () => {
  assert.match(templateSource, /RobotArenaPlatformState/);
  assert.match(templateSource, /RobotArenaPlatformPause/);
  assert.match(templateSource, /PluginYG2\.Transport/);
  assert.doesNotMatch(templateSource, /\[RobotArena\.Platform\] PluginYG2 platform pause=/);
});

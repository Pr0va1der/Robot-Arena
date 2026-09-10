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

test('PluginYG2 template keeps one Yandex SDK loader and plugin insertion seams', () => {
  assert.equal((templateSource.match(/<script[^>]+src="\/sdk\.js"/g) || []).length, 1);
  assert.equal((templateSource.match(/YaGames\.init\(\)/g) || []).length, 1);
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

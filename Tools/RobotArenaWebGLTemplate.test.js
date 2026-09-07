const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const templatePath = path.join(
  __dirname,
  '..',
  'Assets',
  'WebGLTemplates',
  'RobotArenaYandex',
  'index.html');
const templateSource = fs.readFileSync(templatePath, 'utf8');

test('Yandex WebGL template lets the canvas track the host presentation area', () => {
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

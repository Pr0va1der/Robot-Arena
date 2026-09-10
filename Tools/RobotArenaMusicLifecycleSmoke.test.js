const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {
  analyzeMusicTrace,
  assertLocalPluginYG2Fallback,
  assertMusicLifecycle,
  assertSemanticModeTransition,
  createStaticServer,
} = require('./RobotArenaMusicLifecycleSmoke.js');

const smokeSource = fs.readFileSync(
  path.join(__dirname, 'RobotArenaMusicLifecycleSmoke.js'),
  'utf8');

function createTrace(loopContextTime) {
  return [
    {
      type: 'start',
      id: 1,
      wallTime: 1000,
      contextTime: 1,
      when: 0,
      offset: 0,
      duration: null,
      bufferId: 1,
      bufferDuration: 10,
      loop: false,
    },
    {
      type: 'stop',
      id: 1,
      wallTime: 2500,
      contextTime: 2.5,
      when: 0,
    },
    {
      type: 'start',
      id: 2,
      wallTime: 3000,
      contextTime: 3,
      when: 0,
      offset: 2,
      duration: null,
      bufferId: 1,
      bufferDuration: 10,
      loop: false,
    },
    {
      type: 'start',
      id: 3,
      wallTime: loopContextTime * 1000,
      contextTime: loopContextTime,
      when: 0,
      offset: 0,
      duration: null,
      bufferId: 2,
      bufferDuration: 20,
      loop: true,
    },
  ];
}

test('accepts one loop after the resumed intro completes', () => {
  const result = analyzeMusicTrace(createTrace(11), {
    focusLostAt: 2000,
    focusRestoredAt: 4000,
  });

  assert.equal(result.loopStarts.length, 1);
  assert.equal(result.loopStartsDuringFocus.length, 0);
  assert.equal(result.earlyLoopStarts.length, 0);
  assert.equal(result.sameSequenceAudibleOverlaps.length, 0);
  assert.equal(result.resumedIntroStarts.length, 1);

  assert.doesNotThrow(() => assertMusicLifecycle(result));
});

test('rejects a loop that starts before the resumed intro ends', () => {
  const result = analyzeMusicTrace(createTrace(4), {
    focusLostAt: 2000,
    focusRestoredAt: 4000,
  });

  assert.equal(result.earlyLoopStarts.length, 1);
  assert.equal(result.sameSequenceAudibleOverlaps.length, 1);
  assert.throws(
    () => assertMusicLifecycle(result),
    /starts before the intro finishes/);
});

test('rejects a loop scheduled from the requested time before a delayed intro ends', () => {
  const result = analyzeMusicTrace([
    {
      type: 'start',
      id: 1,
      wallTime: 1000,
      contextTime: 1,
      when: 4,
      offset: 0,
      duration: null,
      bufferId: 1,
      bufferDuration: 10,
      loop: false,
    },
    {
      type: 'start',
      id: 2,
      wallTime: 13000,
      contextTime: 13,
      when: 13,
      offset: 0,
      duration: null,
      bufferId: 2,
      bufferDuration: 20,
      loop: true,
    },
  ]);

  assert.equal(result.earlyLoopStarts.length, 1);
  assert.throws(
    () => assertMusicLifecycle(result),
    /starts before the intro finishes/);
});

test('classifies short music clips and ignores immediately stopped WebAudio nodes', () => {
  const result = analyzeMusicTrace([
    {
      type: 'start',
      id: 1,
      wallTime: 1000,
      contextTime: 1,
      when: 1,
      offset: 0,
      duration: null,
      bufferDuration: 7.3,
      loop: false,
    },
    {
      type: 'stop',
      id: 1,
      wallTime: 1000,
      contextTime: 1,
      when: 1,
    },
    {
      type: 'start',
      id: 2,
      wallTime: 1000,
      contextTime: 1,
      when: 1,
      offset: 0,
      duration: null,
      bufferDuration: 7.3,
      loop: false,
    },
    {
      type: 'stop',
      id: 2,
      wallTime: 8300,
      contextTime: 8.3,
      when: 8.3,
    },
    {
      type: 'start',
      id: 3,
      wallTime: 8300,
      contextTime: 8.3,
      when: 8.3,
      offset: 0,
      duration: null,
      bufferDuration: 7.3,
      loop: false,
    },
    {
      type: 'stop',
      id: 3,
      wallTime: 8300,
      contextTime: 8.3,
      when: 8.3,
    },
    {
      type: 'start',
      id: 4,
      wallTime: 8300,
      contextTime: 8.3,
      when: 8.3,
      offset: 0,
      duration: null,
      bufferDuration: 7.3,
      loop: true,
    },
  ]);

  assert.equal(result.introStarts.length, 1);
  assert.equal(result.loopStarts.length, 1);
  assert.equal(result.earlyLoopStarts.length, 0);
  assert.doesNotThrow(() => assertMusicLifecycle(result));
});

test('can validate one loop for each sequential semantic music mode', () => {
  const trace = createTrace(11);
  trace.push(
    {
      type: 'start',
      id: 5,
      wallTime: 11000,
      contextTime: 11,
      when: 11,
      offset: 0,
      duration: null,
      bufferId: 3,
      bufferDuration: 7,
      loop: false,
    },
    {
      type: 'start',
      id: 6,
      wallTime: 18500,
      contextTime: 18.5,
      when: 18.5,
      offset: 0,
      duration: null,
      bufferId: 4,
      bufferDuration: 7,
      loop: true,
    });

  const result = analyzeMusicTrace(trace);

  assert.equal(result.loopStarts.length, 2);
  assert.equal(result.postLoopIntroStarts.length, 1);
  assert.doesNotThrow(() => assertMusicLifecycle(result, { expectedLoopStarts: 2 }));
  assert.doesNotThrow(() => assertSemanticModeTransition(result));
});

test('does not count a resumed post-loop intro as a semantic transition', () => {
  const trace = createTrace(11);
  trace.push(
    {
      type: 'start',
      id: 5,
      wallTime: 11000,
      contextTime: 11,
      when: 11,
      offset: 0,
      duration: null,
      bufferId: 3,
      bufferDuration: 7,
      loop: false,
    },
    {
      type: 'start',
      id: 6,
      wallTime: 18500,
      contextTime: 18.5,
      when: 18.5,
      offset: 0,
      duration: null,
      bufferId: 4,
      bufferDuration: 7,
      loop: true,
    },
    {
      type: 'start',
      id: 7,
      wallTime: 19000,
      contextTime: 19,
      when: 19,
      offset: 1,
      duration: null,
      bufferId: 3,
      bufferDuration: 7,
      loop: false,
    });

  const result = analyzeMusicTrace(trace);

  assert.equal(result.postLoopIntroStarts.length, 1);
  assert.doesNotThrow(() => assertMusicLifecycle(result, { expectedLoopStarts: 2 }));
  assert.doesNotThrow(() => assertSemanticModeTransition(result));
});

test('serves Brotli-compressed Unity assets at their logical URL', async () => {
  const buildDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), 'robot-arena-music-server-'));
  const assetDirectory = path.join(buildDirectory, 'Build');
  fs.mkdirSync(assetDirectory);
  fs.writeFileSync(
    path.join(assetDirectory, 'Game.framework.js.br'),
    Buffer.from('compressed fixture'));

  const staticServer = await createStaticServer(buildDirectory);
  try {
    const response = await fetch(
      `http://127.0.0.1:${staticServer.port}/Build/Game.framework.js`);

    assert.equal(response.status, 200);
    assert.equal(response.headers.get('content-encoding'), 'br');
    assert.equal(response.headers.get('content-type'), 'application/javascript; charset=utf-8');
  } finally {
    await new Promise(resolve => staticServer.server.close(resolve));
    fs.rmSync(buildDirectory, { recursive: true, force: true });
  }
});

test('serves a non-SDK placeholder locally without emulating YaGames', async () => {
  const buildDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), 'robot-arena-music-sdk-server-'));
  const staticServer = await createStaticServer(buildDirectory);
  try {
    const response = await fetch(
      `http://127.0.0.1:${staticServer.port}/sdk.js`);

    assert.equal(response.status, 200);
    assert.equal(response.headers.get('content-type'), 'application/javascript; charset=utf-8');
    const sdkPlaceholder = await response.text();
    assert.match(sdkPlaceholder, /local music smoke/i);
    assert.doesNotMatch(sdkPlaceholder, /YaGames|\.init\s*\(/);

    const faviconResponse = await fetch(
      `http://127.0.0.1:${staticServer.port}/favicon.ico`);
    assert.equal(faviconResponse.status, 204);
  } finally {
    await new Promise(resolve => staticServer.server.close(resolve));
    fs.rmSync(buildDirectory, { recursive: true, force: true });
  }
});

test('marks direct PluginYG2 callback evidence as synthetic', () => {
  assert.match(smokeSource, /platformPauseEvidence:[\s\S]*synthetic-template-callback/);
  assert.match(smokeSource, /bypasses ysdk\.on/);
});

test('requires local smoke to report an explicit non-SDK PluginYG2 state', () => {
  assert.doesNotThrow(() => assertLocalPluginYG2Fallback({ initState: 'local' }));
  assert.doesNotThrow(() => assertLocalPluginYG2Fallback({ initState: 'failed' }));
  assert.doesNotThrow(() => assertLocalPluginYG2Fallback({ initState: 'timeout' }));
  assert.throws(
    () => assertLocalPluginYG2Fallback({ initState: 'ready' }),
    /explicit non-SDK PluginYG2 state/);
});

test('rejects a music loop that starts during a PluginYG2 platform pause', () => {
  const result = analyzeMusicTrace(createTrace(11), {
    platformPauseWindows: [{ pausedAt: 900, resumedAt: 3500 }],
  });

  assert.equal(result.loopStartsDuringPlatformPause.length, 0);

  const pausedTrace = createTrace(3);
  const pausedResult = analyzeMusicTrace(pausedTrace, {
    platformPauseWindows: [{ pausedAt: 2500, resumedAt: 3500 }],
  });
  assert.equal(pausedResult.loopStartsDuringPlatformPause.length, 1);
  assert.throws(
    () => assertMusicLifecycle(pausedResult),
    /PluginYG2 platform pause/);
});

test('allows a WebGL loop source to resume from a saved offset', () => {
  const trace = createTrace(11);
  trace.push(
    {
      type: 'stop',
      id: 3,
      wallTime: 12000,
      contextTime: 12,
      when: 12,
      loop: false,
    },
    {
      type: 'start',
      id: 5,
      wallTime: 12500,
      contextTime: 12.5,
      when: 12.5,
      offset: 0.5,
      duration: null,
      bufferDuration: 20,
      loop: true,
    });

  const result = analyzeMusicTrace(trace);

  assert.equal(result.initialLoopStarts.length, 1);
  assert.equal(result.resumedLoopStarts.length, 1);
  assert.doesNotThrow(() => assertMusicLifecycle(result));
});

test('rejects a second authoritative loop start at zero offset', () => {
  const trace = createTrace(11);
  trace.push({
    type: 'start',
    id: 5,
    wallTime: 12000,
    contextTime: 12,
    when: 12,
    offset: 0,
    duration: null,
    bufferDuration: 20,
    loop: true,
  });

  const result = analyzeMusicTrace(trace);

  assert.equal(result.initialLoopStarts.length, 2);
  assert.throws(
    () => assertMusicLifecycle(result),
    /authoritative music loop/);
});

test('rejects overlapping starts for one music buffer', () => {
  const trace = createTrace(11);
  trace.push({
    type: 'start',
    id: 5,
    wallTime: 12000,
    contextTime: 12,
    when: 12,
    offset: 0,
    duration: null,
    bufferId: 2,
    bufferDuration: 20,
    loop: true,
  });

  const result = analyzeMusicTrace(trace);

  assert.equal(result.sameSequenceAudibleOverlaps.length, 1);
  assert.throws(
    () => assertMusicLifecycle(result),
    /same-sequence audible source overlap/);
});

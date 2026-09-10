const assert = require('node:assert/strict');
const test = require('node:test');

const {
  analyzeMusicTrace,
  assertMusicLifecycle,
  assertSemanticModeTransition,
} = require('./RobotArenaMusicLifecycleSmoke.js');

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

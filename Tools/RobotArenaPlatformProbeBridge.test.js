const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const bridgeSource = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'Plugins', 'WebGL', 'RobotArenaPlatformProbe.jslib'),
  'utf8');

function createHarness(readyImplementation = null) {
  const calls = [];
  let readyCalls = 0;
  const sdk = {
    features: {
      LoadingAPI: {
        ready() {
          readyCalls += 1;
          if (readyImplementation) {
            readyImplementation();
          }
        }
      }
    },
    environment: {
      app: { id: 'test-app' },
      i18n: { lang: 'ru' }
    },
    on() {},
    getPlayer() {},
    getLeaderboards() {}
  };
  const context = {
    LibraryManager: { library: {} },
    mergeInto(target, source) {
      Object.assign(target, source);
    },
    UTF8ToString(value) {
      return value;
    },
    console: { info() {}, warn() {} },
    setTimeout,
    clearTimeout,
    Promise,
    window: { ysdk: sdk }
  };

  vm.runInNewContext(bridgeSource, context, {
    filename: 'RobotArenaPlatformProbe.jslib'
  });

  return {
    begin: context.LibraryManager.library.RobotArenaPlatformProbe_Begin,
    markReady: context.LibraryManager.library.RobotArenaPlatformProbe_MarkGameReady,
    context,
    calls,
    get readyCalls() {
      return readyCalls;
    },
    attachUnity() {
      context.window.unityInstance = {
        SendMessage(...args) {
          calls.push(args);
        }
      };
    }
  };
}

function flushMicrotasks() {
  return new Promise(resolve => setImmediate(resolve));
}

test('buffers SDK snapshot until Unity receiver exists and flushes it', async () => {
  const harness = createHarness();

  harness.begin('RobotArenaPlatformProbe', 1000);
  await flushMicrotasks();

  assert.equal(harness.calls.length, 0);
  assert.equal(harness.context.window.__robotArenaPlatformProbe.snapshot.sdkInitialized, true);
  assert.equal(
    harness.context.window.__robotArenaPlatformProbe.pendingMessages.length,
    1);

  harness.attachUnity();
  harness.context.window.__robotArenaPlatformProbe.flushPendingMessages();

  assert.equal(harness.calls.length, 1);
  assert.equal(harness.calls[0][0], 'RobotArenaPlatformProbe');
  assert.equal(harness.calls[0][1], 'OnPlatformProbeResult');
});

test('marks Game Ready once after a delayed receiver is attached', async () => {
  const harness = createHarness();

  harness.begin('RobotArenaPlatformProbe', 1000);
  await flushMicrotasks();
  harness.attachUnity();
  harness.context.window.__robotArenaPlatformProbe.flushPendingMessages();

  harness.markReady('RobotArenaPlatformProbe');
  harness.markReady('RobotArenaPlatformProbe');

  assert.equal(harness.readyCalls, 1);
  assert.equal(harness.context.window.__robotArenaPlatformProbe.gameReady, true);
  assert.equal(harness.calls.length, 2);
  assert.equal(harness.calls[1][1], 'OnPlatformProbeResult');
});

test('does not call LoadingAPI.ready again after a failed call', async () => {
  const harness = createHarness(() => {
    throw new Error('ready failed');
  });

  harness.begin('RobotArenaPlatformProbe', 1000);
  await flushMicrotasks();
  harness.attachUnity();
  harness.context.window.__robotArenaPlatformProbe.flushPendingMessages();

  harness.markReady('RobotArenaPlatformProbe');
  harness.markReady('RobotArenaPlatformProbe');

  assert.equal(harness.readyCalls, 1);
  assert.equal(harness.context.window.__robotArenaPlatformProbe.gameReady, false);
  assert.equal(harness.calls.length, 2);
  assert.match(harness.calls[1][2], /ready failed/);
});

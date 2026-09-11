'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');

const templatePath = path.join(
  __dirname,
  '..',
  'Assets',
  'WebGLTemplates',
  'RobotArenaPluginYG2',
  'index.html');
const templateSource = fs.readFileSync(templatePath, 'utf8');
const inlineScriptSource = templateSource.match(/<script>\s*([\s\S]*?)\s*<\/script>\s*<\/body>/)[1]
  .replace(/^\s*#(?:if|endif).*$/gm, '');

function wait(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

function createFakeElement(tagName) {
  return {
    tagName: tagName.toUpperCase(),
    style: {},
    className: '',
    isContentEditable: false,
    parentNode: null,
    focus() {
      fakeDocument.activeElement = this;
    },
    requestPointerLock() {
      return Promise.resolve();
    },
  };
}

let fakeDocument;

function createHarness({
  localHost = false,
  sdkMode = 'resolve',
  capabilities = {},
  gameReadyResult,
  unityMode = 'resolve',
} = {}) {
  const messages = [];
  const logs = [];
  const errors = [];
  const listeners = new Map();
  let sdkResolve;
  let sdkReject;
  let unityResolve;
  const sdkPromise = new Promise((resolve, reject) => {
    sdkResolve = resolve;
    sdkReject = reject;
  });
  const unityPromise = new Promise(resolve => {
    unityResolve = resolve;
  });
  const sdkHandlers = new Map();
  const realSetTimeout = setTimeout;
  const realClearTimeout = clearTimeout;
  const scaledSetTimeout = (callback, milliseconds, ...args) => realSetTimeout(
    callback,
    milliseconds >= 1_000 ? 8 : Math.min(milliseconds, 5),
    ...args);
  const scaledClearTimeout = handle => realClearTimeout(handle);

  const windowObject = {
    location: { hostname: localHost ? 'localhost' : 'yandex.example' },
    addEventListener(type, handler) {
      listeners.set(`window:${type}`, handler);
    },
    removeEventListener(type) {
      listeners.delete(`window:${type}`);
    },
    requestAnimationFrame(callback) {
      return scaledSetTimeout(callback, 0);
    },
  };
  windowObject.top = windowObject;

  const elements = new Map([
    ['#unity-container', createFakeElement('div')],
    ['#unity-canvas', createFakeElement('canvas')],
    ['#loading-cover', createFakeElement('div')],
    ['#unity-progress-bar-empty', createFakeElement('div')],
    ['#unity-progress-bar-full', createFakeElement('div')],
    ['.spinner', createFakeElement('div')],
  ]);
  fakeDocument = {
    hidden: false,
    activeElement: null,
    onblur: null,
    body: {
      appendChild(element) {
        element.parentNode = this;
        if (element.src && typeof element.onload === 'function') {
          element.onload();
        }
        return element;
      },
      removeChild(element) {
        element.parentNode = null;
      },
    },
    createElement(tagName) {
      return createFakeElement(tagName);
    },
    querySelector(selector) {
      return elements.get(selector) || null;
    },
    addEventListener(type, handler) {
      listeners.set(`document:${type}`, handler);
    },
    removeEventListener(type) {
      listeners.delete(`document:${type}`);
    },
  };

  const sdk = {
    features: {
      LoadingAPI: capabilities.loadingApi === false
        ? undefined
        : { ready() { return gameReadyResult; } },
    },
    adv: {
      showFullscreenAdv: capabilities.fullscreenAds === false ? undefined : () => {},
    },
    getPlayer: capabilities.playerData === false ? undefined : () => Promise.resolve({}),
    getLeaderboards: capabilities.leaderboard === false
      ? undefined
      : () => Promise.resolve({}),
    on(eventName, handler) {
      sdkHandlers.set(eventName, handler);
    },
  };

  const consoleObject = {
    log(message) {
      logs.push(String(message));
    },
    error(message) {
      errors.push(String(message));
    },
  };
  const context = {
    console: consoleObject,
    document: fakeDocument,
    Element: function Element() {},
    navigator: { userAgent: '' },
    window: windowObject,
    setTimeout: scaledSetTimeout,
    clearTimeout: scaledClearTimeout,
    requestAnimationFrame: windowObject.requestAnimationFrame,
    createUnityInstance(_canvas, _config, onProgress) {
      onProgress(1);
      const unityInstance = {
        SendMessage(objectName, method, argument) {
          messages.push({ objectName, method, argument });
        },
      };
      return unityMode === 'deferred' ? unityPromise : Promise.resolve(unityInstance);
    },
  };
  context.Element.prototype.requestPointerLock = function requestPointerLock() {
    return Promise.resolve();
  };

  if (sdkMode !== 'missing') {
    context.YaGames = {
      init() {
        if (sdkMode === 'reject') {
          return Promise.reject(new Error('synthetic SDK rejection'));
        }
        if (sdkMode === 'never') {
          return new Promise(() => {});
        }
        if (sdkMode === 'invalid') {
          return Promise.resolve({});
        }
        return sdkPromise;
      },
    };
    windowObject.YaGames = context.YaGames;
  }

  vm.createContext(context);
  vm.runInContext(inlineScriptSource, context, { timeout: 2_000 });

  return {
    context,
    logs,
    errors,
    messages,
    sdkHandlers,
    resolveSdk() {
      sdkResolve(sdk);
    },
    rejectSdk(error = new Error('synthetic late rejection')) {
      sdkReject(error);
    },
    resolveUnity() {
      unityResolve({
        SendMessage(objectName, method, argument) {
          messages.push({ objectName, method, argument });
        },
      });
    },
    async settle(milliseconds = 30) {
      await wait(milliseconds);
      await Promise.resolve();
    },
    getState() {
      return JSON.parse(JSON.stringify(context.window.__robotArenaPluginYG2));
    },
    realSetTimeout,
  };
}

test('browser harness accepts SDK resolve and records actual capabilities', async () => {
  const harness = createHarness({ sdkMode: 'resolve' });
  harness.resolveSdk();
  await harness.settle();

  const state = harness.getState();
  assert.equal(state.initState, 'ready');
  assert.deepEqual(state.capabilities, {
    loadingApi: true,
    playerData: true,
    leaderboard: true,
    fullscreenAds: true,
  });
  assert.ok(harness.sdkHandlers.has('game_api_pause'));
  assert.ok(harness.sdkHandlers.has('game_api_resume'));

  const withoutLoadingApi = createHarness({
    sdkMode: 'resolve',
    capabilities: { loadingApi: false },
  });
  withoutLoadingApi.resolveSdk();
  await withoutLoadingApi.settle();
  assert.equal(withoutLoadingApi.getState().initState, 'ready');
  assert.equal(withoutLoadingApi.getState().capabilities.loadingApi, false);
});

test('browser harness turns SDK rejection, absence, and timeout into guest states', async () => {
  for (const sdkMode of ['reject', 'missing', 'never']) {
    const harness = createHarness({ sdkMode });
    await harness.settle(35);

    const state = harness.getState();
    assert.ok(['failed', 'timeout'].includes(state.initState), `${sdkMode}: ${state.initState}`);
    assert.equal(state.capabilities.loadingApi, null);
    assert.ok(harness.messages.some(message => message.method === 'RobotArenaPlatformState'));
  }
});

test('late SDK resolve cannot replace a timeout terminal state', async () => {
  const harness = createHarness({ sdkMode: 'resolve' });
  await harness.settle(25);
  assert.equal(harness.getState().initState, 'timeout');

  harness.resolveSdk();
  await harness.settle(35);

  assert.equal(harness.getState().initState, 'timeout');
  assert.equal(harness.sdkHandlers.size, 0);
  assert.doesNotMatch(harness.logs.join('\n'), /Init YandexSDK Success/);
});

test('explicit pause callbacks use the narrow platform-origin message', async () => {
  const harness = createHarness({ sdkMode: 'resolve' });
  harness.resolveSdk();
  await harness.settle();

  harness.sdkHandlers.get('game_api_pause')();
  harness.sdkHandlers.get('game_api_resume')();
  await harness.settle();

  const stateMessage = harness.messages.find(
    message => message.method === 'RobotArenaPlatformState');
  assert.ok(stateMessage);
  const lifecycleToken = JSON.parse(stateMessage.argument).lifecycleToken;
  assert.equal(typeof lifecycleToken, 'string');
  assert.ok(lifecycleToken.length > 0);

  assert.deepEqual(
    harness.messages
      .filter(message => message.method === 'RobotArenaYandexLifecyclePause')
      .map(message => JSON.parse(message.argument))
      .map(message => ({
        source: message.source,
        state: message.state,
        token: message.token,
      })),
    [
      { source: 'yandex-lifecycle', state: 'paused', token: lifecycleToken },
      { source: 'yandex-lifecycle', state: 'resumed', token: lifecycleToken },
    ]);
  assert.doesNotMatch(harness.logs.join('\n'), /\[RobotArena\.Platform\] PluginYG2 platform pause=/);
});

test('generic PluginYG2 pause does not emit a platform-origin message', async () => {
  const harness = createHarness({ sdkMode: 'resolve' });
  harness.resolveSdk();
  await harness.settle();

  assert.equal(
    vm.runInContext('typeof globalThis.SendPluginYG2PlatformPause', harness.context),
    'undefined');
  vm.runInContext("YG2Instance('SetPauseGame', 'true');", harness.context);
  await harness.settle();

  assert.equal(
    harness.messages.some(message => message.method === 'RobotArenaYandexLifecyclePause'),
    false);
});

test('platform pause received before Unity is ready is delivered after Unity startup', async () => {
  const harness = createHarness({ sdkMode: 'resolve', unityMode: 'deferred' });
  harness.resolveSdk();
  await harness.settle();

  harness.sdkHandlers.get('game_api_pause')();
  assert.equal(
    harness.messages.some(message => message.method === 'RobotArenaYandexLifecyclePause'),
    false);

  harness.resolveUnity();
  await harness.settle(40);

  const stateIndex = harness.messages.findIndex(
    message => message.method === 'RobotArenaPlatformState');
  const pauseIndex = harness.messages.findIndex(
    message => message.method === 'RobotArenaYandexLifecyclePause');
  assert.ok(stateIndex >= 0);
  assert.ok(pauseIndex > stateIndex);
  assert.deepEqual(
    harness.messages
      .filter(message => message.method === 'RobotArenaYandexLifecyclePause')
      .map(message => JSON.parse(message.argument))
      .map(message => ({ source: message.source, state: message.state })),
    [{ source: 'yandex-lifecycle', state: 'paused' }]);
});

test('Game Ready observation stays honest for void APIs and confirms only a resolved promise', async () => {
  const unconfirmed = createHarness({ sdkMode: 'resolve' });
  unconfirmed.resolveSdk();
  await unconfirmed.settle();
  vm.runInContext(
    'InstallPluginYG2GameReadyObservation(); ysdk.features.LoadingAPI.ready();',
    unconfirmed.context);
  await unconfirmed.settle();
  assert.equal(unconfirmed.getState().gameReadyOutcome, 'unconfirmed');

  const confirmed = createHarness({
    sdkMode: 'resolve',
    gameReadyResult: Promise.resolve(),
  });
  confirmed.resolveSdk();
  await confirmed.settle();
  vm.runInContext(
    'InstallPluginYG2GameReadyObservation(); ysdk.features.LoadingAPI.ready();',
    confirmed.context);
  await confirmed.settle();
  assert.equal(confirmed.getState().gameReadyOutcome, 'confirmed');

  const rejected = Promise.reject(new Error('synthetic Game Ready rejection'));
  rejected.catch(() => {});
  const failed = createHarness({
    sdkMode: 'resolve',
    gameReadyResult: rejected,
  });
  failed.resolveSdk();
  await failed.settle();
  vm.runInContext(
    'InstallPluginYG2GameReadyObservation(); ysdk.features.LoadingAPI.ready();',
    failed.context);
  await failed.settle();
  assert.equal(failed.getState().gameReadyOutcome, 'failed');
  assert.equal(failed.getState().gameReadyFailureReason, 'synthetic Game Ready rejection');
});

module.exports = {
  createHarness,
};

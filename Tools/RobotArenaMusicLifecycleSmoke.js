'use strict';

const fs = require('node:fs');
const http = require('node:http');
const net = require('node:net');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');

const DEFAULT_BUILD_DIRECTORY = path.resolve(
  __dirname,
  '..',
  'Build',
  'WebGL',
  'RobotArenaRelease');
const DEFAULT_WAIT_MS = 60_000;
const DEFAULT_FOCUS_PAUSE_MS = 1_000;
const DEFAULT_RESUME_SETTLE_MS = 500;
const DEFAULT_POST_RESUME_MS = 20_000;
// The shipped intro clips are about seven seconds long. Shorter buffers are
// still useful for the smoke test, while weapon and UI effects are much shorter.
const DEFAULT_MINIMUM_MUSIC_BUFFER_SECONDS = 5;
const CONTEXT_TIME_TOLERANCE_SECONDS = 0.05;
const LOCAL_SDK_PLACEHOLDER = '// Local music smoke only; the real SDK is loaded by Yandex Games.\n';

function isFiniteNumber(value) {
  return value !== null && value !== undefined && Number.isFinite(Number(value));
}

function getEventContextTime(event) {
  const contextTime = Number(event.contextTime);
  if (Number.isFinite(contextTime)) {
    const scheduledTime = Number(event.when);
    if (scheduledTime > contextTime) {
      return scheduledTime;
    }

    return contextTime;
  }

  const monotonicTime = Number(event.monotonicTime);
  if (Number.isFinite(monotonicTime)) {
    return monotonicTime / 1000;
  }

  return Number(event.wallTime) / 1000;
}

function getEventEndContextTime(event) {
  const startTime = getEventContextTime(event);
  const offset = isFiniteNumber(event.offset) ? Number(event.offset) : 0;
  const duration = isFiniteNumber(event.duration)
    ? Number(event.duration)
    : Number(event.bufferDuration) - offset;
  if (!Number.isFinite(startTime) || !Number.isFinite(duration) || duration < 0) {
    return null;
  }

  return startTime + duration;
}

function isMusicStart(event, minimumMusicBufferSeconds) {
  if (!event || event.type !== 'start') {
    return false;
  }

  return event.loop === true ||
    (isFiniteNumber(event.bufferDuration) &&
      Number(event.bufferDuration) >= minimumMusicBufferSeconds);
}

function getImmediatelyStoppedStarts(events) {
  const startsBySourceId = new Map();
  for (const event of events) {
    if (event.type !== 'start' || event.id === undefined || event.id === null) {
      continue;
    }

    const starts = startsBySourceId.get(event.id) || [];
    starts.push(event);
    startsBySourceId.set(event.id, starts);
  }

  const immediatelyStopped = new Set();
  for (const event of events) {
    if (event.type !== 'stop' || event.id === undefined || event.id === null) {
      continue;
    }

    const stopContextTime = getEventContextTime(event);
    for (const start of startsBySourceId.get(event.id) || []) {
      const startContextTime = getEventContextTime(start);
      if (Number.isFinite(startContextTime) &&
          Number.isFinite(stopContextTime) &&
          stopContextTime <= startContextTime + CONTEXT_TIME_TOLERANCE_SECONDS) {
        immediatelyStopped.add(start);
      }
    }
  }

  return immediatelyStopped;
}

function findSameSequenceAudibleOverlaps(trace, musicStarts) {
  const eventOrder = new Map(trace.map((event, index) => [event, index]));
  const intervals = musicStarts.map(start => {
    const startOrder = eventOrder.get(start);
    const stop = trace.find((event, index) =>
      index > startOrder && event.type === 'stop' && event.id === start.id);
    const endContextTime = stop
      ? getEventContextTime(stop)
      : start.loop === true
        ? Number.POSITIVE_INFINITY
        : getEventEndContextTime(start);
    return { start, startContextTime: getEventContextTime(start), endContextTime };
  });
  const overlaps = [];

  for (let firstIndex = 0; firstIndex < intervals.length; firstIndex++) {
    const first = intervals[firstIndex];
    if (!Number.isInteger(first.start.bufferId) ||
        !Number.isFinite(first.startContextTime) ||
        !Number.isFinite(first.endContextTime) && first.endContextTime !== Number.POSITIVE_INFINITY) {
      continue;
    }

    for (let secondIndex = firstIndex + 1; secondIndex < intervals.length; secondIndex++) {
      const second = intervals[secondIndex];
      if (second.start.bufferId !== first.start.bufferId ||
          !Number.isFinite(second.startContextTime) ||
          second.startContextTime >= first.endContextTime - CONTEXT_TIME_TOLERANCE_SECONDS) {
        continue;
      }

      overlaps.push({ first, second });
    }
  }

  return overlaps;
}

function analyzeMusicTrace(events, options = {}) {
  const minimumMusicBufferSeconds = options.minimumMusicBufferSeconds === undefined
    ? DEFAULT_MINIMUM_MUSIC_BUFFER_SECONDS
    : options.minimumMusicBufferSeconds;
  const trace = Array.isArray(events) ? events : [];
  const immediatelyStoppedStarts = getImmediatelyStoppedStarts(trace);
  const musicStarts = trace.filter(event =>
    isMusicStart(event, minimumMusicBufferSeconds) &&
    !immediatelyStoppedStarts.has(event));
  const introStarts = musicStarts.filter(event => event.loop !== true);
  const loopStarts = musicStarts.filter(event => event.loop === true);
  const sameSequenceAudibleOverlaps = findSameSequenceAudibleOverlaps(trace, musicStarts);
  const initialLoopStarts = loopStarts.filter(event =>
    !isFiniteNumber(event.offset) || Number(event.offset) <= CONTEXT_TIME_TOLERANCE_SECONDS);
  const resumedLoopStarts = loopStarts.filter(event =>
    isFiniteNumber(event.offset) && Number(event.offset) > CONTEXT_TIME_TOLERANCE_SECONDS);
  const resumedIntroStarts = introStarts.filter(event =>
    isFiniteNumber(event.offset) && Number(event.offset) > CONTEXT_TIME_TOLERANCE_SECONDS);
  const configuredFocusWindows = Array.isArray(options.focusWindows)
    ? options.focusWindows
    : [];
  const focusWindows = configuredFocusWindows.length > 0
    ? configuredFocusWindows
      .map(window => ({
        lostAt: Number(window.lostAt),
        restoredAt: Number(window.restoredAt),
      }))
      .filter(window => Number.isFinite(window.lostAt) && Number.isFinite(window.restoredAt))
    : [{
      lostAt: Number(options.focusLostAt),
      restoredAt: Number(options.focusRestoredAt),
    }].filter(window => Number.isFinite(window.lostAt) && Number.isFinite(window.restoredAt));
  const configuredPlatformPauseWindows = Array.isArray(options.platformPauseWindows)
    ? options.platformPauseWindows
    : [];
  const platformPauseWindows = configuredPlatformPauseWindows
    .map(window => ({
      ...window,
      pausedAt: Number(window.pausedAt),
      resumedAt: Number(window.resumedAt),
    }))
    .filter(window => Number.isFinite(window.pausedAt) && Number.isFinite(window.resumedAt));
  const loopStartsDuringFocus = loopStarts.filter(event =>
    focusWindows.some(window =>
      Number(event.wallTime) >= window.lostAt && Number(event.wallTime) <= window.restoredAt));
  const loopStartsDuringPlatformPause = loopStarts.filter(event =>
    platformPauseWindows.some(window =>
      Number(event.wallTime) >= window.pausedAt && Number(event.wallTime) <= window.resumedAt));
  const earlyLoopStarts = [];
  const eventOrder = new Map(trace.map((event, index) => [event, index]));

  for (const loopStart of loopStarts) {
    const loopContextTime = getEventContextTime(loopStart);
    const loopOrder = eventOrder.get(loopStart);
    let precedingIntro = null;
    for (const introStart of introStarts) {
      const introOrder = eventOrder.get(introStart);
      if (introOrder < loopOrder &&
          getEventContextTime(introStart) <= loopContextTime + CONTEXT_TIME_TOLERANCE_SECONDS) {
        precedingIntro = introStart;
      }
    }

    if (!precedingIntro) {
      continue;
    }

    const introEndContextTime = getEventEndContextTime(precedingIntro);
    if (introEndContextTime !== null &&
        loopContextTime < introEndContextTime - CONTEXT_TIME_TOLERANCE_SECONDS) {
      earlyLoopStarts.push({
        loopStart,
        precedingIntro,
        introEndContextTime,
        loopContextTime,
      });
    }
  }

  for (const earlyLoop of earlyLoopStarts) {
    sameSequenceAudibleOverlaps.push({
      first: {
        start: earlyLoop.precedingIntro,
        startContextTime: getEventContextTime(earlyLoop.precedingIntro),
        endContextTime: earlyLoop.introEndContextTime,
      },
      second: {
        start: earlyLoop.loopStart,
        startContextTime: earlyLoop.loopContextTime,
      },
    });
  }

  const firstLoopStart = loopStarts.length === 0 ? null : loopStarts[0];
  const firstLoopContextTime = firstLoopStart === null
    ? null
    : getEventContextTime(firstLoopStart);
  const firstLoopOrder = firstLoopStart === null
    ? null
    : eventOrder.get(firstLoopStart);
  const postLoopIntroStarts = firstLoopContextTime === null
    ? []
    : introStarts.filter(event => {
      const introOrder = eventOrder.get(event);
      const introContextTime = getEventContextTime(event);
      return introOrder > firstLoopOrder &&
        (introContextTime > firstLoopContextTime + CONTEXT_TIME_TOLERANCE_SECONDS ||
         Math.abs(introContextTime - firstLoopContextTime) <= CONTEXT_TIME_TOLERANCE_SECONDS) &&
        (!isFiniteNumber(event.offset) || Number(event.offset) <= CONTEXT_TIME_TOLERANCE_SECONDS);
    });

  return {
    trace,
    musicStarts,
    introStarts,
    loopStarts,
    sameSequenceAudibleOverlaps,
    resumedIntroStarts,
    initialLoopStarts,
    resumedLoopStarts,
    focusWindows,
    loopStartsDuringFocus,
    platformPauseWindows,
    loopStartsDuringPlatformPause,
    earlyLoopStarts,
    postLoopIntroStarts,
  };
}

function assertMusicLifecycle(result, options = {}) {
  const problems = [];
  const expectedLoopSequences = options.expectedLoopSequences === undefined
    ? (options.expectedLoopStarts === undefined ? 1 : options.expectedLoopStarts)
    : options.expectedLoopSequences;
  if (result.initialLoopStarts.length !== expectedLoopSequences) {
    problems.push(
      `expected exactly ${expectedLoopSequences} authoritative music loop start(s), got ${result.initialLoopStarts.length}` +
      ` [${result.initialLoopStarts.map(event =>
        `id=${event.id},context=${getEventContextTime(event)},offset=${event.offset},wall=${event.wallTime}`).join('; ')}]`);
    problems.push(`trace=${formatMusicTrace(result.trace)}`);
  }
  const expectedPostLoopIntroStarts = Math.max(0, expectedLoopSequences - 1);
  if (result.postLoopIntroStarts.length !== expectedPostLoopIntroStarts) {
    problems.push(
      `expected exactly ${expectedPostLoopIntroStarts} post-loop intro start(s), got ${result.postLoopIntroStarts.length}` +
      ` [${result.postLoopIntroStarts.map(event =>
        `id=${event.id},context=${getEventContextTime(event)},buffer=${event.bufferDuration}`).join('; ')}]`);
  }
  if (result.loopStarts.length < expectedLoopSequences) {
    problems.push(
      `expected at least ${expectedLoopSequences} raw music loop start(s), got ${result.loopStarts.length}`);
  }
  if (result.loopStarts.length > expectedLoopSequences && result.resumedLoopStarts.length === 0) {
    problems.push('extra raw music loop starts did not carry a positive resume offset');
  }

  if (result.sameSequenceAudibleOverlaps.length > 0) {
    problems.push(
      `same-sequence audible source overlap detected (${result.sameSequenceAudibleOverlaps.length})` +
      ` [${result.sameSequenceAudibleOverlaps.map(item =>
        `buffer=${item.first.start.bufferId},first=${item.first.start.id}@${item.first.startContextTime},` +
        `second=${item.second.start.id}@${item.second.startContextTime},firstEnd=${item.first.endContextTime}`).join('; ')}]`);
  }

  if (result.loopStartsDuringFocus.length > 0) {
    problems.push(
      `music loop starts during focus loss (${result.loopStartsDuringFocus.length})` +
      ` [${result.loopStartsDuringFocus.map(event =>
        `id=${event.id},context=${getEventContextTime(event)},offset=${event.offset},wall=${event.wallTime}`).join('; ')}]` +
      ` windows=${result.focusWindows.map(window => `${window.lostAt}-${window.restoredAt}`).join(',')}`);
  }

  if (result.loopStartsDuringPlatformPause.length > 0) {
    problems.push(
      `music loop starts during PluginYG2 platform pause (${result.loopStartsDuringPlatformPause.length})` +
      ` [${result.loopStartsDuringPlatformPause.map(event =>
        `id=${event.id},context=${getEventContextTime(event)},offset=${event.offset},wall=${event.wallTime}`).join('; ')}]` +
      ` windows=${result.platformPauseWindows.map(window => `${window.pausedAt}-${window.resumedAt}`).join(',')}`);
  }

  if (result.earlyLoopStarts.length > 0) {
    problems.push(
      `music loop starts before the intro finishes (${result.earlyLoopStarts.length})` +
      ` [${result.earlyLoopStarts.map(item =>
        `loop=${getEventContextTime(item.loopStart)},intro=${getEventContextTime(item.precedingIntro)},introEnd=${item.introEndContextTime}`).join('; ')}]`);
    problems.push(`trace=${formatMusicTrace(result.trace, { includeBufferDuration: true })}`);
  }

  for (const introStart of result.resumedIntroStarts) {
    if (!isFiniteNumber(introStart.offset) || Number(introStart.offset) <= 0) {
      problems.push('a resumed intro start has no positive playback offset');
      break;
    }
  }

  if (problems.length > 0) {
    throw new Error(problems.join('; '));
  }

  return true;
}

function formatMusicTrace(trace, options = {}) {
  return trace.map(event => {
    const details = [
      `${event.type}#${event.id}@${getEventContextTime(event)}`,
      event.loop === true ? ':loop' : '',
      event.offset === undefined ? '' : `:offset=${event.offset}`,
    ];
    if (options.includeBufferDuration === true && event.bufferDuration !== undefined) {
      details.push(`:buffer=${event.bufferDuration}`);
    }

    return details.join('');
  }).join(',');
}

function createMusicInstrumentationSource() {
  return `(() => {
  const state = {
    version: 1,
    events: [],
    startedAt: Date.now(),
  };
  window.__robotArenaMusicLifecycle = state;

  if (typeof AudioBufferSourceNode === "undefined") {
    state.error = "AudioBufferSourceNode is unavailable";
    return;
  }

  let nextId = 1;
  let nextBufferId = 1;
  const sourceIds = new WeakMap();
  const bufferIds = new WeakMap();
  const originalStart = AudioBufferSourceNode.prototype.start;
  const originalStop = AudioBufferSourceNode.prototype.stop;

  function getId(source) {
    let id = sourceIds.get(source);
    if (!id) {
      id = nextId++;
      sourceIds.set(source, id);
    }
    return id;
  }

  function getBufferId(buffer) {
    if (!buffer) {
      return null;
    }

    let id = bufferIds.get(buffer);
    if (!id) {
      id = nextBufferId++;
      bufferIds.set(buffer, id);
    }
    return id;
  }

  function record(event) {
    event.wallTime = Date.now();
    event.monotonicTime = performance.now();
    state.events.push(event);
  }

  AudioBufferSourceNode.prototype.start = function(...args) {
    const contextTime = this.context ? this.context.currentTime : null;
    const when = args.length > 0 && args[0] !== undefined ? Number(args[0]) : 0;
    const offset = args.length > 1 && args[1] !== undefined ? Number(args[1]) : 0;
    const duration = args.length > 2 && args[2] !== undefined
      ? Number(args[2])
      : null;
    record({
      type: "start",
      id: getId(this),
      contextTime,
      when,
      offset,
      duration,
      bufferId: getBufferId(this.buffer),
      bufferDuration: this.buffer ? this.buffer.duration : null,
      loop: this.loop === true,
    });
    return originalStart.apply(this, args);
  };

  AudioBufferSourceNode.prototype.stop = function(...args) {
    const contextTime = this.context ? this.context.currentTime : null;
    const when = args.length > 0 && args[0] !== undefined ? Number(args[0]) : 0;
    record({
      type: "stop",
      id: getId(this),
      contextTime,
      when,
      bufferId: getBufferId(this.buffer),
      loop: this.loop === true,
    });
    return originalStop.apply(this, args);
  };
})();`;
}

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function waitFor(predicate, timeoutMs, description, intervalMs = 250) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;
  while (Date.now() < deadline) {
    try {
      if (await predicate()) {
        return true;
      }
    } catch (error) {
      lastError = error;
    }

    await delay(intervalMs);
  }

  const suffix = lastError ? `: ${lastError.message}` : '';
  throw new Error(`Timed out waiting for ${description}${suffix}`);
}

function getFreePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      const port = address.port;
      server.close(error => {
        if (error) {
          reject(error);
          return;
        }

        resolve(port);
      });
    });
  });
}

function getContentType(filePath) {
  const logicalPath = filePath.endsWith('.br')
    ? filePath.slice(0, -3)
    : filePath;
  switch (path.extname(logicalPath).toLowerCase()) {
    case '.html':
      return 'text/html; charset=utf-8';
    case '.js':
      return 'application/javascript; charset=utf-8';
    case '.wasm':
      return 'application/wasm';
    case '.json':
      return 'application/json; charset=utf-8';
    case '.data':
      return 'application/octet-stream';
    default:
      return 'application/octet-stream';
  }
}

function createStaticServer(buildDirectory) {
  const root = path.resolve(buildDirectory);
  const server = http.createServer(async (request, response) => {
    if (request.method !== 'GET' && request.method !== 'HEAD') {
      response.writeHead(405, { Allow: 'GET, HEAD' });
      response.end();
      return;
    }

    try {
      const requestUrl = new URL(request.url || '/', 'http://127.0.0.1');
      const requestedPath = requestUrl.pathname === '/'
        ? 'index.html'
        : decodeURIComponent(requestUrl.pathname).replace(/^\/+/, '');
      let filePath = path.resolve(root, requestedPath);
      const relativePath = path.relative(root, filePath);
      if (relativePath.startsWith('..') || path.isAbsolute(relativePath)) {
        response.writeHead(403);
        response.end();
        return;
      }

      if (!fs.existsSync(filePath) && fs.existsSync(filePath + '.br')) {
        filePath += '.br';
      }

      if (requestedPath === 'sdk.js' && !fs.existsSync(filePath)) {
        const body = Buffer.from(LOCAL_SDK_PLACEHOLDER, 'utf8');
        response.writeHead(200, {
          'Cache-Control': 'no-store',
          'Content-Length': body.length,
          'Content-Type': 'application/javascript; charset=utf-8',
        });
        if (request.method === 'HEAD') {
          response.end();
          return;
        }

        response.end(body);
        return;
      }

      if (requestedPath === 'favicon.ico' && !fs.existsSync(filePath)) {
        response.writeHead(204, { 'Cache-Control': 'no-store' });
        response.end();
        return;
      }

      const stat = await fs.promises.stat(filePath);
      if (!stat.isFile()) {
        response.writeHead(404);
        response.end();
        return;
      }

      const headers = {
        'Cache-Control': 'no-store',
        'Content-Length': stat.size,
        'Content-Type': getContentType(filePath),
      };
      if (filePath.endsWith('.br')) {
        headers['Content-Encoding'] = 'br';
      }

      response.writeHead(200, headers);
      if (request.method === 'HEAD') {
        response.end();
        return;
      }

      fs.createReadStream(filePath).pipe(response);
    } catch (error) {
      if (error.code === 'ENOENT' || error instanceof URIError) {
        response.writeHead(404);
        response.end();
        return;
      }

      response.writeHead(500);
      response.end('Static server error');
    }
  });

  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      resolve({ server, port: address.port });
    });
  });
}

function closeServer(server) {
  return new Promise(resolve => {
    if (!server) {
      resolve();
      return;
    }

    server.close(() => resolve());
  });
}

function stopProcess(processHandle, timeoutMs = 5_000) {
  if (!processHandle || processHandle.exitCode !== null) {
    return Promise.resolve();
  }

  return new Promise(resolve => {
    let settled = false;
    const finish = () => {
      if (settled) {
        return;
      }

      settled = true;
      clearTimeout(timer);
      resolve();
    };
    const timer = setTimeout(finish, timeoutMs);
    processHandle.once('close', finish);

    try {
      processHandle.kill();
    } catch (error) {
      finish();
    }
  });
}

function removeTemporaryDirectory(directoryPath) {
  try {
    fs.rmSync(directoryPath, {
      recursive: true,
      force: true,
      maxRetries: 20,
      retryDelay: 250,
    });
  } catch (error) {
    console.warn(`Unable to remove temporary browser profile ${directoryPath}: ${error.message}`);
  }
}

class CdpConnection {
  constructor(socket) {
    this.socket = socket;
    this.nextId = 1;
    this.pending = new Map();
    this.consoleMessages = [];
    this.networkRequests = new Map();
    this.resourceErrors = [];
    this.logEntries = [];

    socket.addEventListener('message', event => {
      const message = JSON.parse(String(event.data));
      const sessionId = message.sessionId;
      const requestKey = `${sessionId || ''}:${message.params?.requestId || ''}`;
      if (message.method === 'Network.requestWillBeSent') {
        this.networkRequests.set(requestKey, message.params.request.url);
        return;
      }

      if (message.method === 'Network.responseReceived') {
        const response = message.params.response;
        if (response && Number(response.status) >= 400) {
          this.resourceErrors.push({
            sessionId,
            source: 'network',
            status: response.status,
            text: `${response.status} ${response.statusText || ''}`.trim(),
            url: response.url,
          });
        }
        return;
      }

      if (message.method === 'Network.loadingFailed') {
        if (!message.params.canceled) {
          this.resourceErrors.push({
            sessionId,
            source: 'network',
            text: message.params.errorText || 'network loading failed',
            url: this.networkRequests.get(requestKey) || '',
          });
        }
        return;
      }

      if (message.method === 'Log.entryAdded') {
        const entry = message.params.entry || {};
        this.logEntries.push({
          sessionId,
          source: entry.source,
          level: entry.level,
          text: entry.text || '',
          url: entry.url || '',
        });
        return;
      }

      if (message.method === 'Runtime.consoleAPICalled') {
        const text = (message.params?.args || [])
          .map(argument => argument.value ?? argument.unserializableValue ?? argument.description ?? '')
          .join(' ');
        this.consoleMessages.push({
          sessionId,
          type: message.params?.type,
          text,
          wallTime: Date.now(),
        });
        return;
      }

      if (message.id !== undefined) {
        const pending = this.pending.get(message.id);
        if (!pending) {
          return;
        }

        this.pending.delete(message.id);
        if (message.error) {
          pending.reject(new Error(`${message.error.code}: ${message.error.message}`));
        } else {
          pending.resolve(message.result || {});
        }
        return;
      }
    });

    socket.addEventListener('close', () => {
      for (const pending of this.pending.values()) {
        pending.reject(new Error('CDP WebSocket closed'));
      }
      this.pending.clear();
    });
  }

  send(method, params = {}, sessionId = undefined) {
    const id = this.nextId++;
    const message = { id, method, params };
    if (sessionId) {
      message.sessionId = sessionId;
    }

    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.socket.send(JSON.stringify(message));
    });
  }

  close() {
    this.socket.close();
  }

  getConsoleMessages(sessionId) {
    return this.consoleMessages.filter(message => message.sessionId === sessionId);
  }

  getResourceErrors(sessionId) {
    return this.resourceErrors.filter(error => error.sessionId === sessionId);
  }

  getLogErrors(sessionId) {
    return this.logEntries.filter(entry =>
      entry.sessionId === sessionId && entry.level === 'error');
  }
}

function connectCdp(webSocketUrl) {
  if (typeof WebSocket !== 'function') {
    throw new Error('Node WebSocket support is required (Node 22 or newer).');
  }

  return new Promise((resolve, reject) => {
    const socket = new WebSocket(webSocketUrl);
    socket.addEventListener('open', () => resolve(new CdpConnection(socket)));
    socket.addEventListener('error', () => reject(new Error('Unable to connect to Chrome DevTools Protocol')));
  });
}

async function getJson(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`${url} returned HTTP ${response.status}`);
  }

  return response.json();
}

function resolveBrowserPath(browserPath) {
  const candidates = [
    browserPath,
    process.env.ROBOT_ARENA_BROWSER,
    process.env.ProgramFiles
      ? path.join(process.env.ProgramFiles, 'Microsoft', 'Edge', 'Application', 'msedge.exe')
      : null,
    process.env['ProgramFiles(x86)']
      ? path.join(process.env['ProgramFiles(x86)'], 'Microsoft', 'Edge', 'Application', 'msedge.exe')
      : null,
    process.env.LOCALAPPDATA
      ? path.join(process.env.LOCALAPPDATA, 'Microsoft', 'Edge', 'Application', 'msedge.exe')
      : null,
  ].filter(Boolean);

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(
    'Microsoft Edge was not found. Pass --browser <path> or set ROBOT_ARENA_BROWSER.');
}

function launchBrowser(browserPath, debugPort, userDataDirectory) {
  return spawn(browserPath, [
    '--headless=new',
    '--disable-gpu',
    '--enable-unsafe-swiftshader',
    '--use-angle=swiftshader',
    '--disable-dev-shm-usage',
    '--no-first-run',
    '--no-default-browser-check',
    `--remote-debugging-port=${debugPort}`,
    `--user-data-dir=${userDataDirectory}`,
    'about:blank',
  ], {
    stdio: 'ignore',
    windowsHide: true,
  });
}

async function waitForDebugger(debugPort, browserProcess, timeoutMs) {
  let lastError = null;
  try {
    await waitFor(async () => {
      if (browserProcess.exitCode !== null) {
        throw new Error(`browser exited with code ${browserProcess.exitCode}`);
      }

      try {
        const version = await getJson(`http://127.0.0.1:${debugPort}/json/version`);
        if (!version.webSocketDebuggerUrl) {
          throw new Error('browser debugger URL is missing');
        }

        lastError = version;
        return true;
      } catch (error) {
        lastError = error;
        return false;
      }
    }, timeoutMs, 'browser DevTools endpoint');
  } catch (error) {
    if (lastError instanceof Error) {
      throw new Error(`${error.message}: ${lastError.message}`);
    }
    throw error;
  }

  return getJson(`http://127.0.0.1:${debugPort}/json/version`);
}

async function createPage(cdp) {
  const created = await cdp.send('Target.createTarget', { url: 'about:blank' });
  const attached = await cdp.send('Target.attachToTarget', {
    targetId: created.targetId,
    flatten: true,
  });
  return {
    targetId: created.targetId,
    sessionId: attached.sessionId,
  };
}

async function evaluate(cdp, sessionId, expression) {
  const response = await cdp.send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
  }, sessionId);
  if (response.exceptionDetails) {
    throw new Error(response.exceptionDetails.text || 'browser evaluation failed');
  }

  return response.result ? response.result.value : undefined;
}

async function readTrace(cdp, sessionId) {
  const serialized = await evaluate(
    cdp,
    sessionId,
    'JSON.stringify(window.__robotArenaMusicLifecycle || { events: [] })');
  return JSON.parse(serialized);
}

async function readPluginYG2InitState(cdp, sessionId) {
  const serialized = await evaluate(
    cdp,
    sessionId,
    'JSON.stringify(window.__robotArenaPluginYG2 || null)');
  return serialized === 'null' ? null : JSON.parse(serialized);
}

function assertLocalPluginYG2Fallback(state) {
  if (!state || !['local', 'failed', 'timeout'].includes(state.initState)) {
    throw new Error(
      'local smoke did not reach an explicit non-SDK PluginYG2 state: ' +
      JSON.stringify(state));
  }
}

async function summarizeMusicTrace(cdp, sessionId) {
  try {
    const trace = await readTrace(cdp, sessionId);
    const eventTypes = trace.events.map(event =>
      `${event.type}#${event.id}:${event.loop === true ? 'loop' : 'intro'}` +
      `(buffer=${event.bufferDuration},offset=${event.offset},when=${event.when},context=${event.contextTime})`);
    let summary = `trace events=${eventTypes.length} [${eventTypes.join(', ')}]`;
    if (trace.error) {
      summary += ` error=${trace.error}`;
    }

    const musicConsoleMessages = cdp.getConsoleMessages(sessionId)
      .filter(message => message.text.includes('[RobotArena.Music]'))
      .slice(-12)
      .map(message => message.text);
    if (musicConsoleMessages.length > 0) {
      summary += ` console=[${musicConsoleMessages.join(' | ')}]`;
    }

    return summary;
  } catch (error) {
    return `trace read failed: ${error.message}`;
  }
}

function getConsoleErrors(cdp, sessionId) {
  return cdp.getConsoleMessages(sessionId)
    .filter(message => message.type === 'error')
    .map(message => message.text);
}

function getBrowserResourceErrors(cdp, sessionId) {
  const networkErrors = cdp.getResourceErrors(sessionId)
    .map(error => `${error.text}${error.url ? ` (${error.url})` : ''}`);
  const logErrors = cdp.getLogErrors(sessionId)
    .map(entry => `${entry.text}${entry.url ? ` (${entry.url})` : ''}`);
  return [...networkErrors, ...logErrors];
}

function hasPluginYG2TransportDiagnostic(cdp, sessionId, isPaused, sinceWallTime) {
  const expectedState = `platform pause=${isPaused ? 'true' : 'false'}`;
  return cdp.getConsoleMessages(sessionId).some(message =>
    message.type === 'log' &&
    message.wallTime >= sinceWallTime &&
    message.text.includes('[RobotArena.PluginYG2.Transport]') &&
    message.text.includes(expectedState));
}

async function waitForMusicIntro(cdp, sessionId, timeoutMs) {
  try {
    await waitFor(
      async () => {
        const trace = await readTrace(cdp, sessionId);
        return analyzeMusicTrace(trace.events).introStarts.length > 0;
      },
      timeoutMs,
      'first calm intro start');
  } catch (error) {
    const traceSummary = await summarizeMusicTrace(cdp, sessionId);
    throw new Error(`${error.message}; ${traceSummary}`);
  }
}

async function dispatchUserGesture(cdp, sessionId) {
  await evaluate(cdp, sessionId, `(() => {
    const canvas = document.querySelector("canvas");
    if (canvas) {
      canvas.focus();
    }
    return true;
  })()`);
  await dispatchPointerClick(cdp, sessionId, 640, 360);
}

async function dispatchPointerClick(cdp, sessionId, x, y) {
  await cdp.send('Input.dispatchMouseEvent', {
    type: 'mouseMoved',
    x,
    y,
    button: 'none',
  }, sessionId);
  await cdp.send('Input.dispatchMouseEvent', {
    type: 'mousePressed',
    x,
    y,
    button: 'left',
    buttons: 1,
    clickCount: 1,
  }, sessionId);
  await cdp.send('Input.dispatchMouseEvent', {
    type: 'mouseReleased',
    x,
    y,
    button: 'left',
    buttons: 0,
    clickCount: 1,
  }, sessionId);
}

async function waitForMusicLoopCount(cdp, sessionId, count, timeoutMs) {
  try {
    await waitFor(
      async () => {
        const trace = await readTrace(cdp, sessionId);
        return analyzeMusicTrace(trace.events).loopStarts.length >= count;
      },
      timeoutMs,
      `${count} music loop start(s)`);
  } catch (error) {
    const traceSummary = await summarizeMusicTrace(cdp, sessionId);
    throw new Error(`${error.message}; ${traceSummary}`);
  }
}

async function waitForPostLoopIntro(cdp, sessionId, timeoutMs) {
  try {
    await waitFor(
      async () => {
        const trace = await readTrace(cdp, sessionId);
        return analyzeMusicTrace(trace.events).postLoopIntroStarts.length > 0;
      },
      timeoutMs,
      'combat intro start');
  } catch (error) {
    const traceSummary = await summarizeMusicTrace(cdp, sessionId);
    throw new Error(`${error.message}; ${traceSummary}`);
  }
}

async function activateTargetForFocusTransition(cdp, targetId) {
  // Headless Edge can ignore the first activation when the target is already
  // active. Repeating it makes focus callbacks deterministic for the smoke.
  await cdp.send('Target.activateTarget', { targetId });
  await cdp.send('Target.activateTarget', { targetId });
}

async function runFocusCycle(
  cdp,
  page,
  backgroundTargetId,
  focusPauseMs,
  resumeSettleMs) {
  const lostAt = Date.now();
  await activateTargetForFocusTransition(cdp, backgroundTargetId);
  await delay(focusPauseMs);

  const restoredAt = Date.now();
  await activateTargetForFocusTransition(cdp, page.targetId);
  await delay(resumeSettleMs);
  return { lostAt, restoredAt };
}

async function runSyntheticPluginYG2TransportCycle(
  cdp,
  page,
  pauseDurationMs,
  resumeSettleMs) {
  // This intentionally bypasses ysdk.on(...). It covers only the generated
  // PluginYG2 template transport path and never claims hosted SDK evidence or
  // a session pause while running in the local guest harness.
  const pausedAt = Date.now();
  const pauseDispatched = await evaluate(cdp, page.sessionId, `(() => {
    if (typeof PauseCallback !== 'function' || typeof YG2Instance !== 'function') {
      return false;
    }

    PauseCallback();
    return true;
  })()`);
  if (!pauseDispatched) {
    throw new Error('PluginYG2 template pause callback is unavailable for synthetic testing');
  }

  const pauseTransportObserved = await waitFor(
    async () => hasPluginYG2TransportDiagnostic(cdp, page.sessionId, true, pausedAt),
    Math.max(5_000, resumeSettleMs + 1_000),
    'PluginYG2 synthetic pause transport');
  await delay(pauseDurationMs);

  const resumedAt = Date.now();
  const resumeDispatched = await evaluate(cdp, page.sessionId, `(() => {
    if (typeof ResumeCallback !== 'function' || typeof YG2Instance !== 'function') {
      return false;
    }

    ResumeCallback();
    return true;
  })()`);
  if (!resumeDispatched) {
    throw new Error('PluginYG2 template resume callback is unavailable for synthetic testing');
  }

  const resumeTransportObserved = await waitFor(
    async () => hasPluginYG2TransportDiagnostic(cdp, page.sessionId, false, resumedAt),
    Math.max(5_000, resumeSettleMs + 1_000),
    'PluginYG2 synthetic resume transport');
  await delay(resumeSettleMs);
  return {
    pausedAt,
    resumedAt,
    pauseDiagnosticObserved: false,
    resumeDiagnosticObserved: false,
    pauseTransportObserved,
    resumeTransportObserved,
    platformPauseWindow: null,
  };
}

function assertSemanticModeTransition(result) {
  const firstIntro = result.introStarts[0];
  const firstLoop = result.initialLoopStarts[0];
  const postLoopIntro = result.postLoopIntroStarts[0];
  const secondLoop = result.initialLoopStarts[1];
  if (!firstIntro || !firstLoop || !postLoopIntro || !secondLoop) {
    throw new Error('semantic mode evidence is missing from the music trace');
  }

  if (!Number.isInteger(firstIntro.bufferId) ||
      !Number.isInteger(firstLoop.bufferId) ||
      !Number.isInteger(postLoopIntro.bufferId)) {
    throw new Error('music trace is missing WebAudio buffer identity evidence');
  }

  if (firstIntro.bufferId === firstLoop.bufferId) {
    throw new Error('the initial music loop reused the intro buffer');
  }

  if (firstIntro.bufferId === postLoopIntro.bufferId) {
    throw new Error('the post-loop music intro reused the initial semantic cue');
  }

  const postLoopIntroOrder = result.trace.indexOf(postLoopIntro);
  const secondLoopOrder = result.trace.indexOf(secondLoop);
  if (postLoopIntroOrder < 0 || secondLoopOrder < 0 || postLoopIntroOrder >= secondLoopOrder) {
    throw new Error('the combat intro did not start before the second semantic loop');
  }

  if (postLoopIntro.bufferId === secondLoop.bufferId) {
    throw new Error('the combat loop reused the combat intro buffer');
  }
}

async function runMusicLifecycleSmoke(options = {}) {
  const buildDirectory = path.resolve(options.buildDirectory || DEFAULT_BUILD_DIRECTORY);
  if (!fs.existsSync(path.join(buildDirectory, 'index.html'))) {
    throw new Error(`WebGL build index.html was not found under ${buildDirectory}`);
  }

  const waitMs = options.waitMs === undefined ? DEFAULT_WAIT_MS : options.waitMs;
  const focusPauseMs = options.focusPauseMs === undefined
    ? DEFAULT_FOCUS_PAUSE_MS
    : options.focusPauseMs;
  const resumeSettleMs = options.resumeSettleMs === undefined
    ? DEFAULT_RESUME_SETTLE_MS
    : options.resumeSettleMs;
  const postResumeMs = options.postResumeMs === undefined
    ? DEFAULT_POST_RESUME_MS
    : options.postResumeMs;
  const focusCycles = options.focusCycles === undefined ? 2 : options.focusCycles;
  const enterSession = options.enterSession === true;
  const syntheticPlatformPauseCycles = options.syntheticPlatformPauseCycles === undefined
    ? 0
    : options.syntheticPlatformPauseCycles;
  if (syntheticPlatformPauseCycles > 0 && !enterSession) {
    throw new Error('--synthetic-platform-pause-cycles requires --enter-session');
  }
  const coverLeadWindow = options.coverLeadWindow === true;
  const coverCombatIntro = options.coverCombatIntro === true;
  if (coverCombatIntro && !enterSession) {
    throw new Error('--cover-combat-intro requires --enter-session');
  }
  const sessionLoadMs = options.sessionLoadMs === undefined ? 1_500 : options.sessionLoadMs;
  const browserPath = resolveBrowserPath(options.browserPath);
  const staticServer = await createStaticServer(buildDirectory);
  const debugPort = await getFreePort();
  const userDataDirectory = fs.mkdtempSync(
    path.join(os.tmpdir(), 'robot-arena-music-smoke-'));
  const browserProcess = launchBrowser(browserPath, debugPort, userDataDirectory);
  let cdp = null;
  let page = null;
  let backgroundTargetId = null;
  let focusLostAt = null;
  let focusRestoredAt = null;

  try {
    const version = await waitForDebugger(debugPort, browserProcess, waitMs);
    cdp = await connectCdp(version.webSocketDebuggerUrl);
    page = await createPage(cdp);
    await cdp.send('Page.enable', {}, page.sessionId);
    await cdp.send('Runtime.enable', {}, page.sessionId);
    await cdp.send('Network.enable', {}, page.sessionId);
    await cdp.send('Log.enable', {}, page.sessionId);
    await cdp.send('Emulation.setDeviceMetricsOverride', {
      width: 1280,
      height: 720,
      deviceScaleFactor: 1,
      mobile: false,
    }, page.sessionId);
    await cdp.send('Page.addScriptToEvaluateOnNewDocument', {
      source: createMusicInstrumentationSource(),
    }, page.sessionId);
    await cdp.send('Page.navigate', {
      url: `http://127.0.0.1:${staticServer.port}/index.html`,
    }, page.sessionId);

    await waitFor(
      async () => evaluate(cdp, page.sessionId, 'Boolean(window.__robotArenaMusicLifecycle)'),
      waitMs,
      'music instrumentation');
    try {
      await waitFor(
        async () => evaluate(cdp, page.sessionId, 'Boolean(window.unityInstance)'),
        waitMs,
        'Unity WebGL initialization');
    } catch (error) {
      const consoleMessages = cdp.getConsoleMessages(page.sessionId)
        .slice(-20)
        .map(message => `${message.type || 'log'}: ${message.text}`)
        .join(' | ');
      const resourceErrors = getBrowserResourceErrors(cdp, page.sessionId).join(' | ');
      throw new Error(`${error.message}; browser console=[${consoleMessages}]; ` +
        `browser resources=[${resourceErrors}]`);
    }

    const pluginYG2Init = await readPluginYG2InitState(cdp, page.sessionId);
    assertLocalPluginYG2Fallback(pluginYG2Init);

    if (enterSession) {
      await delay(sessionLoadMs);
      await evaluate(cdp, page.sessionId, `(() => {
        if (!window.unityInstance) {
          return false;
        }

        window.unityInstance.SendMessage("Canvas", "StartGame");
        return true;
      })()`);
      await delay(sessionLoadMs);
    }

    const background = await cdp.send('Target.createTarget', { url: 'about:blank' });
    backgroundTargetId = background.targetId;
    const focusWindows = [];
    await dispatchUserGesture(cdp, page.sessionId);
    if (coverLeadWindow) {
      focusWindows.push(await runFocusCycle(
        cdp,
        page,
        backgroundTargetId,
        focusPauseMs,
        resumeSettleMs));
    }

    await waitForMusicIntro(cdp, page.sessionId, waitMs);
    await delay(options.beforeBlurMs === undefined ? 1_000 : options.beforeBlurMs);
    const calmIntroFocusWindow = await runFocusCycle(
      cdp,
      page,
      backgroundTargetId,
      focusPauseMs,
      resumeSettleMs);
    focusWindows.push(calmIntroFocusWindow);
    focusLostAt = calmIntroFocusWindow.lostAt;
    focusRestoredAt = calmIntroFocusWindow.restoredAt;

    if (enterSession && coverCombatIntro) {
      await waitForMusicLoopCount(cdp, page.sessionId, 1, postResumeMs);
      await waitForPostLoopIntro(cdp, page.sessionId, postResumeMs);
      focusWindows.push(await runFocusCycle(
        cdp,
        page,
        backgroundTargetId,
        focusPauseMs,
        resumeSettleMs));
      await waitForMusicLoopCount(cdp, page.sessionId, 2, postResumeMs);
    } else {
      await waitForMusicLoopCount(cdp, page.sessionId, enterSession ? 2 : 1, postResumeMs);
    }

    const platformPauseWindows = [];
    for (let cycle = 0; cycle < syntheticPlatformPauseCycles; cycle++) {
      const syntheticCycle = await runSyntheticPluginYG2TransportCycle(
        cdp,
        page,
        focusPauseMs,
        resumeSettleMs);
      if (syntheticCycle.platformPauseWindow) {
        platformPauseWindows.push(syntheticCycle.platformPauseWindow);
      }
    }

    for (let cycle = 0; cycle < focusCycles; cycle++) {
      focusWindows.push(await runFocusCycle(
        cdp,
        page,
        backgroundTargetId,
        focusPauseMs,
        resumeSettleMs));
    }

    const trace = await readTrace(cdp, page.sessionId);
    const consoleErrors = getConsoleErrors(cdp, page.sessionId);
    if (consoleErrors.length > 0) {
      throw new Error(`browser console errors: ${consoleErrors.join(' | ')}`);
    }
    const browserResourceErrors = getBrowserResourceErrors(cdp, page.sessionId);
    if (browserResourceErrors.length > 0) {
      throw new Error(`browser resource errors: ${browserResourceErrors.join(' | ')}`);
    }
    const analysis = analyzeMusicTrace(trace.events, {
      focusLostAt,
      focusRestoredAt,
      focusWindows,
      platformPauseWindows,
    });
    assertMusicLifecycle(analysis, {
      expectedLoopStarts: enterSession ? 2 : 1,
    });
    if (enterSession) {
      assertSemanticModeTransition(analysis);
    }
    return {
      buildDirectory,
      browserPath,
      focusLostAt,
      focusRestoredAt,
      focusWindows,
      platformPauseWindows,
      platformPauseEvidence: syntheticPlatformPauseCycles > 0
        ? 'synthetic-template-transport-only'
        : 'none',
      pluginYG2Init,
      enterSession,
      consoleErrors,
      browserResourceErrors,
      ...analysis,
    };
  } finally {
    if (backgroundTargetId && cdp) {
      try {
        await cdp.send('Target.closeTarget', { targetId: backgroundTargetId });
      } catch (error) {
        // The browser may already be shutting down.
      }
    }
    if (page && cdp) {
      try {
        await cdp.send('Target.closeTarget', { targetId: page.targetId });
      } catch (error) {
        // The browser may already be shutting down.
      }
    }
    if (cdp) {
      cdp.close();
    }
    await closeServer(staticServer.server);
    await stopProcess(browserProcess);
    removeTemporaryDirectory(userDataDirectory);
  }
}

function parseArguments(argumentsList) {
  const options = {};
  for (let index = 0; index < argumentsList.length; index++) {
    const argument = argumentsList[index];
    if (argument === '--help' || argument === '-h') {
      options.help = true;
      continue;
    }
    if (argument === '--enter-session') {
      options.enterSession = true;
      continue;
    }
    if (argument === '--cover-lead-window') {
      options.coverLeadWindow = true;
      continue;
    }
    if (argument === '--cover-combat-intro') {
      options.coverCombatIntro = true;
      continue;
    }

    const valueArguments = new Map([
      ['--build', 'buildDirectory'],
      ['--browser', 'browserPath'],
      ['--wait-ms', 'waitMs'],
      ['--before-blur-ms', 'beforeBlurMs'],
      ['--focus-pause-ms', 'focusPauseMs'],
      ['--resume-settle-ms', 'resumeSettleMs'],
      ['--post-resume-ms', 'postResumeMs'],
      ['--focus-cycles', 'focusCycles'],
      ['--synthetic-platform-pause-cycles', 'syntheticPlatformPauseCycles'],
      ['--session-load-ms', 'sessionLoadMs'],
      ['--output', 'outputPath'],
    ]);
    const optionName = valueArguments.get(argument);
    if (!optionName) {
      throw new Error(`Unknown argument: ${argument}`);
    }
    index++;
    if (index >= argumentsList.length) {
      throw new Error(`Missing value for ${argument}`);
    }

    const value = argumentsList[index];
    options[optionName] = optionName.endsWith('Ms') ||
        optionName === 'waitMs' ||
        optionName === 'focusCycles' ||
        optionName === 'syntheticPlatformPauseCycles'
      ? Number(value)
      : value;
  }

  return options;
}

function printHelp() {
  console.log([
    'Usage: node Tools/RobotArenaMusicLifecycleSmoke.js [options]',
    '',
    '--build <directory>       WebGL release directory (default: Build/WebGL/RobotArenaRelease)',
    '--browser <path>          Edge executable path',
    '--wait-ms <milliseconds> Startup timeout',
    '--before-blur-ms <ms>    Time to play intro before focus loss',
    '--focus-pause-ms <ms>    Time spent on the background target',
    '--resume-settle-ms <ms> Delay after focus restoration before next cycle',
    '--post-resume-ms <ms>    Time to observe after focus restoration',
    '--focus-cycles <count>   Additional focus cycles after the first loop',
    '--synthetic-platform-pause-cycles <count>  Direct callback cycles; local harness only, not Yandex SDK evidence',
    '--session-load-ms <ms>   Delay used when entering SampleScene',
    '--enter-session          Enter SampleScene before granting audio permission',
    '--cover-lead-window      Focus loss immediately after the audio gesture',
    '--cover-combat-intro     Focus loss during combat intro (requires --enter-session)',
    '--output <file>          Write JSON trace result to a file',
  ].join('\n'));
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  if (options.help) {
    printHelp();
    return;
  }

  const result = await runMusicLifecycleSmoke(options);
  const serialized = JSON.stringify(result, null, 2);
  if (options.outputPath) {
    fs.writeFileSync(options.outputPath, serialized + '\n', 'utf8');
  }
  console.log(serialized);
}

if (require.main === module) {
  main().catch(error => {
    console.error(`Robot Arena music lifecycle smoke failed: ${error.stack || error.message}`);
    process.exitCode = 1;
  });
}

module.exports = {
  analyzeMusicTrace,
  assertMusicLifecycle,
  assertSemanticModeTransition,
  createStaticServer,
  assertLocalPluginYG2Fallback,
  readPluginYG2InitState,
  runMusicLifecycleSmoke,
};

mergeInto(LibraryManager.library, {
    RobotArenaPlatformProbe_Begin: function (hostNamePtr, timeoutMilliseconds) {
        var hostName = UTF8ToString(hostNamePtr);
        var state = window.__robotArenaPlatformProbe || {};
        state.hostName = hostName;
        state.sdk = null;
        state.gameReady = false;
        state.gameReadyRequested = false;
        state.finished = false;
        state.snapshot = null;
        state.pendingMessages = [];
        state.getReceiver = function () {
            if (typeof SendMessage === 'function') {
                return function (receiverHostName, methodName, value) {
                    SendMessage(receiverHostName, methodName, value);
                };
            }

            var unityInstance = window.unityInstance || window.gameInstance;
            if (unityInstance && typeof unityInstance.SendMessage === 'function') {
                return function (receiverHostName, methodName, value) {
                    unityInstance.SendMessage(receiverHostName, methodName, value);
                };
            }

            return null;
        };
        state.flushPendingMessages = function (receiver) {
            receiver = receiver || state.getReceiver();
            if (!receiver) {
                return false;
            }

            var pendingMessages = state.pendingMessages || [];
            state.pendingMessages = [];
            for (var index = 0; index < pendingMessages.length; index += 1) {
                var pendingMessage = pendingMessages[index];
                receiver(state.hostName, pendingMessage.methodName, pendingMessage.value);
            }

            return true;
        };
        state.sendMessage = function (methodName, value) {
            var receiver = state.getReceiver();
            if (!receiver) {
                state.pendingMessages = state.pendingMessages || [];
                state.pendingMessages.push({ methodName: methodName, value: value });
                return false;
            }

            state.flushPendingMessages(receiver);
            receiver(state.hostName, methodName, value);
            return true;
        };
        window.__robotArenaPlatformProbe = state;

        var sendSnapshot = function (snapshot) {
            state.snapshot = snapshot;
            if (typeof console !== 'undefined' && typeof console.info === 'function') {
                console.info('RobotArena platform probe', snapshot);
            }
            state.sendMessage(
                'OnPlatformProbeResult',
                JSON.stringify(snapshot));
        };
        state.sendSnapshot = sendSnapshot;
        var fail = function (message, timedOut) {
            sendSnapshot({
                sdkDetected: false,
                sdkInitialized: false,
                environment: '',
                language: '',
                hasLoadingApi: false,
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReady: false,
                timedOut: !!timedOut,
                error: message
            });
        };
        var timeoutId = setTimeout(function () {
            if (!state.finished) {
                state.finished = true;
                fail('Yandex SDK initialization timed out.', true);
            }
        }, timeoutMilliseconds);
        var initialize = function () {
            if (window.ysdk) {
                return Promise.resolve(window.ysdk);
            }

            if (window.YaGames && typeof window.YaGames.init === 'function') {
                return window.YaGames.init();
            }

            return Promise.reject(new Error('YaGames.init and window.ysdk are unavailable.'));
        };

        try {
            initialize().then(function (sdk) {
                if (state.finished) {
                    return;
                }

                clearTimeout(timeoutId);
                state.finished = true;
                state.sdk = sdk;

                var features = sdk && sdk.features ? sdk.features : {};
                var loadingApi = features.LoadingAPI || null;
                var environment = sdk && sdk.environment ? sdk.environment : {};
                var i18n = environment.i18n || {};
                var snapshot = {
                    sdkDetected: !!sdk,
                    sdkInitialized: !!sdk,
                    environment: environment.app ? (environment.app.id || '') : '',
                    language: i18n.lang || '',
                    hasLoadingApi: !!(loadingApi && typeof loadingApi.ready === 'function'),
                    supportsPause: !!(sdk && typeof sdk.on === 'function'),
                    supportsPlayerData: !!(sdk && typeof sdk.getPlayer === 'function'),
                    supportsLeaderboard: !!(sdk && typeof sdk.getLeaderboards === 'function'),
                    supportsFullscreenAds: !!(sdk && sdk.adv && typeof sdk.adv.showFullscreenAdv === 'function'),
                    gameReady: false,
                    timedOut: false,
                    error: ''
                };

                if (sdk && typeof sdk.on === 'function') {
                    try {
                        sdk.on('game_api_pause', function () {
                            state.sendMessage('OnPlatformProbePause', 'true');
                        });
                        sdk.on('game_api_resume', function () {
                            state.sendMessage('OnPlatformProbePause', 'false');
                        });
                    } catch (pauseError) {
                        snapshot.supportsPause = false;
                        snapshot.error = 'Unable to subscribe to pause events: ' + pauseError.message;
                    }
                }

                sendSnapshot(snapshot);

                if (sdk && typeof sdk.getPlayer === 'function') {
                    try {
                        var playerRequest = sdk.getPlayer({ scopes: false });
                        if (playerRequest && typeof playerRequest.then === 'function') {
                            playerRequest.then(function () {
                                if (typeof console !== 'undefined' && typeof console.info === 'function') {
                                    console.info('RobotArena platform probe player boundary is callable.');
                                }
                            }, function (playerError) {
                                if (typeof console !== 'undefined' && typeof console.warn === 'function') {
                                    console.warn('RobotArena platform probe player boundary failed.', playerError);
                                }
                            });
                        }
                    } catch (playerError) {
                        if (typeof console !== 'undefined' && typeof console.warn === 'function') {
                            console.warn('RobotArena platform probe player boundary failed.', playerError);
                        }
                    }
                }

                if (sdk && typeof sdk.getLeaderboards === 'function') {
                    try {
                        var leaderboardRequest = sdk.getLeaderboards();
                        if (leaderboardRequest && typeof leaderboardRequest.then === 'function') {
                            leaderboardRequest.then(function () {
                                if (typeof console !== 'undefined' && typeof console.info === 'function') {
                                    console.info('RobotArena platform probe leaderboard boundary is callable.');
                                }
                            }, function (leaderboardError) {
                                if (typeof console !== 'undefined' && typeof console.warn === 'function') {
                                    console.warn('RobotArena platform probe leaderboard boundary failed.', leaderboardError);
                                }
                            });
                        }
                    } catch (leaderboardError) {
                        if (typeof console !== 'undefined' && typeof console.warn === 'function') {
                            console.warn('RobotArena platform probe leaderboard boundary failed.', leaderboardError);
                        }
                    }
                }
            }).catch(function (error) {
                if (state.finished) {
                    return;
                }

                clearTimeout(timeoutId);
                state.finished = true;
                fail(error && error.message ? error.message : 'Yandex SDK initialization failed.', false);
            });
        } catch (error) {
            clearTimeout(timeoutId);
            state.finished = true;
            fail(error && error.message ? error.message : 'Yandex SDK initialization failed.', false);
        }
    },

    RobotArenaPlatformProbe_MarkGameReady: function (hostNamePtr) {
        var hostName = UTF8ToString(hostNamePtr);
        var state = window.__robotArenaPlatformProbe;
        if (!state ||
            state.hostName !== hostName ||
            state.gameReady ||
            state.gameReadyRequested ||
            !state.sdk) {
            return;
        }

        var loadingApi = state.sdk.features && state.sdk.features.LoadingAPI;
        if (!loadingApi || typeof loadingApi.ready !== 'function') {
            return;
        }

        state.gameReadyRequested = true;
        try {
            loadingApi.ready();
            state.gameReady = true;
            var snapshot = state.snapshot || {};
            snapshot.gameReady = true;
            snapshot.error = '';
            if (typeof state.sendSnapshot === 'function') {
                state.sendSnapshot(snapshot);
            }
        } catch (error) {
            var failure = state.snapshot || {};
            failure.error = error && error.message ? error.message : 'LoadingAPI.ready failed.';
            if (typeof state.sendSnapshot === 'function') {
                state.sendSnapshot(failure);
            }
        }
    }
});

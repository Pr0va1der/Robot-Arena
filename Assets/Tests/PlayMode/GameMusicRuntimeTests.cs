using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace RobotArena.Session.Tests
{
    public sealed class GameMusicRuntimeTests
    {
        private Component createdRuntime;
        private GameObject createdPauseMenuObject;

        [UnityTearDown]
        public IEnumerator DestroyCreatedRuntime()
        {
            if (createdPauseMenuObject != null)
            {
                UnityEngine.Object.Destroy(createdPauseMenuObject);
                createdPauseMenuObject = null;
            }

            if (createdRuntime != null)
            {
                UnityEngine.Object.Destroy(createdRuntime.gameObject);
                createdRuntime = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Runtime_diagnostics_keep_active_intro_paused_during_composed_audio_pause()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = owner;
            }

            GameObject pauseObject = new GameObject("MusicRuntimePauseIntegrationCanvas");
            createdPauseMenuObject = pauseObject;
            Canvas canvas = pauseObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            pauseObject.AddComponent<CanvasScaler>();
            pauseObject.AddComponent<GraphicRaycaster>();
            Type pauseMenuType = Type.GetType("PauseMenu, Assembly-CSharp", true);
            Component pauseMenu = pauseObject.AddComponent(pauseMenuType);
            Type desktopUiType = Type.GetType("DesktopArenaUi, Assembly-CSharp", true);
            Behaviour desktopUi = (Behaviour)pauseObject.GetComponent(desktopUiType);
            if (desktopUi != null)
            {
                desktopUi.enabled = false;
            }

            yield return null;

            FieldInfo focusLostField = runtimeType.GetField(
                "applicationFocusLost",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo applicationPausedField = runtimeType.GetField(
                "applicationPaused",
                BindingFlags.Instance | BindingFlags.NonPublic);
            focusLostField.SetValue(owner, false);
            applicationPausedField.SetValue(owner, false);

            AudioClip testIntro = AudioClip.Create("MusicRuntimeTestIntro", 480, 1, 48000, false);
            AudioClip testLoop = AudioClip.Create("MusicRuntimeTestLoop", 480, 1, 48000, false);
            MethodInfo startMode = runtimeType.GetMethod(
                "StartMode",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(AudioClip) },
                null);
            MethodInfo requestCue = runtimeType.GetMethod(
                "OnCueRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeCueField = runtimeType.GetField(
                "activeCue",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                startMode.Invoke(owner, new object[] { testIntro, testLoop });
                yield return null;

                PropertyInfo diagnosticsProperty = runtimeType.GetProperty(
                    "Diagnostics",
                    BindingFlags.Public | BindingFlags.Instance);
                MusicRuntimeDiagnostics diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.PlaybackPhase, Is.EqualTo(MusicPlaybackPhase.Intro));

                pauseMenuType.GetMethod("SetTutorialMode").Invoke(pauseMenu, new object[] { false });
                MethodInfo setPauseSource = pauseMenuType.GetMethod("SetPauseSource");
                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Focus, true });
                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Advertisement, true });
                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Platform, true });
                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.User, true });
                yield return null;

                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.IsAudioPaused, Is.True);
                Assert.That(diagnostics.PlaybackPhase, Is.EqualTo(MusicPlaybackPhase.Intro));
                Assert.That(diagnostics.ActivePauseSources.HasFlag(PauseSource.Focus), Is.True);
                Assert.That(diagnostics.ActivePauseSources.HasFlag(PauseSource.Advertisement), Is.True);
                Assert.That(diagnostics.ActivePauseSources.HasFlag(PauseSource.Platform), Is.True);
                Assert.That(diagnostics.ActivePauseSources.HasFlag(PauseSource.User), Is.True);
                Assert.That(diagnostics.AudibleSourceCount, Is.Zero);
                Assert.That(diagnostics.ActiveSequenceAudibleSourceCount, Is.Zero);

                yield return new WaitForSecondsRealtime(0.25f);
                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.PlaybackPhase, Is.EqualTo(MusicPlaybackPhase.Intro));

                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Focus, false });
                yield return null;
                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.IsAudioPaused, Is.True);

                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Advertisement, false });
                yield return null;
                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.IsAudioPaused, Is.True);

                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.Platform, false });
                yield return null;
                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.IsAudioPaused, Is.True);

                setPauseSource.Invoke(pauseMenu, new object[] { PauseSource.User, false });
                yield return null;
                focusLostField.SetValue(owner, false);
                applicationPausedField.SetValue(owner, false);

                diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.IsAudioPaused, Is.False);
                PropertyInfo requiresPointerLockClick = pauseMenuType.GetProperty("RequiresPointerLockClick");
                Assert.That((bool)requiresPointerLockClick.GetValue(pauseMenu), Is.True);

                requestCue.Invoke(owner, new object[] { MusicCue.Silent });
                yield return new WaitForSecondsRealtime(0.6f);
            }
            finally
            {
                requestCue.Invoke(owner, new object[] { MusicCue.Silent });
                activeCueField.SetValue(owner, MusicCue.None);
                UnityEngine.Object.Destroy(testIntro);
                UnityEngine.Object.Destroy(testLoop);
            }
        }

        [UnityTest]
        public IEnumerator Early_stopped_intro_does_not_promote_to_loop_after_timer_expires()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = owner;
            }

            GameObject pauseObject = new GameObject("MusicRuntimeEarlyStopCanvas");
            createdPauseMenuObject = pauseObject;
            Canvas canvas = pauseObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            pauseObject.AddComponent<CanvasScaler>();
            pauseObject.AddComponent<GraphicRaycaster>();
            Type pauseMenuType = Type.GetType("PauseMenu, Assembly-CSharp", true);
            Component pauseMenu = pauseObject.AddComponent(pauseMenuType);
            Behaviour desktopUi = (Behaviour)pauseObject.GetComponent(
                Type.GetType("DesktopArenaUi, Assembly-CSharp", true));
            if (desktopUi != null)
            {
                desktopUi.enabled = false;
            }

            AudioClip testIntro = AudioClip.Create("MusicRuntimeEarlyStopIntro", 48000, 1, 48000, false);
            AudioClip testLoop = AudioClip.Create("MusicRuntimeEarlyStopLoop", 48000, 1, 48000, false);
            MethodInfo startMode = runtimeType.GetMethod(
                "StartMode",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(AudioClip) },
                null);
            MethodInfo requestCue = runtimeType.GetMethod(
                "OnCueRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo groupsField = runtimeType.GetField(
                "groups",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeGroupField = runtimeType.GetField(
                "activeGroup",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo diagnosticsProperty = runtimeType.GetProperty(
                "Diagnostics",
                BindingFlags.Public | BindingFlags.Instance);
            FieldInfo audioPausedBackingField = pauseMenuType.GetField(
                "<AudioIsPaused>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo focusLostField = runtimeType.GetField(
                "applicationFocusLost",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo applicationPausedField = runtimeType.GetField(
                "applicationPaused",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                yield return null;
                pauseMenuType.GetMethod("SetTutorialMode").Invoke(pauseMenu, new object[] { false });
                audioPausedBackingField.SetValue(null, false);
                focusLostField.SetValue(owner, false);
                applicationPausedField.SetValue(owner, false);
                AudioListener.pause = false;
                yield return null;

                startMode.Invoke(owner, new object[] { testIntro, testLoop });
                yield return null;

                Array groups = (Array)groupsField.GetValue(owner);
                int activeGroup = (int)activeGroupField.GetValue(owner);
                object group = groups.GetValue(activeGroup);
                Type groupType = group.GetType();
                AudioSource introSource = (AudioSource)groupType.GetField(
                    "introSource",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(group);
                AudioSource loopSource = (AudioSource)groupType.GetField(
                    "loopSource",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(group);

                Assert.That(introSource.isPlaying, Is.True);
                yield return new WaitForSecondsRealtime(0.15f);
                introSource.Stop();

                yield return new WaitForSecondsRealtime(1.25f);

                MusicRuntimeDiagnostics diagnostics =
                    (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
                Assert.That(diagnostics.PlaybackPhase, Is.EqualTo(MusicPlaybackPhase.Intro));
                Assert.That(loopSource.isPlaying, Is.False);

                requestCue.Invoke(owner, new object[] { MusicCue.Silent });
                yield return new WaitForSecondsRealtime(0.6f);
            }
            finally
            {
                requestCue.Invoke(owner, new object[] { MusicCue.Silent });
                UnityEngine.Object.Destroy(testIntro);
                UnityEngine.Object.Destroy(testLoop);
            }
        }

        [UnityTest]
        public IEnumerator Runtime_creation_returns_one_persistent_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            MethodInfo getOrCreate = runtimeType.GetMethod(
                "GetOrCreate",
                BindingFlags.Public | BindingFlags.Static);

            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component first = (Component)getOrCreate.Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = first;
            }
            yield return null;
            Component second = (Component)getOrCreate.Invoke(null, null);

            Assert.That(second, Is.SameAs(first));
            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Runtime_diagnostics_report_one_owner_and_silent_playback_before_permission()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = owner;
            }

            yield return null;

            PropertyInfo diagnosticsProperty = runtimeType.GetProperty(
                "Diagnostics",
                BindingFlags.Public | BindingFlags.Instance);
            MusicRuntimeDiagnostics diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);

            Assert.That(diagnostics.OwnerCount, Is.EqualTo(1));
            Assert.That(diagnostics.SemanticMode, Is.EqualTo(MusicMode.Silent));
            Assert.That(diagnostics.ActiveCue, Is.EqualTo(MusicCue.None));
            Assert.That(diagnostics.PlaybackPhase, Is.EqualTo(MusicPlaybackPhase.Silent));
            Assert.That(diagnostics.ActiveGroupIndex, Is.EqualTo(-1));
            Assert.That(diagnostics.FadingGroupIndex, Is.EqualTo(-1));
            Assert.That(diagnostics.FadingGroupMask, Is.Zero);
            Assert.That(diagnostics.FadingSemanticModeMask, Is.Zero);
            Assert.That(diagnostics.AudibleSourceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Scene_duplicate_is_destroyed_before_it_can_become_an_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = owner;
            }
            GameObject duplicateObject = new GameObject("DuplicateGameMusicRuntime");
            duplicateObject.AddComponent(runtimeType);

            yield return null;

            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectOfType(runtimeType), Is.SameAs(owner));

            PropertyInfo diagnosticsProperty = runtimeType.GetProperty(
                "Diagnostics",
                BindingFlags.Public | BindingFlags.Instance);
            MusicRuntimeDiagnostics diagnostics = (MusicRuntimeDiagnostics)diagnosticsProperty.GetValue(owner);
            Assert.That(diagnostics.OwnerCount, Is.EqualTo(1));
        }
    }
}

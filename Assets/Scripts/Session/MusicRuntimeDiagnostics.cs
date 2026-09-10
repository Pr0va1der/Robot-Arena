using System;

namespace RobotArena.Session
{
    public readonly struct MusicRuntimeDiagnostics : IEquatable<MusicRuntimeDiagnostics>
    {
        public MusicRuntimeDiagnostics(
            int ownerCount,
            MusicMode semanticMode,
            MusicCue activeCue,
            MusicPlaybackPhase playbackPhase,
            int activeGroupIndex,
            int fadingGroupIndex,
            int fadingGroupMask,
            int audibleSourceCount,
            int activeSequenceAudibleSourceCount,
            bool intentionalCrossfade,
            bool isAudioPaused)
        {
            OwnerCount = ownerCount;
            SemanticMode = semanticMode;
            ActiveCue = activeCue;
            PlaybackPhase = playbackPhase;
            ActiveGroupIndex = activeGroupIndex;
            FadingGroupIndex = fadingGroupIndex;
            FadingGroupMask = fadingGroupMask;
            AudibleSourceCount = audibleSourceCount;
            ActiveSequenceAudibleSourceCount = activeSequenceAudibleSourceCount;
            IntentionalCrossfade = intentionalCrossfade;
            IsAudioPaused = isAudioPaused;
        }

        public int OwnerCount { get; }
        public MusicMode SemanticMode { get; }
        public MusicCue ActiveCue { get; }
        public MusicPlaybackPhase PlaybackPhase { get; }
        public int ActiveGroupIndex { get; }
        public int FadingGroupIndex { get; }
        public int FadingGroupMask { get; }
        public int AudibleSourceCount { get; }
        public int ActiveSequenceAudibleSourceCount { get; }
        public bool IntentionalCrossfade { get; }
        public bool IsAudioPaused { get; }

        public bool Equals(MusicRuntimeDiagnostics other)
        {
            return OwnerCount == other.OwnerCount &&
                   SemanticMode == other.SemanticMode &&
                   ActiveCue == other.ActiveCue &&
                   PlaybackPhase == other.PlaybackPhase &&
                   ActiveGroupIndex == other.ActiveGroupIndex &&
                   FadingGroupIndex == other.FadingGroupIndex &&
                   FadingGroupMask == other.FadingGroupMask &&
                   AudibleSourceCount == other.AudibleSourceCount &&
                   ActiveSequenceAudibleSourceCount == other.ActiveSequenceAudibleSourceCount &&
                   IntentionalCrossfade == other.IntentionalCrossfade &&
                   IsAudioPaused == other.IsAudioPaused;
        }

        public override bool Equals(object obj)
        {
            return obj is MusicRuntimeDiagnostics other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = OwnerCount;
                hash = (hash * 397) ^ (int)SemanticMode;
                hash = (hash * 397) ^ (int)ActiveCue;
                hash = (hash * 397) ^ (int)PlaybackPhase;
                hash = (hash * 397) ^ ActiveGroupIndex;
                hash = (hash * 397) ^ FadingGroupIndex;
                hash = (hash * 397) ^ FadingGroupMask;
                hash = (hash * 397) ^ AudibleSourceCount;
                hash = (hash * 397) ^ ActiveSequenceAudibleSourceCount;
                hash = (hash * 397) ^ (IntentionalCrossfade ? 1 : 0);
                return (hash * 397) ^ (IsAudioPaused ? 1 : 0);
            }
        }

        public override string ToString()
        {
            return string.Format(
                "ownerCount={0}; mode={1}; cue={2}; phase={3}; activeGroup={4}; fadingGroup={5}; fadingGroupMask={6}; audibleSources={7}; activeSequenceAudibleSources={8}; intentionalCrossfade={9}; audioPaused={10}",
                OwnerCount,
                SemanticMode,
                ActiveCue,
                PlaybackPhase,
                ActiveGroupIndex,
                FadingGroupIndex,
                FadingGroupMask,
                AudibleSourceCount,
                ActiveSequenceAudibleSourceCount,
                IntentionalCrossfade,
                IsAudioPaused);
        }
    }
}

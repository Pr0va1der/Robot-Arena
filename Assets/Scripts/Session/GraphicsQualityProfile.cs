namespace RobotArena.Session
{
    public enum GraphicsQualityProfile
    {
        Performance = 0,
        Quality = 1
    }

    public static class GraphicsQualityProfileCatalog
    {
        public const string PerformanceQualityName = "Low";
        public const string QualityQualityName = "High";
        public const int PerformanceQualityLevelIndex = 1;
        public const int QualityQualityLevelIndex = 3;

        public static GraphicsQualityProfile Normalize(GraphicsQualityProfile profile)
        {
            return profile == GraphicsQualityProfile.Quality
                ? GraphicsQualityProfile.Quality
                : GraphicsQualityProfile.Performance;
        }

        public static string GetQualityLevelName(GraphicsQualityProfile profile)
        {
            return Normalize(profile) == GraphicsQualityProfile.Quality
                ? QualityQualityName
                : PerformanceQualityName;
        }

        public static int GetFallbackQualityLevelIndex(GraphicsQualityProfile profile)
        {
            return Normalize(profile) == GraphicsQualityProfile.Quality
                ? QualityQualityLevelIndex
                : PerformanceQualityLevelIndex;
        }
    }
}

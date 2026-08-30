using System;

namespace RobotArena.Session
{
    public readonly struct NormalizedViewport
    {
        public NormalizedViewport(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
    }

    public static class DesktopViewportLayout
    {
        public const float TargetAspectRatio = 16f / 9f;

        public static NormalizedViewport Calculate(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            }

            float displayAspectRatio = pixelWidth / (float)pixelHeight;
            if (displayAspectRatio > TargetAspectRatio)
            {
                float width = TargetAspectRatio / displayAspectRatio;
                return new NormalizedViewport((1f - width) * 0.5f, 0f, width, 1f);
            }

            float height = displayAspectRatio / TargetAspectRatio;
            return new NormalizedViewport(0f, (1f - height) * 0.5f, 1f, height);
        }
    }
}

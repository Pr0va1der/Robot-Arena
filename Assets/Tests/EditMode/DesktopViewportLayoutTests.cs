using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class DesktopViewportLayoutTests
    {
        [Test]
        public void Wide_display_centers_a_16_by_9_viewport()
        {
            NormalizedViewport viewport = DesktopViewportLayout.Calculate(2560, 1080);

            Assert.That(viewport.X, Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(viewport.Y, Is.EqualTo(0f));
            Assert.That(viewport.Width, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(viewport.Height, Is.EqualTo(1f));
        }

        [Test]
        public void Tall_display_centers_a_16_by_9_viewport()
        {
            NormalizedViewport viewport = DesktopViewportLayout.Calculate(1080, 1920);

            Assert.That(viewport.X, Is.EqualTo(0f));
            Assert.That(viewport.Y, Is.EqualTo(0.341796875f).Within(0.0001f));
            Assert.That(viewport.Width, Is.EqualTo(1f));
            Assert.That(viewport.Height, Is.EqualTo(0.31640625f).Within(0.0001f));
        }

        [Test]
        public void Invalid_display_dimensions_are_rejected()
        {
            Assert.That(
                () => DesktopViewportLayout.Calculate(0, 1080),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());

            Assert.That(
                () => DesktopViewportLayout.Calculate(1920, 0),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}

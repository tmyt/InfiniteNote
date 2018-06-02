using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace InfiniteNote.Extensions
{
    public static class PointExtension
    {
        public static Point Translate(this Point pt, double x, double y)
        {
            return new Point(pt.X + x, pt.Y + y);
        }
    }
}
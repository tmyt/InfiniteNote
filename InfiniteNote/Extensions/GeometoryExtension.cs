using Windows.Foundation;

namespace InfiniteNote.Extensions
{
    public static class GeometoryExtension
    {
        public static Point Translate(this Point pt, double x, double y)
        {
            return new Point(pt.X + x, pt.Y + y);
        }

        public static bool IsIntersect(this Rect rect, Rect target)
        {
            rect.Intersect(target);
            return rect != Rect.Empty;
        }

        public static Rect Scale(this Rect rect, double scale)
        {
            return new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
        }

        public static Size Scale(this Size size, double scale)
        {
            return new Size(size.Width * scale, size.Height * scale);
        }

        public static Point Scale(this Point point, double scale)
        {
            return new Point(point.X * scale, point.Y * scale);
        }
    }
}

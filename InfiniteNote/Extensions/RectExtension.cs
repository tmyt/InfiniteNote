using Windows.Foundation;

namespace InfiniteNote.Extensions
{
    public static class RectExtension
    {
        public static bool IsIntersect(this Rect rect, Rect target)
        {
            rect.Intersect(target);
            return rect != Rect.Empty;
        }
    }
}
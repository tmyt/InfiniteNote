using System.Collections.Generic;
using System.Linq;
using Windows.UI.Input.Inking;
using InfiniteNote.Serializable;

namespace InfiniteNote.Extensions
{
    public static class SerializableExtension
    {
        public static IEnumerable<InkStroke> AsInkStrokes(this IEnumerable<SerializableStroke> strokes)
        {
            return strokes.Select(x => x.AsStroke());
        }

        public static IEnumerable<SerializableStroke> AsSerializable(this IEnumerable<InkStroke> strokes)
        {
            return strokes.Select(x => new SerializableStroke(x));
        }

        public static IEnumerable<InkPoint> AsInkPoints(this IEnumerable<SerializablePoint> points)
        {
            return points.Select(x => x.AsPoint());
        }

        public static IEnumerable<SerializablePoint> AsSerializable(this IEnumerable<InkPoint> points)
        {
            return points.Select(x => new SerializablePoint(x));
        }
    }
}
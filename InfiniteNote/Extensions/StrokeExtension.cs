using System.Linq;
using Windows.UI.Input.Inking;

namespace InfiniteNote.Extensions
{
    public static class StrokeExtension
    {
        public static InkStroke Translate(this InkStroke inkStroke, double left, double top, double scale = 1.0)
        {
            var builder = new InkStrokeBuilder();
            var points = inkStroke.GetInkPoints()
                .Select(x => new InkPoint(x.Position.Translate(left, top).Scale(scale), x.Pressure, x.TiltX, x.TiltY, x.Timestamp))
                .ToList();
            var newStroke = builder.CreateStrokeFromInkPoints(points, inkStroke.PointTransform, inkStroke.StrokeStartedTime, inkStroke.StrokeDuration);
            newStroke.DrawingAttributes = inkStroke.DrawingAttributes;
            return newStroke;
        }
    }
}
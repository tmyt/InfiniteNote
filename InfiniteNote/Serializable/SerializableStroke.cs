using System;
using System.Linq;
using System.Numerics;
using Windows.UI.Input.Inking;
using InfiniteNote.Extensions;

namespace InfiniteNote.Serializable
{
    public class SerializableStroke
    {
        static InkStrokeBuilder strokeBuilder = new InkStrokeBuilder();

        public Matrix3x2 PointTransform { get; set; }
        public InkDrawingAttributes DrawingAttributes { get; set; }
        public TimeSpan? StrokeDuration { get; set; }
        public DateTimeOffset? StrokeStartedTime { get; set; }
        public SerializablePoint[] InkPoints { get; set; }

        public SerializableStroke()
        {
        }

        public SerializableStroke(InkStroke stroke)
        {
            PointTransform = stroke.PointTransform;
            DrawingAttributes = stroke.DrawingAttributes;
            StrokeDuration = stroke.StrokeDuration;
            StrokeStartedTime = stroke.StrokeStartedTime;
            InkPoints = stroke.GetInkPoints().AsSerializable().ToArray();
        }

        public InkStroke AsStroke()
        {
            var stroke = strokeBuilder.CreateStrokeFromInkPoints(InkPoints.AsInkPoints(), PointTransform, StrokeStartedTime, StrokeDuration);
            stroke.DrawingAttributes = DrawingAttributes;
            return stroke;
        }
    }
}
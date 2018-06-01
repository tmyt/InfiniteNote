using Windows.Foundation;
using Windows.UI.Input.Inking;

namespace InfiniteNote.Serializable
{
    public class SerializablePoint
    {
        public Point Position { get; set; }
        public float Pressure { get; set; }
        public float TiltX { get; set; }
        public float TiltY { get; set; }
        public ulong Timestamp { get; set; }

        public SerializablePoint() { }

        public SerializablePoint(InkPoint point)
        {
            Position = point.Position;
            Pressure = point.Pressure;
            TiltX = point.TiltX;
            TiltY = point.TiltY;
            Timestamp = point.Timestamp;
        }

        public InkPoint AsPoint()
        {
            return new InkPoint(Position, Pressure, TiltX, TiltY, Timestamp);
        }
    }
}
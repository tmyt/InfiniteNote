using Windows.Foundation;
using InfiniteNote.Serializable;

namespace InfiniteNote.Models
{
    public class ApplicationState
    {
        public SerializableStroke[] Strokes { get; set; }
        public Rect Viewport { get; set; }
    }
}
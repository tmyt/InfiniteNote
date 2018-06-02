using Windows.Foundation;

namespace InfiniteNote.Models
{
    public class DefaultSizeAction : IAction
    {
        public ActionType Type { get; set; }
        public Rect Viewport { get; set; }
    }
}
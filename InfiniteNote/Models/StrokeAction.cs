using System.Collections.Generic;
using Windows.UI.Input.Inking;

namespace InfiniteNote.Models
{
    public class StrokeAction : IAction
    {
        public ActionType Type { get; set; }
        public IReadOnlyList<InkStroke> Strokes { get; set; }
    }
}
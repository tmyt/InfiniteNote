namespace InfiniteNote.Models
{
    public class ResizeAction : IAction
    {
        public ActionType Type { get; set; }
        public double ExtendsX { get; set; }
        public double ExtendsY { get; set; }
    }
}
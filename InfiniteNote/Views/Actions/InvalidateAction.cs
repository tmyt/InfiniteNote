using Windows.UI.Xaml;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Actions
{
    public class InvalidateAction : DependencyObject, IAction
    {
        public object Execute(object sender, object parameter)
        {
            ((CanvasVirtualControl)sender).Invalidate();
            return null;
        }
    }
}

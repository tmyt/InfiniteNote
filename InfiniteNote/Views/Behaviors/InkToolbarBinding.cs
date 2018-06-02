using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Behaviors
{
    public class InkToolbarBinding : DependencyObject, IBehavior
    {
        public static readonly DependencyProperty ActiveToolKindProperty = DependencyProperty.RegisterAttached(
            "ActiveToolKind", typeof(InkToolbarTool), typeof(InkToolbarBinding), new PropertyMetadata(default(InkToolbarTool)));

        public static void SetActiveToolKind(DependencyObject element, InkToolbarTool value)
        {
            element.SetValue(ActiveToolKindProperty, value);
        }

        public static InkToolbarTool GetActiveToolKind(DependencyObject element)
        {
            return (InkToolbarTool)element.GetValue(ActiveToolKindProperty);
        }

        public void Attach(DependencyObject associatedObject)
        {
            var toolbar = (InkToolbar)(AssociatedObject = associatedObject);
            toolbar.ActiveToolChanged += ActiveToolChanged;
            SetActiveToolKind(associatedObject, toolbar.ActiveTool?.ToolKind ?? InkToolbarTool.BallpointPen);
        }

        public void Detach()
        {
            var toolbar = (InkToolbar)AssociatedObject;
            toolbar.ActiveToolChanged -= ActiveToolChanged;
        }

        private void ActiveToolChanged(InkToolbar sender, object args)
        {
            SetActiveToolKind(AssociatedObject, sender.ActiveTool.ToolKind);
        }

        public DependencyObject AssociatedObject { get; private set; }
    }
}

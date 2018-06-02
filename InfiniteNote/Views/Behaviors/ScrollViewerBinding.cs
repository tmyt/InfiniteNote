using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Behaviors
{
    public class ScrollViewerBinding : DependencyObject, IBehavior
    {
        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.RegisterAttached(
            "HorizontalOffset", typeof(double), typeof(ScrollViewerBinding), new PropertyMetadata(default(double), HozontalOffsetChanged));

        private static void HozontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollViewer)d).ChangeView((double)e.NewValue, null, null, true);
        }

        public static void SetHorizontalOffset(DependencyObject element, double value)
        {
            element.SetValue(HorizontalOffsetProperty, value);
        }

        public static double GetHorizontalOffset(DependencyObject element)
        {
            return (double)element.GetValue(HorizontalOffsetProperty);
        }

        public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.RegisterAttached(
            "VerticalOffset", typeof(double), typeof(ScrollViewerBinding), new PropertyMetadata(default(double), VerticalOffsetChanged));

        private static void VerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ScrollViewer)d).ChangeView(null, (double)e.NewValue, null, true);
        }

        public static void SetVerticalOffset(DependencyObject element, double value)
        {
            element.SetValue(VerticalOffsetProperty, value);
        }

        public static double GetVerticalOffset(DependencyObject element)
        {
            return (double)element.GetValue(VerticalOffsetProperty);
        }

        public static readonly DependencyProperty ScrollEnabledProperty = DependencyProperty.RegisterAttached(
            "ScrollEnabled", typeof(bool), typeof(ScrollViewerBinding), new PropertyMetadata(default(bool), ScrollEnabledChanged));

        private static void ScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)d;
            if ((bool) e.NewValue)
            {
                scrollViewer.HorizontalScrollMode = ScrollMode.Auto;
                scrollViewer.VerticalScrollMode = ScrollMode.Auto;
            }
            else
            {
                scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
                scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
                scrollViewer.CancelDirectManipulations();
            }
        }

        public static void SetScrollEnabled(DependencyObject element, bool value)
        {
            element.SetValue(ScrollEnabledProperty, value);
        }

        public static bool GetScrollEnabled(DependencyObject element)
        {
            return (bool) element.GetValue(ScrollEnabledProperty);
        }

        public void Attach(DependencyObject associatedObject)
        {
            ((ScrollViewer)associatedObject).ViewChanged += ScrollViewerBinding_ViewChanged;
            AssociatedObject = associatedObject;
        }

        public void Detach()
        {
            ((ScrollViewer)AssociatedObject).ViewChanged -= ScrollViewerBinding_ViewChanged;
        }

        public DependencyObject AssociatedObject { get; private set; }

        private void ScrollViewerBinding_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            SetHorizontalOffset((DependencyObject)sender, ((ScrollViewer)sender).HorizontalOffset);
            SetVerticalOffset((DependencyObject)sender, ((ScrollViewer)sender).VerticalOffset);
        }
    }
}

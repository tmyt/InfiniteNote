using Windows.UI.Xaml;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Behaviors
{
    public class FrameworkElementBinding : DependencyObject, IBehavior
    {
        public static readonly DependencyProperty ActualWidthProperty = DependencyProperty.RegisterAttached(
            "ActualWidth", typeof(double), typeof(FrameworkElementBinding), new PropertyMetadata(default(double)));

        public static void SetActualWidth(DependencyObject element, double value)
        {
            element.SetValue(ActualWidthProperty, value);
        }

        public static double GetActualWidth(DependencyObject element)
        {
            return (double)element.GetValue(ActualWidthProperty);
        }

        public static readonly DependencyProperty ActualHeightProperty = DependencyProperty.RegisterAttached(
            "ActualHeight", typeof(double), typeof(FrameworkElementBinding), new PropertyMetadata(default(double)));

        public static void SetActualHeight(DependencyObject element, double value)
        {
            element.SetValue(ActualHeightProperty, value);
        }

        public static double GetActualHeight(DependencyObject element)
        {
            return (double)element.GetValue(ActualHeightProperty);
        }

        public void Attach(DependencyObject associatedObject)
        {
            ((FrameworkElement)associatedObject).SizeChanged += SizeChanged;
            AssociatedObject = associatedObject;
        }

        public void Detach()
        {
            ((FrameworkElement)AssociatedObject).SizeChanged -= SizeChanged;
        }

        public DependencyObject AssociatedObject { get; private set; }

        private void SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetActualWidth((DependencyObject)sender, e.NewSize.Width);
            SetActualHeight((DependencyObject)sender, e.NewSize.Height);
        }

    }
}

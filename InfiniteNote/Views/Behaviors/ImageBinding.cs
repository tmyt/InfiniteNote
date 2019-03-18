using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace InfiniteNote.Views.Behaviors
{
    public class ImageBinding
    {
        public static readonly DependencyProperty ImageStreamProperty = DependencyProperty.RegisterAttached(
            "ImageStream", typeof(IRandomAccessStream), typeof(ImageBinding), new PropertyMetadata(default(IRandomAccessStream), ImageStreamChanged));

        private static void ImageStreamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bitmap = new BitmapImage();
            bitmap.SetSource((IRandomAccessStream)e.NewValue);
            ((Image)d).Source = bitmap;
        }

        public static void SetImageStream(DependencyObject element, IRandomAccessStream value)
        {
            element.SetValue(ImageStreamProperty, value);
        }

        public static IRandomAccessStream GetImageStream(DependencyObject element)
        {
            return (IRandomAccessStream)element.GetValue(ImageStreamProperty);
        }
    }
}

using Reactive.Bindings;
using System;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace InfiniteNote.ViewModels
{
    public partial class MainViewModel
    {
        public ReactiveProperty<IRandomAccessStream> MinimapImage { get; private set; }
        public ReadOnlyReactiveProperty<double> MinimapViewportScale { get; private set; }
        public ReadOnlyReactiveProperty<double> MinimapViewportWidth { get; private set; }
        public ReadOnlyReactiveProperty<double> MinimapViewportHeight { get; private set; }
        public ReadOnlyReactiveProperty<double> MinimapViewportTop { get; private set; }
        public ReadOnlyReactiveProperty<double> MinimapViewportLeft { get; private set; }

        async void UpdateMinimap()
        {
            var scaleW = 100 / _drawingData.CanvasWidth;
            var scaleH = 100 / _drawingData.CanvasHeight;
            var scale = Math.Min(scaleW, scaleH);
            var minimapImage = await _inkRenderer.ConvertInkToPng(
                new Rect(0, 0, _drawingData.CanvasWidth, _drawingData.CanvasHeight), scale, _drawingData.Strokes, 1);
            MinimapImage.Value = minimapImage;
        }
    }
}

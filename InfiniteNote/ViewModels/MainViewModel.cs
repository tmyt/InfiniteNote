using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using InfiniteNote.Extensions;
using InfiniteNote.Models;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace InfiniteNote.ViewModels
{
    public partial class MainViewModel
    {
        private DrawingData _drawingData;
        private InkRenderer _inkRenderer;
        private bool _resumed;

        public ReactiveProperty<double> CanvasWidth { get; private set; }
        public ReactiveProperty<double> CanvasHeight { get; private set; }
        public ReactiveProperty<double> ViewportOffsetX { get; private set; }
        public ReactiveProperty<double> ViewportOffsetY { get; private set; }
        public ReactiveProperty<double> ViewportWidth { get; private set; }
        public ReactiveProperty<double> ViewportHeight { get; private set; }
        public ReactiveProperty<InkToolbarTool> ActiveTool { get; private set; }
        public ReactiveProperty<bool> IsScrollEnabled { get; private set; }
        public ReactiveProperty<bool> IsTouchInputEnabled { get; private set; }

        public MainViewModel()
        {
            _drawingData = new DrawingData();
            _inkRenderer = new InkRenderer();
            CanvasWidth = _drawingData.ObserveProperty(x => x.CanvasWidth)
                .ToReactiveProperty();
            CanvasHeight = _drawingData.ObserveProperty(x => x.CanvasHeight)
                .ToReactiveProperty();
            ViewportOffsetX = _drawingData.ToReactivePropertyAsSynchronized(x => x.OffsetX);
            ViewportOffsetY = _drawingData.ToReactivePropertyAsSynchronized(x => x.OffsetY);
            ViewportWidth = _drawingData.ToReactivePropertyAsSynchronized(x => x.ViewWidth);
            ViewportHeight = _drawingData.ToReactivePropertyAsSynchronized(x => x.ViewHeight);
            ActiveTool = new ReactiveProperty<InkToolbarTool>();
            IsScrollEnabled = new ReactiveProperty<bool>();
            IsTouchInputEnabled = new ReactiveProperty<bool>();
            DataTransferManager.GetForCurrentView().DataRequested += DataRequested;
        }

        private async void DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            using (var image = await GeneratePng())
            {
                var reference = RandomAccessStreamReference.CreateFromStream(image);
                args.Request.Data.SetBitmap(reference);
                args.Request.Data.Properties.Title = "Created with Infinite Note";
            }
            deferral.Complete();
        }

        public void DrawStrokes(IEnumerable<InkStroke> strokes)
        {
            _drawingData.DrawStrokes(strokes.Select(x => x.Translate(ViewportOffsetX.Value, ViewportOffsetY.Value)));
        }

        public Task<InMemoryRandomAccessStream> GeneratePng()
        {
            return _inkRenderer.ConvertInkToPng(_drawingData.Viewport, _drawingData.Strokes);
        }

        public async void Loaded()
        {
            _drawingData.MoveToCenter();
            await ResumeCore();
        }

        public async Task ResumeCore()
        {
            if (_resumed || !await _drawingData.RestoreState()) return;
            _resumed = true;
        }

        public Task SuspendCore()
        {
            return _drawingData.SaveState();
        }

        public bool EraseStrokeFromPoint(Point point)
        {
            var stroke = _drawingData.FindNearestStroke(point);
            if (stroke == null) return false;
            _drawingData.EraseStroke(stroke);
            return true;
        }

        public void RenderPart(CanvasVirtualControl canvas, Rect rect)
        {
            using (var session = canvas.CreateDrawingSession(rect))
            {
                _inkRenderer.Render(session, _drawingData.Strokes.Where(x => rect.IsIntersect(x.BoundingRect)));
            }
        }
    }
}

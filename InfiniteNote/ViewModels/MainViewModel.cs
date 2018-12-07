using InfiniteNote.Extensions;
using InfiniteNote.Models;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;

namespace InfiniteNote.ViewModels
{
    public partial class MainViewModel
    {
        private DrawingData _drawingData;
        private InkRenderer _inkRenderer;
        private bool _resumed;

        public ReadOnlyReactiveProperty<double> CanvasWidth { get; private set; }
        public ReadOnlyReactiveProperty<double> CanvasHeight { get; private set; }
        public ReactiveProperty<double> ViewportOffsetX { get; private set; }
        public ReactiveProperty<double> ViewportOffsetY { get; private set; }
        public ReactiveProperty<double> ViewportWidth { get; private set; }
        public ReactiveProperty<double> ViewportHeight { get; private set; }
        public ReactiveProperty<double> Scale { get; private set; }
        public ReactiveProperty<InkToolbarTool> ActiveTool { get; private set; }
        public ReactiveProperty<bool> IsScrollEnabled { get; private set; }
        public ReactiveProperty<bool> IsTouchInputEnabled { get; private set; }

        public MainViewModel()
        {
            _drawingData = new DrawingData();
            _inkRenderer = new InkRenderer();
            Scale = new ReactiveProperty<double>(1.0);
            CanvasWidth = _drawingData.ObserveProperty(x => x.CanvasWidth)
                .CombineLatest(Scale, (a, b) => a * b)
                .ToReadOnlyReactiveProperty();
            CanvasHeight = _drawingData.ObserveProperty(x => x.CanvasHeight)
                .CombineLatest(Scale, (a, b) => a * b)
                .ToReadOnlyReactiveProperty();
            ViewportOffsetX = _drawingData.ToReactivePropertyAsSynchronized(x => x.OffsetX, x => x * Scale.Value, x => x / Scale.Value);
            ViewportOffsetY = _drawingData.ToReactivePropertyAsSynchronized(x => x.OffsetY, x => x * Scale.Value, x => x / Scale.Value);
            ViewportWidth = _drawingData.ToReactivePropertyAsSynchronized(x => x.ViewWidth, x => x * Scale.Value, x => x / Scale.Value);
            ViewportHeight = _drawingData.ToReactivePropertyAsSynchronized(x => x.ViewHeight, x => x * Scale.Value, x => x / Scale.Value);
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
            _drawingData.DrawStrokes(strokes.Select(x => x.Translate(ViewportOffsetX.Value, ViewportOffsetY.Value, 1 / Scale.Value)));
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
                var scaleMatrix = Matrix3x2.CreateScale((float)Scale.Value, (float)Scale.Value);
                var scaleRect = rect.Scale(1 / Scale.Value);
                var strokes = _drawingData.Strokes.Where(x => scaleRect.IsIntersect(x.BoundingRect))
                    .Select(x =>
                    {
                        var stroke = x.Clone();
                        stroke.PointTransform = Matrix3x2.Multiply(stroke.PointTransform, scaleMatrix);
                        var da = stroke.DrawingAttributes;
                        da.Size = da.Size.Scale(Scale.Value);
                        stroke.DrawingAttributes = da;
                        return stroke;
                    });
                _inkRenderer.Render(session, strokes);
            }
        }
    }
}

using InfiniteNote.Extensions;
using InfiniteNote.Models;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
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
        private readonly DrawingData _drawingData;
        private readonly InkRenderer _inkRenderer;
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
            MinimapImage = new ReactiveProperty<IRandomAccessStream>();
            MinimapViewportScale = _drawingData.ObserveProperty(x => x.CanvasWidth)
                .CombineLatest(_drawingData.ObserveProperty(x => x.CanvasHeight), ValueTuple.Create)
                .Select(((double width, double height) x) =>
                {
                    var scaleW = 100 / x.width;
                    var scaleH = 100 / x.height;
                    return Math.Min(scaleW, scaleH);
                }).ToReadOnlyReactiveProperty();
            MinimapViewportWidth = _drawingData.ObserveProperty(x => x.ViewWidth).Select(x => x * MinimapViewportScale.Value)
                .ToReadOnlyReactiveProperty();
            MinimapViewportHeight = _drawingData.ObserveProperty(x => x.ViewHeight).Select(x => x * MinimapViewportScale.Value)
                .ToReadOnlyReactiveProperty();
            MinimapViewportLeft = _drawingData.ObserveProperty(x => x.OffsetX)
                .Select(x => x * MinimapViewportScale.Value + (100 - _drawingData.CanvasWidth * MinimapViewportScale.Value) / 2)
                .ToReadOnlyReactiveProperty();
            MinimapViewportTop = _drawingData.ObserveProperty(x => x.OffsetY)
                .Select(x => x * MinimapViewportScale.Value + (100 - _drawingData.CanvasHeight * MinimapViewportScale.Value) / 2)
                .ToReadOnlyReactiveProperty();
            ActiveTool = new ReactiveProperty<InkToolbarTool>();
            IsScrollEnabled = new ReactiveProperty<bool>();
            IsTouchInputEnabled = new ReactiveProperty<bool>();
            Scale.Subscribe(x =>
            {
                ViewportOffsetX.ForceNotify();
                ViewportOffsetY.ForceNotify();
                ViewportWidth.ForceNotify();
                ViewportHeight.ForceNotify();
            });
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
            return _inkRenderer.ConvertInkToPng(_drawingData.Viewport, Scale.Value, _drawingData.Strokes);
        }

        public async void Loaded()
        {
            _drawingData.MoveToCenter();
            await ResumeCore();
        }

        public async Task ResumeCore()
        {
            if (_resumed || !await _drawingData.RestoreState()) return;
            UpdateMinimap();
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
                        stroke.PointTransform *= scaleMatrix;
                        var da = stroke.DrawingAttributes;
                        da.Size = da.Size.Scale(Scale.Value);
                        stroke.DrawingAttributes = da;
                        return stroke;
                    })
                    .ToArray();
                _inkRenderer.Render(session, strokes);
            }
        }
    }
}

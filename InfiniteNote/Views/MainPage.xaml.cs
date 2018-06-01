using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using InfiniteNote.Extensions;
using InfiniteNote.Serializable;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Newtonsoft.Json;
using Buffer = Windows.Storage.Streams.Buffer;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace InfiniteNote.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int CanvasWidth = 16384;
        private const int CanvasHeight = 16384;

        private bool _resumed;
        private readonly List<InkStroke> _strokes;
        private readonly Stack<IReadOnlyList<InkStroke>> _undoBuffer;
        private readonly Stack<IReadOnlyList<InkStroke>> _redoBuffer;
        private readonly InkSynchronizer _inkSynchronizer;

        public MainPage()
        {
            this.InitializeComponent();
            _strokes = new List<InkStroke>();
            _undoBuffer = new Stack<IReadOnlyList<InkStroke>>();
            _redoBuffer = new Stack<IReadOnlyList<InkStroke>>();
            _inkSynchronizer = InkCanvas.InkPresenter.ActivateCustomDrying();
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Loaded += MainPage_Loaded;
            // touch
            EnableTouchInkingButton.Checked += (s, e) => InkCanvas.InkPresenter.InputDeviceTypes =
                CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch;
            EnableTouchInkingButton.Unchecked += (s, e) => InkCanvas.InkPresenter.InputDeviceTypes =
                CoreInputDeviceTypes.Pen;
            // commands
            UndoButton.Command = new TaskCommand(Undo);
            RedoButton.Command = new TaskCommand(Redo);
            EraseAllButton.Command = new TaskCommand(EraseAll);
            SaveButton.Command = new TaskCommand(Save);
            CopyButton.Command = new TaskCommand(Copy);
            ShareButton.Command = new TaskCommand(Share);
            CloseButton.Command = new TaskCommand(Close);
            // canvas
            InkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
            InkCanvas.InkPresenter.StrokeInput.StrokeStarted += StrokeInput_StrokeStarted;
            InkCanvas.InkPresenter.StrokeInput.StrokeEnded += StrokeInput_StrokeEnded;
            InkCanvas.InkPresenter.StrokeInput.StrokeCanceled += StrokeInput_StrokeCanceled;
            InkCanvas.InkPresenter.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            DataTransferManager.GetForCurrentView().DataRequested += Manager_DataRequested;
        }

        private void StrokeInput_StrokeCanceled(InkStrokeInput sender, PointerEventArgs args)
        {
            EnableScroll();
        }

        private void StrokeInput_StrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            EnableScroll();
        }

        private void StrokeInput_StrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            DisableScroll();
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            var inkStrokes = _inkSynchronizer.BeginDry().Select(Translate).ToReadOnlyList();
            _undoBuffer.Push(inkStrokes);
            _redoBuffer.Clear();
            // begin dry
            _strokes.AddRange(inkStrokes);
            _inkSynchronizer.EndDry();
            Dry.Invalidate();
        }

        private void CanvasVirtualControl_OnRegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            foreach (var rect in args.InvalidatedRegions)
            {
                using (var session = sender.CreateDrawingSession(rect))
                {
                    session.Clear(Colors.White);
                    var s = _strokes.Where(x => rect.IsIntersect(x.BoundingRect)).ToList();
                    session.DrawInk(s, false);
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            ScrollViewer.ChangeView((CanvasWidth - ScrollViewer.ActualWidth) / 2, (CanvasHeight - ScrollViewer.ActualHeight) / 2, null);
            await Restore();
        }

        private async void Manager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            var reference = RandomAccessStreamReference.CreateFromStream(await ConvertInkToPng());
            args.Request.Data.SetBitmap(reference);
            args.Request.Data.Properties.Title = "Created with Infinite Note";
            deferral.Complete();
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (Toolbar.ActiveTool == Toolbar.GetToolButton(InkToolbarTool.Eraser) || args.CurrentPoint.Properties.IsEraser)
            {
                Erase(args.CurrentPoint.Position.Translate(ScrollViewer));
            }
        }

        private void EnableScroll()
        {
            ScrollViewer.HorizontalScrollMode = ScrollMode.Auto;
            ScrollViewer.VerticalScrollMode = ScrollMode.Auto;
        }

        private void DisableScroll()
        {
            ScrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            ScrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            ScrollViewer.CancelDirectManipulations();
        }

        private InkStroke Translate(InkStroke inkStroke)
        {
            return Translate(inkStroke, ScrollViewer.HorizontalOffset, ScrollViewer.VerticalOffset);
        }

        private InkStroke Translate(InkStroke inkStroke, double left, double top)
        {
            var builder = new InkStrokeBuilder();
            var points = inkStroke.GetInkPoints()
                .Select(x => new InkPoint(x.Position.Translate(left, top), x.Pressure, x.TiltX, x.TiltY, x.Timestamp))
                .ToList();
            var newStroke = builder.CreateStrokeFromInkPoints(points, inkStroke.PointTransform, inkStroke.StrokeStartedTime, inkStroke.StrokeDuration);
            newStroke.DrawingAttributes = inkStroke.DrawingAttributes;
            return newStroke;
        }


        public void Erase(Point point)
        {
            const double toleranceWithZoom = 5.0;
            for (var i = _strokes.Count - 1; i >= 0; --i)
            {
                var stroke = _strokes[i];
                if (!stroke.BoundingRect.Contains(point)) continue;
                foreach (var inkPoint in stroke.GetInkPoints())
                {
                    if (!(Math.Abs(point.X - inkPoint.Position.X) < toleranceWithZoom) ||
                        !(Math.Abs(point.Y - inkPoint.Position.Y) < toleranceWithZoom)) continue;
                    _strokes.Remove(stroke);
                    Dry.Invalidate();
                    return;
                }
            }
        }

        private Task EraseAll()
        {
            _strokes.Clear();
            Dry.Invalidate();
            return Task.CompletedTask;
        }

        private Task Undo()
        {
            if (_undoBuffer.Count == 0) return Task.CompletedTask;
            var inkStroke = _undoBuffer.Pop();
            _redoBuffer.Push(inkStroke);
            _strokes.RemoveRange(inkStroke);
            Dry.Invalidate();
            return Task.CompletedTask;
        }

        private Task Redo()
        {
            if (_redoBuffer.Count == 0) return Task.CompletedTask;
            var inkStroke = _redoBuffer.Pop();
            _undoBuffer.Push(inkStroke);
            _strokes.AddRange(inkStroke);
            Dry.Invalidate();
            return Task.CompletedTask;
        }

        public async Task Save()
        {
            var fileSavePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = "Sketch",
            };
            fileSavePicker.FileTypeChoices.Add("PNG file", new List<string> { ".png" });
            var file = await fileSavePicker.PickSaveFileAsync();
            if (file != null)
            {
                var png = await ConvertInkToPng();
                CachedFileManager.DeferUpdates(file);
                var buffer = new Buffer((uint)png.Size);
                await png.ReadAsync(buffer, (uint)png.Size, InputStreamOptions.None);
                await FileIO.WriteBufferAsync(file, buffer);
                var status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status != FileUpdateStatus.Complete)
                {
                    // failed
                }
            }
        }

        public async Task Copy()
        {
            ((Storyboard)Resources["CopyAnimation"]).Begin();
            var package = new DataPackage();
            var reference = RandomAccessStreamReference.CreateFromStream(await ConvertInkToPng());
            package.SetBitmap(reference);
            Clipboard.Clear();
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }

        public Task Share()
        {
            ((Storyboard)Resources["CopyAnimation"]).Begin();
            DataTransferManager.ShowShareUI();
            return Task.CompletedTask;
        }

        public async Task Close()
        {
            await Suspend();
            Application.Current.Exit();
        }

        public async Task Restore()
        {
            if (_resumed) return;
            try
            {
                var storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync("stroke.json");
                var strokes =
                    JsonConvert.DeserializeObject<SerializableStroke[]>(await FileIO.ReadTextAsync(storageFile));
                if (strokes == null) return;
                _strokes.Clear();
                _strokes.AddRange(strokes.AsInkStrokes());
            }
            catch (ArgumentException)
            {
                return;
            }
            catch (JsonException)
            {
                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (NullReferenceException)
            {
                return;
            }
            _resumed = true;
        }

        public async Task Suspend()
        {
            var storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("stroke.json", CreationCollisionOption.ReplaceExisting);
            var strokes = JsonConvert.SerializeObject(_strokes.AsSerializable());
            await FileIO.WriteTextAsync(storageFile, strokes);
        }

        private Task<InMemoryRandomAccessStream> ConvertInkToPng()
        {
            var left = Math.Min(_strokes.Select(x => x.BoundingRect.Left).DefaultIfEmpty(CanvasWidth).Min(),
                ScrollViewer.HorizontalOffset);
            var top = Math.Min(_strokes.Select(x => x.BoundingRect.Top).DefaultIfEmpty(CanvasHeight).Min(),
                ScrollViewer.VerticalOffset);
            var right = _strokes.Select(x => x.BoundingRect.Right).DefaultIfEmpty(0).Max();
            var bottom = _strokes.Select(x => x.BoundingRect.Bottom).DefaultIfEmpty(0).Max();
            var width = (int)Math.Min(Math.Max(right - left, ActualWidth), CanvasWidth);
            var height = (int)Math.Min(Math.Max(bottom - top, ActualHeight), CanvasHeight);
            var device = CanvasDevice.GetSharedDevice();
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            var renderTarget = new CanvasRenderTarget(device, width, height, dpi);
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Colors.White);
                if (_strokes.Count > 0)
                {
                    ds.DrawInk(_strokes.Select(x => Translate(x, -left, -top)));
                }
            }
            var pixels = renderTarget.GetPixelBytes();
            return ConvertPixelsToPng(pixels, (int)renderTarget.SizeInPixels.Width, (int)renderTarget.SizeInPixels.Height);
        }

        private async Task<InMemoryRandomAccessStream> ConvertPixelsToPng(byte[] pixels, int width, int height)
        {
            var outputStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder
                .CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                (uint)width,
                (uint)height,
                dpi,
                dpi,
                pixels);
            await encoder.FlushAsync();
            outputStream.Seek(0);
            return outputStream;
        }
    }
}

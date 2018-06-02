using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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
        private const double DefaultCanvasWidth = 16384;
        private const double DefaultCanvasHeight = 16384;

        private bool _resumed;
        private readonly List<InkStroke> _strokes;
        private readonly Stack<IAction[]> _undoBuffer;
        private readonly Stack<IAction[]> _redoBuffer;
        private readonly InkSynchronizer _inkSynchronizer;

        private double CanvasWidth = DefaultCanvasWidth;
        private double CanvasHeight = DefaultCanvasHeight;

        public MainPage()
        {
            this.InitializeComponent();
            _strokes = new List<InkStroke>();
            _undoBuffer = new Stack<IAction[]>();
            _redoBuffer = new Stack<IAction[]>();
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
            _strokes.AddRange(inkStrokes);
            _inkSynchronizer.EndDry();
            // build undo buffer
            var left = inkStrokes.Select(x => x.BoundingRect.Left).Min();
            var right = inkStrokes.Select(x => x.BoundingRect.Right).Min();
            var top = inkStrokes.Select(x => x.BoundingRect.Top).Min();
            var bottom = inkStrokes.Select(x => x.BoundingRect.Bottom).Min();
            var extendsX = 0.0;
            var extendsY = 0.0;
            if (left < ActualWidth) extendsX = -ActualWidth;
            if (CanvasWidth - right < ActualWidth) extendsX = ActualWidth;
            if (top < ActualHeight) extendsY = -ActualHeight;
            if (CanvasHeight - bottom < ActualHeight) extendsY = ActualHeight;
            var actions = new List<IAction>();
            actions.Add(new StrokeAction
            {
                Type = ActionType.Draw,
                Strokes = inkStrokes,
            });
            if (extendsX != 0 || extendsY != 0)
            {
                actions.Add(new ResizeAction
                {
                    Type = ActionType.Resize,
                    ExtendsX = extendsX,
                    ExtendsY = extendsY,
                });
                ResizeCore(extendsX, extendsY, false);
            }
            PushUndo(actions.ToArray());
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
            ScrollViewer.ChangeView((CanvasWidth - ScrollViewer.ActualWidth) / 2, (CanvasHeight - ScrollViewer.ActualHeight) / 2, null, true);
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

        private void PushUndo(IAction[] actions)
        {
            _undoBuffer.Push(actions);
            _redoBuffer.Clear();
        }

        private void ResizeCore(double extendsX, double extendsY, bool undo)
        {
            var needReplace = false;
            var replaceX = 0.0;
            var replaceY = 0.0;
            if (!undo)
            {
                CanvasWidth += Math.Abs(extendsX);
                CanvasHeight += Math.Abs(extendsY);
            }
            else
            {
                CanvasWidth -= Math.Abs(extendsX);
                CanvasHeight -= Math.Abs(extendsY);
            }
            CanvasGrid.Width = CanvasWidth;
            CanvasGrid.Height = CanvasHeight;
            if (extendsX < 0)
            {
                if (undo) extendsX = -extendsX;
                ScrollViewer.ChangeView(ScrollViewer.HorizontalOffset - extendsX, null, null, true);
                needReplace = true;
                replaceX = -extendsX;
            }
            if (extendsY < 0)
            {
                if (undo) extendsY = -extendsY;
                ScrollViewer.ChangeView(null, ScrollViewer.VerticalOffset - extendsY, null, true);
                needReplace = true;
                replaceY = -extendsY;
            }
            if (needReplace)
            {
                foreach (var stroke in _strokes)
                {
                    stroke.PointTransform *= Matrix3x2.CreateTranslation((float)replaceX, (float)replaceY);
                }
            }
        }

        private Task EraseAll()
        {
            var actions = new List<IAction>();
            actions.Add(new StrokeAction
            {
                Type = ActionType.Erase,
                Strokes = _strokes.ToReadOnlyList(),
            });
            actions.Add(new DefaultSizeAction
            {
                Type = ActionType.DefaultSize,
                Viewport = new Rect(ScrollViewer.HorizontalOffset, ScrollViewer.VerticalOffset, CanvasWidth, CanvasHeight),
            });
            CanvasGrid.Width = CanvasWidth = DefaultCanvasWidth;
            CanvasGrid.Height = CanvasHeight = DefaultCanvasHeight;
            ScrollViewer.ChangeView((CanvasWidth - ActualWidth) / 2, (CanvasHeight - ActualHeight) / 2, null, true);
            PushUndo(actions.ToArray());
            _strokes.Clear();
            Dry.Invalidate();
            return Task.CompletedTask;
        }

        private Task Undo()
        {
            if (_undoBuffer.Count == 0) return Task.CompletedTask;
            var actions = _undoBuffer.Pop();
            _redoBuffer.Push(actions);
            foreach (var action in actions)
            {
                switch (action.Type)
                {
                    case ActionType.Draw:
                        _strokes.RemoveRange(((StrokeAction)action).Strokes);
                        break;
                    case ActionType.Erase:
                        _strokes.AddRange(((StrokeAction)action).Strokes);
                        break;
                    case ActionType.Resize:
                        ResizeCore(((ResizeAction)action).ExtendsX, ((ResizeAction)action).ExtendsY, true);
                        break;
                    case ActionType.DefaultSize:
                        CanvasGrid.Width = CanvasWidth = ((DefaultSizeAction)action).Viewport.Width;
                        CanvasGrid.Height = CanvasHeight = ((DefaultSizeAction)action).Viewport.Height;
                        ScrollViewer.ChangeView(((DefaultSizeAction)action).Viewport.Left, ((DefaultSizeAction)action).Viewport.Top, null, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            Dry.Invalidate();
            return Task.CompletedTask;
        }

        private Task Redo()
        {
            if (_redoBuffer.Count == 0) return Task.CompletedTask;
            var actions = _redoBuffer.Pop();
            _undoBuffer.Push(actions);
            foreach (var action in actions)
            {
                switch (action.Type)
                {
                    case ActionType.Draw:
                        _strokes.AddRange(((StrokeAction)action).Strokes);
                        break;
                    case ActionType.Erase:
                        _strokes.RemoveRange(((StrokeAction)action).Strokes);
                        break;
                    case ActionType.Resize:
                        ResizeCore(((ResizeAction)action).ExtendsX, ((ResizeAction)action).ExtendsY, false);
                        break;
                    case ActionType.DefaultSize:
                        CanvasGrid.Width = CanvasWidth = DefaultCanvasWidth;
                        CanvasGrid.Height = CanvasHeight = DefaultCanvasHeight;
                        ScrollViewer.ChangeView((CanvasWidth - ActualWidth) / 2, (CanvasHeight - ActualHeight) / 2, null, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
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
                var state = JsonConvert.DeserializeObject<ApplicationState>(await FileIO.ReadTextAsync(storageFile));
                if (state?.Strokes == null) return;
                _strokes.Clear();
                _strokes.AddRange(state.Strokes.AsInkStrokes());
                CanvasGrid.Width = CanvasWidth = state.Viewport.Width;
                CanvasGrid.Height = CanvasHeight = state.Viewport.Height;
                ScrollViewer.ChangeView(state.Viewport.Left, state.Viewport.Top, null, true);
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
            var state = JsonConvert.SerializeObject(new ApplicationState
            {
                Strokes = _strokes.AsSerializable().ToArray(),
                Viewport = new Rect(ScrollViewer.HorizontalOffset, ScrollViewer.VerticalOffset, CanvasWidth, CanvasHeight),
            });
            await FileIO.WriteTextAsync(storageFile, state);
        }

        private Task<InMemoryRandomAccessStream> ConvertInkToPng()
        {
            var left = ScrollViewer.HorizontalOffset;
            var top = ScrollViewer.VerticalOffset;
            var width = (int)ActualWidth;
            var height = (int)ActualHeight;
            var rect = new Rect(left, top, width, height);
            var device = CanvasDevice.GetSharedDevice();
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            var renderTarget = new CanvasRenderTarget(device, width, height, dpi);
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Colors.White);
                var inkStrokes = _strokes
                    .Where(x => x.BoundingRect.IsIntersect(rect))
                    .Select(x => Translate(x, -left, -top))
                    .ToList();
                if (inkStrokes.Count > 0) ds.DrawInk(inkStrokes);
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

    public class ApplicationState
    {
        public SerializableStroke[] Strokes { get; set; }
        public Rect Viewport { get; set; }
    }

    public interface IAction
    {
        ActionType Type { get; set; }
    }

    public class StrokeAction : IAction
    {
        public ActionType Type { get; set; }
        public IReadOnlyList<InkStroke> Strokes { get; set; }
    }

    public class ResizeAction : IAction
    {
        public ActionType Type { get; set; }
        public double ExtendsX { get; set; }
        public double ExtendsY { get; set; }
    }

    public class DefaultSizeAction : IAction
    {
        public ActionType Type { get; set; }
        public Rect Viewport { get; set; }
    }

    public enum ActionType
    {
        Draw,
        Erase,
        Resize,
        DefaultSize,
    }
}

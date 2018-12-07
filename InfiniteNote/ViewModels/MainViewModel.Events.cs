using InfiniteNote.Extensions;
using InfiniteNote.Infrastructure;
using InfiniteNote.Views.Messages;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Buffer = Windows.Storage.Streams.Buffer;

namespace InfiniteNote.ViewModels
{
    public partial class MainViewModel
    {
        private InkDrawingAttributes _previousDefaultDrawingAttributes;
        private Point _manipulationPoint;

        public void RegionInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            foreach (var rect in args.InvalidatedRegions)
            {
                RenderPart(sender, rect);
            }
        }

        public void EraseAll()
        {
            _drawingData.Reset();
            Messenger.Default.Send<InvalidateRequestedMessage>();
        }

        public void Undo()
        {
            _drawingData.Undo();
            Messenger.Default.Send<InvalidateRequestedMessage>();
        }

        public void Redo()
        {
            _drawingData.Redo();
            Messenger.Default.Send<InvalidateRequestedMessage>();
        }

        public async void Save()
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
                var png = await GeneratePng();
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

        public async void Copy()
        {
            Messenger.Default.Send<StartAnimationMessage>();
            var package = new DataPackage();
            var reference = RandomAccessStreamReference.CreateFromStream(await GeneratePng());
            package.SetBitmap(reference);
            Clipboard.Clear();
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }

        public void Share()
        {
            Messenger.Default.Send<StartAnimationMessage>();
            DataTransferManager.ShowShareUI();
        }

        public async void Close()
        {
            await SuspendCore();
            Application.Current.Exit();
        }

        #region Lifecycle Events

        public async void Resume(object sender, LeavingBackgroundEventArgs e)
        {
            using (var deferral = e.GetDeferral())
            {
                await ResumeCore();
            }
        }

        public async void Suspend(object sender, EnteredBackgroundEventArgs e)
        {
            using (var deferral = e.GetDeferral())
            {
                await SuspendCore();
            }
        }

        #endregion

        #region InkCanvas Events

        public void StrokeStarted(object sender, EventArgs e)
        {
            IsScrollEnabled.Value = false;
        }

        public void StrokeEnded(object sender, EventArgs e)
        {
            IsScrollEnabled.Value = true;
        }

        public void PointerMoved(object sender, PointerPoint e)
        {
            if (e.Properties.IsEraser || ActiveTool.Value == InkToolbarTool.Eraser)
            {
                var point = e.Position.Translate(ViewportOffsetX.Value, ViewportOffsetY.Value).Scale(1 / Scale.Value);
                if (EraseStrokeFromPoint(point))
                {
                    Messenger.Default.Send<InvalidateRequestedMessage>();
                }
            }
        }

        public void StrokeCollected(object sender, IReadOnlyList<InkStroke> e)
        {
            DrawStrokes(e);
            Messenger.Default.Send<InvalidateRequestedMessage>();
        }

        #endregion

        #region Manipulation Events

        public void ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            ((UIElement)e.OriginalSource).CancelDirectManipulations();
            e.Handled = true;
            _manipulationPoint = e.Position;
        }

        public void ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            ((UIElement)e.OriginalSource).CancelDirectManipulations();
            e.Handled = true;
            var newScale = Math.Min(8, Math.Max(0.25, Scale.Value * e.Delta.Scale));
            var step = newScale / Scale.Value;
            var center = _manipulationPoint.Translate(ViewportOffsetX.Value, ViewportOffsetY.Value);
            ViewportOffsetX.Value += center.X * step - center.X;
            ViewportOffsetY.Value += center.Y * step - center.Y;
            Scale.Value = newScale;
            Messenger.Default.Send<InvalidateRequestedMessage>();
        }

        #endregion
    }
}

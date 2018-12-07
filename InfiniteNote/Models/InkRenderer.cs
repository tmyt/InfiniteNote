using InfiniteNote.Extensions;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace InfiniteNote.Models
{
    public class InkRenderer
    {
        public void Render(CanvasDrawingSession session, IEnumerable<InkStroke> strokes)
        {
            session.Clear(Colors.White);
            session.DrawInk(strokes, false);
        }

        public Task<InMemoryRandomAccessStream> ConvertInkToPng(Rect viewport, double scale, IEnumerable<InkStroke> strokes)
        {
            var device = CanvasDevice.GetSharedDevice();
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi;
            var scaledViewport = viewport.Scale(scale);
            var renderTarget = new CanvasRenderTarget(device, (float)scaledViewport.Width, (float)scaledViewport.Height, dpi);
            using (var ds = renderTarget.CreateDrawingSession())
            {
                var inkStrokes = strokes
                    .Where(x => x.BoundingRect.IsIntersect(viewport))
                    .Select(x =>
                    {
                        var stroke = x.Translate(-viewport.Left, -viewport.Top);
                        stroke.PointTransform *= Matrix3x2.CreateScale((float)scale);
                        var da = stroke.DrawingAttributes;
                        da.Size = da.Size.Scale(scale);
                        stroke.DrawingAttributes = da;
                        return stroke;
                    });
                Render(ds, inkStrokes);
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
                (uint)width, (uint)height, dpi, dpi, pixels);
            await encoder.FlushAsync();
            outputStream.Seek(0);
            return outputStream;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Input.Inking;
using Windows.UI.ViewManagement;
using InfiniteNote.Annotations;
using InfiniteNote.Extensions;
using Newtonsoft.Json;

namespace InfiniteNote.Models
{
    public class DrawingData : INotifyPropertyChanged
    {
        private const string StateFile = "stroke.json";

        public static readonly double DefaultCanvasWidth = 16384;
        public static readonly double DefaultCanvasHeight = 16384;

        private readonly List<InkStroke> _strokes;
        private readonly Stack<IAction[]> _undoBuffer;
        private readonly Stack<IAction[]> _redoBuffer;
        private double _canvasWidth;
        private double _canvasHeight;
        private double _viewWidth;
        private double _viewHeight;
        private double _offsetX;
        private double _offsetY;

        public double CanvasWidth
        {
            get => _canvasWidth;
            private set
            {
                if (value.Equals(_canvasWidth)) return;
                _canvasWidth = value;
                OnPropertyChanged();
            }
        }

        public double CanvasHeight
        {
            get => _canvasHeight;
            private set
            {
                if (value.Equals(_canvasHeight)) return;
                _canvasHeight = value;
                OnPropertyChanged();
            }
        }

        public double ViewWidth
        {
            get => _viewWidth;
            set
            {
                if (value.Equals(_viewWidth)) return;
                _viewWidth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Viewport));
            }
        }

        public double ViewHeight
        {
            get => _viewHeight;
            set
            {
                if (value.Equals(_viewHeight)) return;
                _viewHeight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Viewport));
            }
        }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (value.Equals(_offsetX)) return;
                _offsetX = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Viewport));
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (value.Equals(_offsetY)) return;
                _offsetY = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Viewport));
            }
        }

        public Rect Viewport => new Rect(OffsetX, OffsetY, ViewWidth, ViewHeight);
        public IReadOnlyList<InkStroke> Strokes => _strokes;

        public DrawingData()
        {
            _strokes = new List<InkStroke>();
            _undoBuffer = new Stack<IAction[]>();
            _redoBuffer = new Stack<IAction[]>();
            CanvasWidth = DefaultCanvasWidth;
            CanvasHeight = DefaultCanvasHeight;
        }

        private void PushUndo(params IAction[] actions)
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
            if (extendsX < 0)
            {
                if (undo) extendsX = -extendsX;
                OffsetX -= extendsX;
                needReplace = true;
                replaceX = -extendsX;
            }
            if (extendsY < 0)
            {
                if (undo) extendsY = -extendsY;
                OffsetY -= extendsY;
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

        public void DrawStrokes(IEnumerable<InkStroke> strokes)
        {
            var strokeList = strokes.ToReadOnlyList();
            _strokes.AddRange(strokeList);
            // build undo buffer
            var strokeBounds = strokeList.Aggregate(Rect.Empty, (rect, stroke) =>
            {
                rect.Union(stroke.BoundingRect);
                return rect;
            });
            var visibleBounds = ApplicationView.GetForCurrentView().VisibleBounds;
            var extendsX = 0.0;
            var extendsY = 0.0;
            if (strokeBounds.Left < visibleBounds.Width) extendsX = -visibleBounds.Width;
            if (CanvasWidth - strokeBounds.Right < visibleBounds.Width) extendsX = visibleBounds.Width;
            if (strokeBounds.Top < visibleBounds.Height) extendsY = -visibleBounds.Height;
            if (CanvasHeight - strokeBounds.Bottom < visibleBounds.Height) extendsY = visibleBounds.Height;
            var actions = new List<IAction>
            {
                new StrokeAction
                {
                    Type = ActionType.Draw,
                    Strokes = strokeList,
                }
            };
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
        }

        public void EraseStroke(InkStroke stroke)
        {
            PushUndo(new StrokeAction
            {
                Type = ActionType.Erase,
                Strokes = new[] { stroke },
            });
            _strokes.Remove(stroke);
        }

        public void Reset()
        {
            PushUndo(new StrokeAction
            {
                Type = ActionType.Erase,
                Strokes = _strokes.ToReadOnlyList(),
            }, new DefaultSizeAction
            {
                Type = ActionType.DefaultSize,
                Viewport = new Rect(OffsetX, OffsetY, CanvasWidth, CanvasHeight),
            });
            CanvasWidth = DefaultCanvasWidth;
            CanvasHeight = DefaultCanvasHeight;
            OffsetX = (CanvasWidth - ViewWidth) / 2;
            OffsetY = (CanvasHeight - ViewHeight) / 2;
            _strokes.Clear();
        }

        public void Undo()
        {
            if (_undoBuffer.Count == 0) return;
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
                        CanvasWidth = ((DefaultSizeAction)action).Viewport.Width;
                        CanvasHeight = ((DefaultSizeAction)action).Viewport.Height;
                        OffsetX = ((DefaultSizeAction)action).Viewport.Left;
                        OffsetY = ((DefaultSizeAction)action).Viewport.Top;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Redo()
        {
            if (_redoBuffer.Count == 0) return;
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
                        CanvasWidth = DefaultCanvasWidth;
                        CanvasHeight = DefaultCanvasHeight;
                        OffsetX = (CanvasWidth - ViewWidth) / 2;
                        OffsetY = (CanvasHeight - ViewHeight) / 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public async Task<bool> RestoreState()
        {
            try
            {
                var storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(StateFile);
                var state = JsonConvert.DeserializeObject<ApplicationState>(await FileIO.ReadTextAsync(storageFile));
                if (state?.Strokes == null) return false;
                _strokes.Clear();
                _strokes.AddRange(state.Strokes.AsInkStrokes());
                CanvasWidth = state.Viewport.Width;
                CanvasHeight = state.Viewport.Height;
                OffsetX = state.Viewport.Left;
                OffsetY = state.Viewport.Top;
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (JsonException)
            {
            }
            catch (FileNotFoundException)
            {
            }
            catch (NullReferenceException)
            {
            }

            return false;
        }

        public async Task SaveState()
        {
            var storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(StateFile, CreationCollisionOption.ReplaceExisting);
            var state = JsonConvert.SerializeObject(new ApplicationState
            {
                Strokes = _strokes.AsSerializable().ToArray(),
                Viewport = new Rect(OffsetX, OffsetY, CanvasWidth, CanvasHeight),
            });
            await FileIO.WriteTextAsync(storageFile, state);
        }

        public InkStroke FindNearestStroke(Point pt)
        {
            const double toleranceWithZoom = 5.0;
            for (var i = _strokes.Count - 1; i >= 0; --i)
            {
                var stroke = _strokes[i];
                if (!stroke.BoundingRect.Contains(pt)) continue;
                foreach (var inkPoint in stroke.GetInkPoints())
                {
                    if (Math.Abs(pt.X - inkPoint.Position.X) < toleranceWithZoom &&
                        Math.Abs(pt.Y - inkPoint.Position.Y) < toleranceWithZoom)
                    {
                        return stroke;
                    }
                }
            }

            return null;
        }

        public void MoveToCenter()
        {
            OffsetX = (CanvasHeight - ViewWidth) / 2;
            OffsetY = (CanvasHeight - ViewHeight) / 2;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace InfiniteNote.Views.Behaviors
{
    public class InkCanvasBinding : DependencyObject, IBehavior
    {
        public static readonly DependencyProperty IsTouchInputEnabledProperty = DependencyProperty.RegisterAttached(
            "IsTouchInputEnabled", typeof(bool), typeof(InkCanvasBinding), new PropertyMetadata(default(bool), IsTouchInputEnabledChanged));

        public static void SetIsTouchInputEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsTouchInputEnabledProperty, value);
        }

        public static bool GetIsTouchInputEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsTouchInputEnabledProperty);
        }

        private static void IsTouchInputEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var presenter = ((InkCanvas)d).InkPresenter;
            if ((bool)e.NewValue)
            {
                presenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch;
            }
            else
            {
                presenter.InputDeviceTypes = CoreInputDeviceTypes.Pen;
            }
        }

        public event EventHandler StrokeStarted;
        public event EventHandler StrokeEnded;
        public event EventHandler<PointerPoint> PointerMoved;
        public event EventHandler<IReadOnlyList<InkStroke>> StrokeCollected;

        private InkSynchronizer _inkSynchronizer;

        public void Attach(DependencyObject associatedObject)
        {
            var canvas = (InkCanvas)(AssociatedObject = associatedObject);
            _inkSynchronizer = canvas.InkPresenter.ActivateCustomDrying();
            canvas.InkPresenter.StrokesCollected += OnStrokeCollected; ;
            canvas.InkPresenter.UnprocessedInput.PointerMoved += OnPointerMoved;
            canvas.InkPresenter.StrokeInput.StrokeStarted += OnStrokeStarted;
            canvas.InkPresenter.StrokeInput.StrokeEnded += OnStrokeEnded;
            canvas.InkPresenter.StrokeInput.StrokeCanceled += OnStrokeCanceled;
        }

        public void Detach()
        {
            var canvas = (InkCanvas)AssociatedObject;
            canvas.InkPresenter.StrokesCollected -= OnStrokeCollected; ;
            canvas.InkPresenter.UnprocessedInput.PointerMoved -= OnPointerMoved;
            canvas.InkPresenter.StrokeInput.StrokeStarted -= OnStrokeStarted;
            canvas.InkPresenter.StrokeInput.StrokeEnded -= OnStrokeEnded;
            canvas.InkPresenter.StrokeInput.StrokeCanceled -= OnStrokeCanceled;
        }

        public DependencyObject AssociatedObject { get; private set; }

        private void OnStrokeCanceled(InkStrokeInput sender, PointerEventArgs args)
        {
            StrokeEnded?.Invoke(sender, EventArgs.Empty);
        }

        private void OnStrokeEnded(InkStrokeInput sender, PointerEventArgs args)
        {
            StrokeEnded?.Invoke(sender, EventArgs.Empty);
        }

        private void OnStrokeStarted(InkStrokeInput sender, PointerEventArgs args)
        {
            StrokeStarted?.Invoke(sender, EventArgs.Empty);
        }

        private void OnPointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            PointerMoved?.Invoke(sender, args.CurrentPoint);
        }

        private void OnStrokeCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            var list = _inkSynchronizer.BeginDry();
            StrokeCollected?.Invoke(this, list);
            _inkSynchronizer.EndDry();
        }
    }
}

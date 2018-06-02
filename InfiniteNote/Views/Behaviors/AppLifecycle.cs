using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Behaviors
{
    public class AppLifecycle : DependencyObject, IBehavior
    {
        public event EventHandler<EnteredBackgroundEventArgs> Suspending;
        public event EventHandler<LeavingBackgroundEventArgs> Resuming;

        public void Attach(DependencyObject associatedObject)
        {
            Application.Current.EnteredBackground += Current_EnteredBackground;
            Application.Current.LeavingBackground += Current_LeavingBackground;
        }

        public void Detach()
        {
            Application.Current.EnteredBackground -= Current_EnteredBackground;
            Application.Current.LeavingBackground -= Current_LeavingBackground;
        }

        private void Current_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            Resuming?.Invoke(this, e);
        }

        private void Current_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            Suspending?.Invoke(this, e);
        }

        public DependencyObject AssociatedObject { get; }
    }
}

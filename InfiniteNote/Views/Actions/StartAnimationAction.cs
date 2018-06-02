using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Views.Actions
{
    public  class StartAnimationAction:DependencyObject,IAction
    {
        public Storyboard Storyboard { get; set; }

        public object Execute(object sender, object parameter)
        {
            Storyboard.Begin();
            return null;
        }
    }
}

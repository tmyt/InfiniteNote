using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;
using Microsoft.Xaml.Interactivity;

namespace InfiniteNote.Infrastructure
{
    [ContentProperty(Name = "Actions")]
    public class Message<T> : DependencyObject, IBehavior
    {
        public List<IAction> Actions { get; set; } = new List<IAction>();

        public void Attach(DependencyObject associatedObject)
        {
            AssociatedObject = associatedObject;
            Messenger.Default.Subscribe(this);
        }

        public void Detach()
        {
            Messenger.Default.Unsubscribe(this);
            AssociatedObject = null;
        }

        public void Call(object arg)
        {
            foreach (var action in Actions)
            {
                action.Execute(AssociatedObject, arg);
            }
        }

        public DependencyObject AssociatedObject { get; private set; }
    }
}

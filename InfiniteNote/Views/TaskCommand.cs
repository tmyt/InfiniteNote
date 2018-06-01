using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InfiniteNote.Views
{
    public class TaskCommand : ICommand
    {
        private readonly Func<Task> _action;

        public TaskCommand(Func<Task> action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _action();
        }

        public event EventHandler CanExecuteChanged;
    }
}
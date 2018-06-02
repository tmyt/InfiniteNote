using System.Collections.Generic;
using System.Linq;

namespace InfiniteNote.Infrastructure
{
    public class Messenger
    {
        private static Messenger _default;
        public static Messenger Default => _default ?? (_default = new Messenger());

        private readonly List<object> _receivers = new List<object>();

        public void Send<T>(object arg)
            where T : Message<T>
        {
            foreach (var o in _receivers.Where(x => x.GetType() == typeof(T)))
            {
                ((T)o).Call(arg);
            }
        }

        public void Send<T>()
            where T : Message<T>
        {
            Send<T>(null);
        }

        public void Subscribe<T>(Message<T> that)
        {
            _receivers.Add(that);
        }

        public void Unsubscribe<T>(Message<T> that)
        {
            _receivers.Remove(that);
        }
    }
}

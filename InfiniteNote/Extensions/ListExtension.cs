using System.Collections.Generic;

namespace InfiniteNote.Extensions
{
    public static class ListExtension
    {
        public static void RemoveRange<T>(this IList<T> source, IEnumerable<T> items)
        {
            foreach (var i in items)
            {
                source.Remove(i);
            }
        }
    }
}
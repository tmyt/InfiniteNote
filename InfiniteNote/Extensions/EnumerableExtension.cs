using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace InfiniteNote.Extensions
{
    public static class EnumerableExtension
    {
        public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(source.ToList());
        }
    }
}
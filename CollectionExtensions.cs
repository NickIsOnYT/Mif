using System.Collections.ObjectModel;

namespace Mif;

public static class CollectionExtensions
{
    public static void Move<T>(this ObservableCollection<T> collection, int oldIndex, int newIndex)
    {
        var item = collection[oldIndex];
        collection.RemoveAt(oldIndex);
        collection.Insert(newIndex, item);
    }
}

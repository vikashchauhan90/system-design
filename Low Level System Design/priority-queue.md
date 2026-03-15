# Priority Queue

```C#
public class PriorityQueue<T>
{
    private readonly SortedDictionary<int, Queue<T>> queue = [];

    public void Enqueue(int priority, T item)
    {
        if (!queue.ContainsKey(priority))
        {
            queue.Add(priority, new Queue<T>());
        }
        queue[priority].Enqueue(item);
    }

    public T Dequeue()
    {
        if (queue.Count == 0)
        {
            return default!;
        }

        var topPriority = GetHightPriorty();

        var subqueue = queue[topPriority];

        var item = subqueue.Dequeue();

        if (subqueue.Count == 0)
        {
            queue.Remove(topPriority);
        }

        return item;
    }


    public int Count()
    {
        int count = 0;
        foreach (var item in queue)
        {
            count += item.Value.Count;

        }

        return count;
    }

    public bool IsEmpty => queue.Count == 0;

    private int GetHightPriorty()
    {
        var enurmator = queue.GetEnumerator();
        enurmator.MoveNext();
        return enurmator.Current.Key;
    }
}
```
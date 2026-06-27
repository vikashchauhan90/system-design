using System;
using System.Collections.Generic;

namespace DistributedSystem.PriorityQueue;

public sealed class PriorityQueue<T>
{
    private readonly List<(T Item, int Priority)> _heap = [];
    private readonly IComparer<int> _priorityComparer;

    public PriorityQueue(IComparer<int>? priorityComparer = null)
    {
        _priorityComparer = priorityComparer ?? Comparer<int>.Default;
    }

    public int Count => _heap.Count;

    public void Enqueue(T item, int priority)
    {
        _heap.Add((item, priority));
        BubbleUp(_heap.Count - 1);
    }

    public T Dequeue()
    {
        if (_heap.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        var first = _heap[0];
        var last = _heap[^1];
        _heap[0] = last;
        _heap.RemoveAt(_heap.Count - 1);

        if (_heap.Count > 0)
        {
            BubbleDown(0);
        }

        return first.Item;
    }

    public T Peek()
    {
        if (_heap.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        return _heap[0].Item;
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            var parentIndex = (index - 1) / 2;
            if (_priorityComparer.Compare(_heap[index].Priority, _heap[parentIndex].Priority) >= 0)
            {
                break;
            }

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    private void BubbleDown(int index)
    {
        while (true)
        {
            var smallestChildIndex = -1;
            var leftChildIndex = 2 * index + 1;
            var rightChildIndex = 2 * index + 2;

            if (leftChildIndex < _heap.Count)
            {
                smallestChildIndex = leftChildIndex;
            }

            if (rightChildIndex < _heap.Count &&
                _priorityComparer.Compare(_heap[rightChildIndex].Priority, _heap[leftChildIndex].Priority) < 0)
            {
                smallestChildIndex = rightChildIndex;
            }

            if (smallestChildIndex == -1 || _priorityComparer.Compare(_heap[index].Priority, _heap[smallestChildIndex].Priority) <= 0)
            {
                break;
            }

            Swap(index, smallestChildIndex);
            index = smallestChildIndex;
        }
    }

    private void Swap(int left, int right)
    {
        (_heap[left], _heap[right]) = (_heap[right], _heap[left]);
    }
}

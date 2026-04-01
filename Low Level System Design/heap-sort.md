# Heap Sort

Heap sort is a sorting algorithm that works by first organizing the data to be sorted into a special type of binary tree called a heap. The heap itself has the property that every node in the heap is either greater than or equal to (in a max heap) or less than or equal to (in a min heap) its two children. The heap sort algorithm then repeatedly removes the largest/smallest element from the heap, thus building the sorted list from back to front. Heap sort provides a good compromise of efficiency and simplicity. It has an average and worst-case time complexity of `O(n log n)`.

First transforms the input array into a max heap. Then, it repeatedly swaps the first element (the maximum) with the last element of the heap, reduces the size of the heap by one, and heapifies the root element. This process continues until the heap is empty, resulting in a sorted array in ascending order.

```C#
using System;

public class HeapSort
{
    /// <summary>
    /// Sorts array in ascending order using Max Heap
    /// Time Complexity: O(n log n) - Building heap O(n) + n heapify operations O(log n) each
    /// Space Complexity: O(1) - In-place sorting, iterative approach avoids recursion stack
    /// </summary>
    public void SortAscending(int[] array)
    {
        if (array == null || array.Length <= 1)
            return;

        int n = array.Length;

        // Step 1: Build max heap
        // Start from last non-leaf node and heapify each node
        for (int i = n / 2 - 1; i >= 0; i--)
        {
            Heapify(array, n, i);
        }

        // Step 2: Extract elements from heap one by one
        for (int i = n - 1; i > 0; i--)
        {
            // Move current root (largest) to end
            Swap(array, 0, i);
            
            // Call heapify on reduced heap
            Heapify(array, i, 0);
        }
    }

    /// <summary>
    /// Sorts array in descending order using Min Heap
    /// Time Complexity: O(n log n)
    /// Space Complexity: O(1)
    /// </summary>
    public void SortDescending(int[] array)
    {
        if (array == null || array.Length <= 1)
            return;

        int n = array.Length;

        // Build min heap
        for (int i = n / 2 - 1; i >= 0; i--)
        {
            HeapifyMin(array, n, i);
        }

        // Extract elements
        for (int i = n - 1; i > 0; i--)
        {
            Swap(array, 0, i);
            HeapifyMin(array, i, 0);
        }
    }

    /// <summary>
    /// Iterative heapify for max heap - avoids recursion stack overhead
    /// </summary>
    private void Heapify(int[] array, int heapSize, int rootIndex)
    {
        while (true)
        {
            int largest = rootIndex;
            int leftChild = 2 * rootIndex + 1;
            int rightChild = 2 * rootIndex + 2;

            // Find largest among root, left child, and right child
            if (leftChild < heapSize && array[leftChild] > array[largest])
                largest = leftChild;

            if (rightChild < heapSize && array[rightChild] > array[largest])
                largest = rightChild;

            // If root is largest, heap property satisfied
            if (largest == rootIndex)
                break;

            // Swap root with largest child
            Swap(array, rootIndex, largest);
            
            // Continue heapifying at the affected child
            rootIndex = largest;
        }
    }

    /// <summary>
    /// Iterative heapify for min heap
    /// </summary>
    private void HeapifyMin(int[] array, int heapSize, int rootIndex)
    {
        while (true)
        {
            int smallest = rootIndex;
            int leftChild = 2 * rootIndex + 1;
            int rightChild = 2 * rootIndex + 2;

            if (leftChild < heapSize && array[leftChild] < array[smallest])
                smallest = leftChild;

            if (rightChild < heapSize && array[rightChild] < array[smallest])
                smallest = rightChild;

            if (smallest == rootIndex)
                break;

            Swap(array, rootIndex, smallest);
            rootIndex = smallest;
        }
    }

    /// <summary>
    /// Swaps two elements in array
    /// </summary>
    private void Swap(int[] array, int i, int j)
    {
        if (i == j) return;
        
        int temp = array[i];
        array[i] = array[j];
        array[j] = temp;
    }


```
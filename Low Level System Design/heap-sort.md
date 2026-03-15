# Heap Sort

Heap sort is a sorting algorithm that works by first organizing the data to be sorted into a special type of binary tree called a heap. The heap itself has the property that every node in the heap is either greater than or equal to (in a max heap) or less than or equal to (in a min heap) its two children. The heap sort algorithm then repeatedly removes the largest/smallest element from the heap, thus building the sorted list from back to front. Heap sort provides a good compromise of efficiency and simplicity. It has an average and worst-case time complexity of `O(n log n)`.

First transforms the input array into a max heap. Then, it repeatedly swaps the first element (the maximum) with the last element of the heap, reduces the size of the heap by one, and heapifies the root element. This process continues until the heap is empty, resulting in a sorted array in ascending order.

```C#
public class HeapSort
{
    //Time complexity:It takes O(logn) for heapify and O(n) for constructing a heap. Hence, the overall time complexity of heap sort using min heap or max heap is O(n log n)
    //Space complexity: O(n) for call stack
    //Auxiliary Space complexity: O(1)  for swap two items

    public void Accending(int[] nums)
    {
        int N = nums.Length;
        for (int i = N / 2 - 1; i >= 0; i--)
        {
            MaxHeap(nums, N, i);
        }

        for (int j = N - 1; j >= 0; j--)
        {
            int temp = nums[0];
            nums[0] = nums[j];
            nums[j] = temp;
            MaxHeap(nums, j, 0);
        }
    }
    public void Decreasing(int[] nums)
    {
        int N = nums.Length;
        for (int i = N / 2 - 1; i >= 0; i--)
        {
            MinHeap(nums, N, i);
        }

        for (int j = N - 1; j >= 0; j--)
        {
            int temp = nums[0];
            nums[0] = nums[j];
            nums[j] = temp;
            MinHeap(nums, j, 0);
        }
    }

    private void MaxHeap(int[] nums, int N, int index)
    {
        int largest = index;
        int left = 2 * index + 1;
        int right = 2 * index + 2;

        if (left < N && nums[left] > nums[largest])
        {
            largest = left;
        }
        if (right < N && nums[right] > nums[largest])
        {
            largest = right;
        }

        if (largest != index)
        {
            int temp = nums[index];
            nums[index] = nums[largest];
            nums[largest] = temp;

            MaxHeap(nums, N, largest);
        }
    }

    private void MinHeap(int[] nums, int N, int index)
    {
        int smallest = index;
        int left = 2 * index + 1;
        int right = 2 * index + 2;

        if (left < N && nums[left] < nums[smallest])
        {
            smallest = left;
        }
        if (right < N && nums[right] < nums[smallest])
        {
            smallest = right;
        }

        if (smallest != index)
        {
            int temp = nums[index];
            nums[index] = nums[smallest];
            nums[smallest] = temp;

            MinHeap(nums, N, smallest);
        }
    }
}
```
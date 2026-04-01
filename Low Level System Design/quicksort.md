# QuickSort

```C#
public class QuickSort
{

    public void Sort(int[] arr)
    {
        if (arr == null || arr.Length <= 1)
            return;

        QuickSortHelper(arr, 0, arr.Length - 1);
    }

    private void QuickSortHelper(int[] arr, int low, int high)
    {
        if (low < high)
        {
            int pi = Partition(arr, low, high); // Partitioning index
            QuickSortHelper(arr, low, pi - 1); // Recursively sort elements before partition
            QuickSortHelper(arr, pi + 1, high); // Recursively sort elements after partition
        }
    }
    private int Partition(int[] arr, int lowIndex, int highIndex)
    {
        int pivot = arr[highIndex]; // Choosing the last element as pivot
        int i = lowIndex - 1; // Index of smaller element
        for (int j = lowIndex; j < highIndex; j++)
        {
            if (arr[j] < pivot) // If current element is smaller than pivot
            {
                i++; // Increment index of smaller element
                Swap(arr, i, j); // Swap elements at index i and j
            }
        }
        Swap(arr, i + 1, highIndex); // Swap the pivot element with the element at index i + 1
        return i + 1; // Return the partitioning index
    }

    private void Swap(int[] arr, int i, int j)
    {
        int temp = arr[i];
        arr[i] = arr[j];
        arr[j] = temp;
    }
}
```
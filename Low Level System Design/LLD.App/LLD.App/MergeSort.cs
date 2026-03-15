namespace LLD.App;

public class MergeSort
{

    public void Sort(int[] arr)
    {
        if (arr == null || arr.Length <= 1)
            return;

        int mid = arr.Length / 2;
        int[] left = new int[mid];
        int[] right = new int[arr.Length - mid];
        Array.Copy(arr, 0, left, 0, mid); // Copy first half to left
        Array.Copy(arr, mid, right, 0, arr.Length - mid); // Copy second half to right
        Sort(left); // Recursively sort left half
        Sort(right); // Recursively sort right half
        MergeSortedArrays(arr, left, right); // Merge sorted halves back into original array
    }

    private void MergeSortedArrays(int[] target, int[] left, int[] right)
    {
        int i = 0, j = 0, k = 0;
        while (i < left.Length && j < right.Length)
        {
            if (left[i] <= right[j])
            {
                target[k++] = left[i++];
            }
            else
            {
                target[k++] = right[j++];
            }
        }
        while (i < left.Length)
        {
            target[k++] = left[i++];
        }
        while (j < right.Length)
        {
            target[k++] = right[j++];
        }
    }
}

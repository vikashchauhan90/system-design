# Sort an Almost Sorted Array (K-Sorted Array)

**Description:**
Given an array where each element is at most **K positions away** from its correct sorted position, sort the array efficiently.

Such an array is called a **K-sorted array** (or nearly sorted array).

---

## Examples:

```
Input:
arr = [6, 5, 3, 2, 8, 10, 9]
k = 3

Output:
[2, 3, 5, 6, 8, 9, 10]
```

```
Input:
arr = [10, 9, 8, 7, 4, 70, 60, 50]
k = 4

Output:
[4, 7, 8, 9, 10, 50, 60, 70]
```

```
Input:
arr = [1, 2, 3, 4]
k = 1

Output:
[1, 2, 3, 4]
```

---

## Constraints

* `1 ≤ n ≤ 10^5`
* `0 ≤ k < n`
* Elements are integers
* Try to do better than `O(n log n)`
* In-place preferred

---

# ✅ Approach 1: Using Min Heap (Optimal & Most Common)

### 🔥 Key Idea

In a K-sorted array:

* The smallest element among the first `k+1` elements must be the first element in the sorted array.
* Use a **Min Heap** of size `k+1`.

---

## Algorithm Steps

1. Insert first `k+1` elements into a min heap.
2. Extract min and place into result.
3. Add next element into heap.
4. Continue until array is sorted.

---

## Pseudocode

```
FUNCTION SortKSortedArray(arr, k):

    n = length of arr
    CREATE minHeap

    // Step 1: Add first k+1 elements
    FOR i = 0 TO k:
        minHeap.add(arr[i])

    index = 0

    // Step 2: Process remaining elements
    FOR i = k+1 TO n-1:
        arr[index] = minHeap.removeMin()
        index++
        minHeap.add(arr[i])

    // Step 3: Remove remaining elements
    WHILE minHeap is not empty:
        arr[index] = minHeap.removeMin()
        index++

    RETURN arr
END FUNCTION
```

---

## Code Implementation (C#)

```csharp
using System;
using System.Collections.Generic;

public static int[] SortKSortedArray(int[] arr, int k)
{
    int n = arr.Length;
    PriorityQueue<int, int> minHeap = new PriorityQueue<int, int>();

    // Step 1: Add first k+1 elements
    for (int i = 0; i <= k && i < n; i++)
    {
        minHeap.Enqueue(arr[i], arr[i]);
    }

    int index = 0;

    // Step 2: Process rest
    for (int i = k + 1; i < n; i++)
    {
        arr[index++] = minHeap.Dequeue();
        minHeap.Enqueue(arr[i], arr[i]);
    }

    // Step 3: Empty remaining heap
    while (minHeap.Count > 0)
    {
        arr[index++] = minHeap.Dequeue();
    }

    return arr;
}
```

---

## Visualization

```
Input: [6, 5, 3, 2, 8, 10, 9]
k = 3
```

Initial heap (first k+1 = 4 elements):

```
[6, 5, 3, 2]
MinHeap → 2
```

Steps:

```
Remove 2 → place at index 0
Add 8

Remove 3 → place at index 1
Add 10

Remove 5 → place at index 2
Add 9

Remove 6 → place at index 3
...
```

Final sorted array:

```
[2, 3, 5, 6, 8, 9, 10]
```

---

## Complexity

* **Time Complexity:** `O(n log k)`

  * Heap size is `k+1`
  * Each insertion & deletion → `O(log k)`
* **Space Complexity:** `O(k)`

🔥 Much better than `O(n log n)` when `k << n`

---

# ✅ Approach 2: Insertion Sort (Simple but Less Optimal)

### Key Idea

Since elements are close to their correct positions, insertion sort performs efficiently.

---

## Pseudocode

```
FUNCTION InsertionSort(arr):

    FOR i = 1 TO n-1:
        key = arr[i]
        j = i - 1

        WHILE j >= 0 AND arr[j] > key:
            arr[j+1] = arr[j]
            j--

        arr[j+1] = key

    RETURN arr
END FUNCTION
```

---

## Code Implementation

```csharp
public static int[] InsertionSort(int[] arr)
{
    for (int i = 1; i < arr.Length; i++)
    {
        int key = arr[i];
        int j = i - 1;

        while (j >= 0 && arr[j] > key)
        {
            arr[j + 1] = arr[j];
            j--;
        }

        arr[j + 1] = key;
    }

    return arr;
}
```

---

## Complexity

* **Worst Case:** `O(n²)`
* **Best Case (Nearly Sorted):** `O(nk)`
* **Space Complexity:** `O(1)`

⚠️ Not ideal when `k` is large.

---

# ✅ Approach 3: Using Standard Sorting

Simply call built-in sort.

---

## Code

```csharp
Array.Sort(arr);
```

---

## Complexity

* **Time Complexity:** `O(n log n)`
* **Space Complexity:** Depends on implementation

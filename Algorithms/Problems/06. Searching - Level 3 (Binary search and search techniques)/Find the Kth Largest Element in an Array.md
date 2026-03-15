## Find the Kth Largest Element in an Array

**Description:**
Given an unsorted array of integers, find the **Kth largest element**.

⚠️ Note:

* Kth largest means the element that would be at position `n - k` after sorting.
* It is **not** the Kth distinct element.
* The array does not need to be sorted fully.

---

## Examples

```
Input:
arr = [3,2,1,5,6,4]
k = 2

Output:
5

Explanation:
Sorted array → [1,2,3,4,5,6]
2nd largest = 5
```

```
Input:
arr = [3,2,3,1,2,4,5,5,6]
k = 4

Output:
4
```

```
Input:
arr = [1]
k = 1

Output:
1
```

---

## Constraints

* `1 ≤ k ≤ n`
* `1 ≤ n ≤ 10^5`
* Elements may contain duplicates
* Prefer better than `O(n log n)`

---

# ✅ Approach 1: Sorting (Simple but Not Optimal)

### Key Idea

Sort the array in ascending order.
Return element at index `n - k`.

---

## Pseudocode

```
FUNCTION FindKthLargest(arr, k):

    SORT arr in ascending order

    RETURN arr[length(arr) - k]
END FUNCTION
```

---

## Code Implementation (C#)

```csharp
public static int FindKthLargest(int[] arr, int k)
{
    Array.Sort(arr);
    return arr[arr.Length - k];
}
```

---

## Complexity

* **Time Complexity:** `O(n log n)`
* **Space Complexity:** Depends on sorting algorithm

⚠️ We are sorting entire array unnecessarily.

---

# ✅ Approach 2: Min Heap (Optimal & Common)

### 🔥 Key Idea

* Maintain a **Min Heap of size K**
* Keep only K largest elements
* The root will be the Kth largest

---

## Algorithm Steps

1. Create min heap.
2. Add elements one by one.
3. If heap size > K → remove smallest.
4. After processing all elements → top is answer.

---

## Pseudocode

```
FUNCTION FindKthLargest(arr, k):

    CREATE minHeap

    FOR each element in arr:
        minHeap.add(element)

        IF minHeap.size > k:
            minHeap.removeMin()

    RETURN minHeap.peek()
END FUNCTION
```

---

## Code Implementation

```csharp
using System.Collections.Generic;

public static int FindKthLargest(int[] nums, int k)
{
    PriorityQueue<int, int> minHeap = new PriorityQueue<int, int>();

    foreach (int num in nums)
    {
        minHeap.Enqueue(num, num);

        if (minHeap.Count > k)
        {
            minHeap.Dequeue();
        }
    }

    return minHeap.Peek();
}
```

---

## Visualization

```
arr = [3,2,1,5,6,4]
k = 2
```

Heap process:

```
Insert 3 → [3]
Insert 2 → [2,3]
Insert 1 → remove 1
Insert 5 → remove 2
Insert 6 → remove 3
Insert 4 → remove 4
```

Final heap:

```
[5,6]
Top = 5 → 2nd largest
```

---

## Complexity

* **Time Complexity:** `O(n log k)`
* **Space Complexity:** `O(k)`

🔥 Best when `k << n`

---

# ✅ Approach 3: QuickSelect (Best Average Performance)

### 🔥 Key Idea

Similar to QuickSort partition:

* Partition array around pivot
* After partition:

  * If pivot index == `n - k` → answer found
  * Else recurse left or right

---

## Pseudocode

```
FUNCTION QuickSelect(arr, left, right, targetIndex):

    pivotIndex = Partition(arr, left, right)

    IF pivotIndex == targetIndex:
        RETURN arr[pivotIndex]

    ELSE IF pivotIndex < targetIndex:
        RETURN QuickSelect(arr, pivotIndex+1, right, targetIndex)

    ELSE:
        RETURN QuickSelect(arr, left, pivotIndex-1, targetIndex)
```

Target index:

```
targetIndex = n - k
```

---

## Code Implementation

```csharp
public static int FindKthLargestQuickSelect(int[] nums, int k)
{
    int targetIndex = nums.Length - k;
    return QuickSelect(nums, 0, nums.Length - 1, targetIndex);
}

private static int QuickSelect(int[] nums, int left, int right, int target)
{
    if (left == right)
        return nums[left];

    int pivotIndex = Partition(nums, left, right);

    if (pivotIndex == target)
        return nums[pivotIndex];
    else if (pivotIndex < target)
        return QuickSelect(nums, pivotIndex + 1, right, target);
    else
        return QuickSelect(nums, left, pivotIndex - 1, target);
}

private static int Partition(int[] nums, int left, int right)
{
    int pivot = nums[right];
    int i = left;

    for (int j = left; j < right; j++)
    {
        if (nums[j] <= pivot)
        {
            Swap(nums, i, j);
            i++;
        }
    }

    Swap(nums, i, right);
    return i;
}

private static void Swap(int[] arr, int i, int j)
{
    int temp = arr[i];
    arr[i] = arr[j];
    arr[j] = temp;
}
```

---

## Visualization

Example:

```
arr = [3,2,1,5,6,4]
k = 2
targetIndex = 6 - 2 = 4
```

Partition around pivot 4:

```
[3,2,1,4,6,5]
Pivot index = 3
```

Since 3 < 4 → search right side:

```
[6,5]
```

Next partition → index = 4
Return 5 ✅

---

## Complexity

* **Average Time:** `O(n)`
* **Worst Case:** `O(n²)`
* **Space Complexity:** `O(1)`

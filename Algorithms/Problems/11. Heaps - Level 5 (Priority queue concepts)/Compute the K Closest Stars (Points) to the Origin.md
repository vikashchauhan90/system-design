# Compute the K Closest Stars (Points) to the Origin

**Description:**
Given a list of stars represented as 2D coordinates `(x, y)`, find the **K closest stars to the origin (0, 0)**.

Distance from origin:

```
Distance = √(x² + y²)
```

Since square root is expensive and unnecessary for comparison, we use:

```
Distance² = x² + y²
```

---

## Examples

```
Input:
points = [(1,3), (-2,2), (5,8), (0,1)]
k = 2

Output:
[(-2,2), (0,1)]

Explanation:
Distances:
(1,3) → 10
(-2,2) → 8
(5,8) → 89
(0,1) → 1

Two smallest distances → (0,1), (-2,2)
```

```
Input:
points = [(3,3), (5,-1), (-2,4)]
k = 1

Output:
[(3,3)]
```

---

## Constraints

* `1 ≤ n ≤ 10^5`
* `1 ≤ k ≤ n`
* Coordinates are integers
* Use squared distance for efficiency
* Prefer better than `O(n log n)`

---

# ✅ Approach 1: Max Heap (Optimal & Most Common)

### 🔥 Key Idea

* Maintain a **Max Heap of size K**
* Keep the K smallest distances
* If new point is closer than the farthest in heap → replace it

---

## Algorithm Steps

1. Create a max heap.
2. Insert first K points.
3. For remaining points:

   * If distance < max distance in heap → remove max & insert new
4. Heap contains K closest stars.

---

## Pseudocode

```
FUNCTION KClosest(points, k):

    CREATE maxHeap

    FOR each point in points:
        distance = x² + y²
        maxHeap.add(point, distance)

        IF maxHeap.size > k:
            maxHeap.removeMax()

    RETURN elements in maxHeap
END FUNCTION
```

---

## Code Implementation (C#)

```csharp
using System;
using System.Collections.Generic;

public static int[][] KClosest(int[][] points, int k)
{
    // Max Heap using negative priority
    PriorityQueue<int[], int> maxHeap = new PriorityQueue<int[], int>();

    foreach (var point in points)
    {
        int distance = point[0] * point[0] + point[1] * point[1];

        // Use negative to simulate max heap
        maxHeap.Enqueue(point, -distance);

        if (maxHeap.Count > k)
        {
            maxHeap.Dequeue();
        }
    }

    int[][] result = new int[k][];
    for (int i = 0; i < k; i++)
    {
        result[i] = maxHeap.Dequeue();
    }

    return result;
}
```

---

## Visualization

```
Points:
(1,3)  → 10
(-2,2) → 8
(5,8)  → 89
(0,1)  → 1

k = 2
```

Heap Process:

```
Insert (1,3)
Insert (-2,2)

Insert (5,8) → Remove farthest
Insert (0,1) → Remove farthest
```

Final Heap:

```
(0,1), (-2,2)
```

---

## Complexity

* **Time Complexity:** `O(n log k)`
* **Space Complexity:** `O(k)`

🔥 Best when `k << n`

---

# ✅ Approach 2: Min Heap (Alternative)

Insert all elements into a min heap and extract first K.

---

## Pseudocode

```
FUNCTION KClosest(points, k):

    CREATE minHeap

    FOR each point:
        minHeap.add(point, distance)

    FOR i = 1 TO k:
        result.add(minHeap.removeMin())

    RETURN result
```

---

## Complexity

* **Time Complexity:** `O(n log n)`
* **Space Complexity:** `O(n)`

⚠️ Not optimal when `n` is large.

---

# ✅ Approach 3: QuickSelect (Best Average Performance)

### 🔥 Key Idea

Similar to QuickSort partition:

1. Pick pivot
2. Partition based on distance
3. Recurse only on one side

After partition, first K elements are closest.

---

## Pseudocode

```
FUNCTION QuickSelect(points, left, right, k):

    pivotIndex = Partition(points, left, right)

    IF pivotIndex == k:
        RETURN
    ELSE IF pivotIndex < k:
        QuickSelect(points, pivotIndex+1, right, k)
    ELSE:
        QuickSelect(points, left, pivotIndex-1, k)
```

---

## Code Implementation (Simplified)

```csharp
public static int[][] KClosestQuickSelect(int[][] points, int k)
{
    QuickSelect(points, 0, points.Length - 1, k);
    int[][] result = new int[k][];
    Array.Copy(points, result, k);
    return result;
}

private static void QuickSelect(int[][] points, int left, int right, int k)
{
    if (left >= right) return;

    int pivotIndex = Partition(points, left, right);

    if (pivotIndex == k) return;
    else if (pivotIndex < k)
        QuickSelect(points, pivotIndex + 1, right, k);
    else
        QuickSelect(points, left, pivotIndex - 1, k);
}

private static int Partition(int[][] points, int left, int right)
{
    int[] pivot = points[right];
    int pivotDist = Distance(pivot);
    int i = left;

    for (int j = left; j < right; j++)
    {
        if (Distance(points[j]) <= pivotDist)
        {
            Swap(points, i, j);
            i++;
        }
    }

    Swap(points, i, right);
    return i;
}

private static int Distance(int[] point)
{
    return point[0] * point[0] + point[1] * point[1];
}

private static void Swap(int[][] arr, int i, int j)
{
    var temp = arr[i];
    arr[i] = arr[j];
    arr[j] = temp;
}
```

---

## Complexity

* **Average Time:** `O(n)`
* **Worst Case:** `O(n²)`
* **Space Complexity:** `O(1)`

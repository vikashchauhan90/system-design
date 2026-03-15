# Range Lookup Problem

## 📘 Description

Given:

* A **sorted array** (or sorted data structure)
* A range `[low, high]`

Return all elements that fall within that range:

```
low ≤ element ≤ high
```

---

## 🔎 Example 1 (Sorted Array)

### Input

```
arr = [1,3,5,7,9,11,15]
low = 4
high = 10
```

### Output

```
[5,7,9]
```

---

## 🔎 Example 2 (Binary Search Tree)

Return all BST nodes in range.

---

# ✅ Case 1: Range Lookup in a Sorted Array (Optimal)

## 🔥 Key Idea

1. Use **Binary Search** to find:

   * First element ≥ low
   * First element > high
2. All elements between those indices are valid.

Time Complexity:

```
O(log n + k)
```

Where `k` = number of elements in range.

---

## 🧠 Algorithm (Pseudocode)

```
FUNCTION RangeLookup(arr, low, high):

    leftIndex = FirstGreaterOrEqual(arr, low)
    rightIndex = FirstGreater(arr, high)

    result = empty list

    FOR i from leftIndex to rightIndex - 1:
        result.add(arr[i])

    RETURN result
```

---

## Helper: First Element ≥ Target

```
FUNCTION FirstGreaterOrEqual(arr, target):

    left = 0
    right = length(arr) - 1
    result = length(arr)

    WHILE left <= right:

        mid = (left + right) / 2

        IF arr[mid] >= target:
            result = mid
            right = mid - 1
        ELSE:
            left = mid + 1

    RETURN result
```

---

## Helper: First Element > Target

```
FUNCTION FirstGreater(arr, target):

    left = 0
    right = length(arr) - 1
    result = length(arr)

    WHILE left <= right:

        mid = (left + right) / 2

        IF arr[mid] > target:
            result = mid
            right = mid - 1
        ELSE:
            left = mid + 1

    RETURN result
```

---

## 💻 C# Implementation

```csharp
using System.Collections.Generic;

public static List<int> RangeLookup(int[] arr, int low, int high)
{
    int leftIndex = FirstGreaterOrEqual(arr, low);
    int rightIndex = FirstGreater(arr, high);

    List<int> result = new List<int>();

    for (int i = leftIndex; i < rightIndex; i++)
        result.Add(arr[i]);

    return result;
}

private static int FirstGreaterOrEqual(int[] arr, int target)
{
    int left = 0, right = arr.Length - 1;
    int result = arr.Length;

    while (left <= right)
    {
        int mid = left + (right - left) / 2;

        if (arr[mid] >= target)
        {
            result = mid;
            right = mid - 1;
        }
        else
        {
            left = mid + 1;
        }
    }

    return result;
}

private static int FirstGreater(int[] arr, int target)
{
    int left = 0, right = arr.Length - 1;
    int result = arr.Length;

    while (left <= right)
    {
        int mid = left + (right - left) / 2;

        if (arr[mid] > target)
        {
            result = mid;
            right = mid - 1;
        }
        else
        {
            left = mid + 1;
        }
    }

    return result;
}
```

---

## ⏱ Complexity

* Binary search → `O(log n)`
* Collecting results → `O(k)`
* **Total → O(log n + k)**

Optimal.

---

# ✅ Case 2: Range Lookup in a BST

Use modified inorder traversal.

## 🔥 Key Idea

* If node value < low → skip left subtree
* If node value > high → skip right subtree
* Otherwise → include node

---

## 🧠 Algorithm

```
FUNCTION RangeLookupBST(node, low, high, result):

    IF node is NULL:
        RETURN

    IF node.value > low:
        RangeLookupBST(node.left, low, high, result)

    IF low <= node.value <= high:
        result.add(node.value)

    IF node.value < high:
        RangeLookupBST(node.right, low, high, result)
```

---

## 💻 C# Implementation

```csharp
public static void RangeLookupBST(TreeNode node, int low, int high, List<int> result)
{
    if (node == null)
        return;

    if (node.val > low)
        RangeLookupBST(node.left, low, high, result);

    if (node.val >= low && node.val <= high)
        result.Add(node.val);

    if (node.val < high)
        RangeLookupBST(node.right, low, high, result);
}
```

---

## ⏱ Complexity

Let:

* `h` = height of tree
* `k` = results in range

Time Complexity:

```
O(h + k)
```

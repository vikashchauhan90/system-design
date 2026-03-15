# Generate All Subsets of Size K

## 📘 Description

Given:

* An array of `n` elements
* An integer `k`

Generate **all subsets (combinations)** of size `k`.

⚠️ Order does NOT matter.
This is a classic **recursion + backtracking** problem.

---

## 🔎 Example

### Input

```
arr = [1,2,3,4]
k = 2
```

### Output

```
[1,2]
[1,3]
[1,4]
[2,3]
[2,4]
[3,4]
```

---

# ✅ Approach: Backtracking (Recursive)

## 🔥 Key Idea

At each step:

* Choose current element
* Recurse to fill remaining positions
* Backtrack (remove last element)

We build subsets of size `k`.

---

# 🧠 Algorithm (Pseudocode)

```
FUNCTION GenerateSubsets(arr, k):

    result = empty list
    current = empty list

    CALL Backtrack(start = 0)

    RETURN result


FUNCTION Backtrack(start):

    IF size(current) == k:
        add copy of current to result
        RETURN

    FOR i from start to length(arr) - 1:

        add arr[i] to current

        Backtrack(i + 1)

        remove last element from current   // backtrack
```

---

# 💻 C# Implementation

```csharp
using System.Collections.Generic;

public class Solution
{
    public static List<List<int>> GenerateSubsets(int[] arr, int k)
    {
        List<List<int>> result = new List<List<int>>();
        List<int> current = new List<int>();

        Backtrack(arr, k, 0, current, result);

        return result;
    }

    private static void Backtrack(int[] arr, int k, int start,
                                  List<int> current,
                                  List<List<int>> result)
    {
        if (current.Count == k)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = start; i < arr.Length; i++)
        {
            current.Add(arr[i]);             // choose
            Backtrack(arr, k, i + 1, current, result);
            current.RemoveAt(current.Count - 1); // backtrack
        }
    }
}
```

---

# 📊 Recursion Tree Example

For:

```
arr = [1,2,3]
k = 2
```

Tree:

```
[]
 ├── 1
 │    ├── 2 → [1,2]
 │    └── 3 → [1,3]
 ├── 2
 │    └── 3 → [2,3]
 └── 3
```

---

# ⏱ Complexity

Let:

* `n` = number of elements

Number of subsets of size k:

```
C(n, k) = n! / (k! * (n-k)!)
```

### Time Complexity

```
O(C(n,k) * k)
```

### Space Complexity

```
O(k) recursion stack
```

---

# 🔥 Optimization (Pruning)

We can prune unnecessary recursion.

Instead of:

```
for i = start to n-1
```

Use:

```
for i = start to n - (k - current.size)
```

Because:

We must leave enough elements to fill remaining positions.

---

## Optimized Pseudocode

```
FOR i from start to n - (k - size(current)):

    choose arr[i]
    recurse
    backtrack
```

This avoids useless branches.

---

# ✅ Alternative Approach: Using Bitmask (Less Preferred)

For small n:

* Generate all subsets (2^n)
* Keep only size k

Time complexity:

```
O(2^n)
```

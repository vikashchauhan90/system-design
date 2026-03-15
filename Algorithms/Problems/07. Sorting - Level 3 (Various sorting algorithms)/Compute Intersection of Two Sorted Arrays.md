## Compute Intersection of Two Sorted Arrays

**Description:**
Given two **sorted arrays**, compute their **intersection**.

Intersection means elements that appear in **both arrays**.

Depending on requirement, intersection can be:

1. **Unique Intersection** → Each common element appears once
2. **With Duplicates** → Include duplicates as many times as they appear in both arrays

---

## Examples

### Example 1 (Unique Intersection)

```
Input:
arr1 = [1,2,2,3,4]
arr2 = [2,2,4,6]

Output:
[2,4]
```

---

### Example 2 (With Duplicates)

```
Input:
arr1 = [1,2,2,3,4]
arr2 = [2,2,4,6]

Output:
[2,2,4]
```

---

### Example 3

```
Input:
arr1 = [1,3,5]
arr2 = [2,4,6]

Output:
[]
```

---

## Constraints

* Arrays are sorted in non-decreasing order
* `1 ≤ n, m ≤ 10^5`
* Prefer `O(n + m)` time
* Extra space allowed for result

---

# ✅ Approach 1: Two Pointer Technique (Optimal & Most Common)

### 🔥 Key Idea

Since arrays are sorted:

* Use two pointers `i` and `j`
* Compare elements
* Move pointer of smaller element
* If equal → add to result and move both

---

# 🔹 Case 1: Intersection WITH Duplicates

---

## Pseudocode

```
FUNCTION IntersectionWithDuplicates(arr1, arr2):

    i = 0
    j = 0
    result = empty list

    WHILE i < length(arr1) AND j < length(arr2):

        IF arr1[i] == arr2[j]:
            result.add(arr1[i])
            i++
            j++

        ELSE IF arr1[i] < arr2[j]:
            i++

        ELSE:
            j++

    RETURN result
END FUNCTION
```

---

## Code Implementation (C#)

```csharp
using System.Collections.Generic;

public static List<int> IntersectionWithDuplicates(int[] arr1, int[] arr2)
{
    int i = 0, j = 0;
    List<int> result = new List<int>();

    while (i < arr1.Length && j < arr2.Length)
    {
        if (arr1[i] == arr2[j])
        {
            result.Add(arr1[i]);
            i++;
            j++;
        }
        else if (arr1[i] < arr2[j])
        {
            i++;
        }
        else
        {
            j++;
        }
    }

    return result;
}
```

---

## Visualization

```
arr1 = [1,2,2,3,4]
arr2 = [2,2,4,6]
```

Step-by-step:

```
1 < 2 → move i
2 == 2 → add 2
2 == 2 → add 2
3 < 4 → move i
4 == 4 → add 4
```

Result:

```
[2,2,4]
```

---

## Complexity

* **Time Complexity:** `O(n + m)`
* **Space Complexity:** `O(min(n, m))`

🔥 Optimal

---

# 🔹 Case 2: Unique Intersection (No Duplicates in Result)

We skip duplicates while adding.

---

## Pseudocode

```
FUNCTION UniqueIntersection(arr1, arr2):

    i = 0
    j = 0
    result = empty list

    WHILE i < n AND j < m:

        IF arr1[i] == arr2[j]:
            IF result is empty OR result.last != arr1[i]:
                result.add(arr1[i])

            i++
            j++

        ELSE IF arr1[i] < arr2[j]:
            i++
        ELSE:
            j++

    RETURN result
END FUNCTION
```

---

## Code Implementation

```csharp
public static List<int> UniqueIntersection(int[] arr1, int[] arr2)
{
    int i = 0, j = 0;
    List<int> result = new List<int>();

    while (i < arr1.Length && j < arr2.Length)
    {
        if (arr1[i] == arr2[j])
        {
            if (result.Count == 0 || result[result.Count - 1] != arr1[i])
            {
                result.Add(arr1[i]);
            }
            i++;
            j++;
        }
        else if (arr1[i] < arr2[j])
        {
            i++;
        }
        else
        {
            j++;
        }
    }

    return result;
}
```

---

## Complexity

* **Time Complexity:** `O(n + m)`
* **Space Complexity:** `O(min(n, m))`

---

# ✅ Approach 2: Using HashSet (When Arrays Not Sorted)

### 🔥 Key Idea

1. Insert elements of first array into HashSet
2. Traverse second array
3. If element exists → add to result

---

## Pseudocode

```
FUNCTION IntersectionHash(arr1, arr2):

    set = HashSet(arr1)
    result = empty set

    FOR each element in arr2:
        IF element in set:
            result.add(element)

    RETURN result
END FUNCTION
```

---

## Code Implementation

```csharp
using System.Collections.Generic;

public static List<int> IntersectionHash(int[] arr1, int[] arr2)
{
    HashSet<int> set = new HashSet<int>(arr1);
    HashSet<int> result = new HashSet<int>();

    foreach (int num in arr2)
    {
        if (set.Contains(num))
        {
            result.Add(num);
        }
    }

    return new List<int>(result);
}
```

---

## Complexity

* **Time Complexity:** `O(n + m)`
* **Space Complexity:** `O(n)`

# Delete Duplicates from a Sorted Singly Linked List

**Description:**
Given a **sorted singly linked list**, delete all duplicate nodes such that each element appears only once.

Since the list is sorted, duplicate values will always be **adjacent**.

---

## Examples:

```
Input:
Head → 1 → 1 → 2 → 3 → 3 → 4 → 4 → 4 → 5

Output:
Head → 1 → 2 → 3 → 4 → 5
```

```
Input:
Head → 10 → 10 → 10 → 10

Output:
Head → 10
```

```
Input:
Head → 1 → 2 → 3

Output:
Head → 1 → 2 → 3
```

```
Input:
Head → NULL

Output:
NULL
```

---

## Constraints

* Linked list is **sorted in non-decreasing order**
* May contain multiple duplicates
* Delete duplicates **in-place**
* Use `O(1)` extra space preferred
* Time complexity should be `O(n)`

---

# ✅ Approach 1: Iterative (Most Efficient & Common)

### Key Idea:

Since the list is sorted:

* Compare current node with next node
* If equal → skip next node
* Otherwise → move forward

---

## Pseudocode:

```
FUNCTION RemoveDuplicates(head):

    IF head == NULL:
        RETURN NULL

    current = head

    WHILE current != NULL AND current.next != NULL:

        IF current.data == current.next.data:
            current.next = current.next.next
        ELSE:
            current = current.next

    RETURN head
END FUNCTION
```

---

## Code Implementation (C#):

```csharp
public class ListNode
{
    public int Data;
    public ListNode Next;

    public ListNode(int data)
    {
        Data = data;
        Next = null;
    }
}

public static ListNode RemoveDuplicates(ListNode head)
{
    if (head == null)
        return null;

    ListNode current = head;

    while (current != null && current.Next != null)
    {
        if (current.Data == current.Next.Data)
        {
            // Skip duplicate node
            current.Next = current.Next.Next;
        }
        else
        {
            current = current.Next;
        }
    }

    return head;
}
```

---

## Visualization

### Example:

```
1 → 1 → 2 → 3 → 3 → 4
```

Step-by-step:

```
Compare 1 & 1 → Equal → Remove
1 → 2 → 3 → 3 → 4

Compare 1 & 2 → Not equal → Move

Compare 2 & 3 → Not equal → Move

Compare 3 & 3 → Equal → Remove
1 → 2 → 3 → 4
```

Final Result:

```
1 → 2 → 3 → 4
```

---

## Complexity

* **Time Complexity:** `O(n)`

  * Each node visited once
* **Space Complexity:** `O(1)`

  * No extra space used

---

# ✅ Approach 2: Recursive Approach

### Key Idea:

* Base case: if list empty or one node → return head
* Recursively remove duplicates in sublist
* Compare head with head.next

---

## Pseudocode:

```
FUNCTION RemoveDuplicatesRecursive(head):

    IF head == NULL OR head.next == NULL:
        RETURN head

    head.next = RemoveDuplicatesRecursive(head.next)

    IF head.data == head.next.data:
        RETURN head.next
    ELSE:
        RETURN head
END FUNCTION
```

---

## Code Implementation:

```csharp
public static ListNode RemoveDuplicatesRecursive(ListNode head)
{
    if (head == null || head.Next == null)
        return head;

    head.Next = RemoveDuplicatesRecursive(head.Next);

    if (head.Data == head.Next.Data)
        return head.Next;

    return head;
}
```

---

## Visualization

Example:

```
1 → 1 → 2 → 3 → 3
```

Call Stack:

```
Remove(1)
  → Remove(1)
     → Remove(2)
        → Remove(3)
           → Remove(3)
```

Duplicates removed during stack unwind.

Final:

```
1 → 2 → 3
```

---

## Complexity

* **Time Complexity:** `O(n)`
* **Space Complexity:** `O(n)` (recursive stack)

---

# ⭐ Follow-Up (Important Interview Variation)

## Remove All Duplicates (LeetCode 82 Style)

⚠️ Remove numbers that appear more than once entirely.

Example:

```
Input:
1 → 2 → 3 → 3 → 4 → 4 → 5

Output:
1 → 2 → 5
```

---

### Pseudocode:

```
CREATE dummy node before head
prev = dummy
current = head

WHILE current != NULL:
    IF current.next != NULL AND current.data == current.next.data:
        duplicateValue = current.data

        WHILE current != NULL AND current.data == duplicateValue:
            current = current.next

        prev.next = current
    ELSE:
        prev = current
        current = current.next

RETURN dummy.next
```

---

### Complexity

* **Time Complexity:** `O(n)`
* **Space Complexity:** `O(1)`

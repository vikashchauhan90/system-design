# Delete Node from a Singly Linked List

**Description:**
Delete a node from a **singly linked list** based on a given key (value).
If the key exists, remove the first occurrence of that node and adjust pointers accordingly.

A singly linked list node contains:

```
Data
Next pointer → points to next node
```

---

## Examples:

```
Input:
Head → 1 → 2 → 3 → 4 → 5
Key = 3

Output:
Head → 1 → 2 → 4 → 5
```

```
Input:
Head → 10 → 20 → 30
Key = 10

Output:
Head → 20 → 30
```

```
Input:
Head → 5
Key = 5

Output:
Head → NULL
```

```
Input:
Head → 1 → 2 → 3
Key = 10

Output:
Head → 1 → 2 → 3   (Key not found)
```

---

## Constraints

* Linked list may be empty
* Delete only the **first occurrence**
* Values are within integer range
* Time complexity should ideally be `O(n)`
* Prefer `O(1)` space

---

# ✅ Approach 1: Iterative Traversal (Most Common)

### Key Idea:

1. If head contains the key → move head to next
2. Otherwise traverse the list
3. Keep track of previous node
4. Adjust `prev.next = current.next`

---

## Pseudocode:

```
FUNCTION DeleteNode(head, key):

    IF head == NULL:
        RETURN NULL

    // Case 1: Head contains key
    IF head.data == key:
        RETURN head.next

    prev = head
    current = head.next

    WHILE current != NULL:
        IF current.data == key:
            prev.next = current.next
            BREAK
        prev = current
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

public static ListNode DeleteNode(ListNode head, int key)
{
    if (head == null)
        return null;

    // Case 1: Delete head
    if (head.Data == key)
        return head.Next;

    ListNode prev = head;
    ListNode current = head.Next;

    while (current != null)
    {
        if (current.Data == key)
        {
            prev.Next = current.Next;
            break;
        }

        prev = current;
        current = current.Next;
    }

    return head;
}
```

---

## Visualization

### Example:

```
Head → 1 → 2 → 3 → 4 → 5
Key = 3
```

Traversal:

```
prev=1, curr=2
prev=2, curr=3  ← Found
```

Adjustment:

```
prev.next = curr.next
2 → 4
```

Result:

```
1 → 2 → 4 → 5
```

---

## Complexity

* **Time Complexity:** `O(n)`

  * In worst case we traverse entire list
* **Space Complexity:** `O(1)`

  * No extra space used

---

# ✅ Approach 2: Recursive Deletion

### Key Idea:

* If head is null → return null
* If head contains key → return head.next
* Otherwise recursively delete in sublist

---

## Pseudocode:

```
FUNCTION DeleteRecursive(head, key):

    IF head == NULL:
        RETURN NULL

    IF head.data == key:
        RETURN head.next

    head.next = DeleteRecursive(head.next, key)

    RETURN head
END FUNCTION
```

---

## Code Implementation:

```csharp
public static ListNode DeleteRecursive(ListNode head, int key)
{
    if (head == null)
        return null;

    if (head.Data == key)
        return head.Next;

    head.Next = DeleteRecursive(head.Next, key);

    return head;
}
```

---

## Visualization

Example:

```
DeleteRecursive(1 → 2 → 3 → 4, 3)
```

Call Stack:

```
Delete(1)
  → Delete(2)
      → Delete(3)  ← Match
         return 4
      2.next = 4
  1.next = 2
Return 1
```

Result:

```
1 → 2 → 4
```

---

## Complexity

* **Time Complexity:** `O(n)`
* **Space Complexity:** `O(n)` (Recursive stack)

---

# ✅ Approach 3: Delete Given Node (Without Head Reference)

⚠️ Special Case (Interview Favorite)

If only the node to delete is given (not head), and it’s NOT the last node.

### Idea:

Copy next node's data into current node
Then delete next node

---

## Pseudocode:

```
FUNCTION DeleteGivenNode(node):

    IF node == NULL OR node.next == NULL:
        RETURN  // Not possible

    node.data = node.next.data
    node.next = node.next.next
END FUNCTION
```

---

## Code Implementation:

```csharp
public static void DeleteGivenNode(ListNode node)
{
    if (node == null || node.Next == null)
        return;

    node.Data = node.Next.Data;
    node.Next = node.Next.Next;
}
```

---

## Visualization

```
Given Node = 3

1 → 2 → 3 → 4 → 5
```

Copy 4 into 3:

```
1 → 2 → 4 → 4 → 5
```

Bypass next:

```
1 → 2 → 4 → 5
```

---

## Complexity

* **Time Complexity:** `O(1)`
* **Space Complexity:** `O(1)`


# Find the K Largest Elements in a BST

## 📘 Description

Given the root of a **Binary Search Tree (BST)** and an integer `k`, return the **k largest elements** in the BST.

### 🔑 Important BST Property

In a BST:

* **Left subtree** → Smaller values
* **Right subtree** → Larger values

So if we traverse:

* **Inorder (Left → Root → Right)** → Sorted ascending
* **Reverse Inorder (Right → Root → Left)** → Sorted descending ✅

---

## ✅ Optimal Approach: Reverse Inorder Traversal

### 🔥 Key Idea

To get largest elements first:

1. Traverse **Right subtree**
2. Visit Root
3. Traverse Left subtree
4. Stop once we collect `k` elements

This avoids traversing entire tree.

---

## 🧠 Algorithm (Pseudocode)

```
FUNCTION FindKLargest(root, k):

    result = empty list

    CALL ReverseInorder(root, k, result)

    RETURN result


FUNCTION ReverseInorder(node, k, result):

    IF node is NULL OR result.size == k:
        RETURN

    // Step 1: Visit right subtree
    ReverseInorder(node.right, k, result)

    // Step 2: Process current node
    IF result.size < k:
        result.add(node.value)

    // Step 3: Visit left subtree
    ReverseInorder(node.left, k, result)
```

---

## 💻 C# Implementation

```csharp
using System.Collections.Generic;

public class TreeNode
{
    public int val;
    public TreeNode left;
    public TreeNode right;

    public TreeNode(int value)
    {
        val = value;
    }
}

public class Solution
{
    public static List<int> FindKLargest(TreeNode root, int k)
    {
        List<int> result = new List<int>();
        ReverseInorder(root, k, result);
        return result;
    }

    private static void ReverseInorder(TreeNode node, int k, List<int> result)
    {
        if (node == null || result.Count == k)
            return;

        // Right
        ReverseInorder(node.right, k, result);

        // Root
        if (result.Count < k)
            result.Add(node.val);

        // Left
        ReverseInorder(node.left, k, result);
    }
}
```

---

## 📊 Example

### Input BST

```
        50
       /  \
     30    70
    / \    / \
   20 40  60 80
```

### Input

```
k = 3
```

### Reverse Inorder Traversal Order

```
80 → 70 → 60 → 50 → 40 → 30 → 20
```

### Output

```
[80, 70, 60]
```

---

## ⏱ Complexity

Let:

* `n` = number of nodes
* `h` = height of tree

### Time Complexity

* **O(h + k)**
  (We stop after finding k elements)

### Space Complexity

* **O(h)** (recursion stack)

---

# ✅ Iterative Approach (Using Stack)

If recursion not allowed:

---

## 🧠 Algorithm

```
FUNCTION FindKLargest(root, k):

    stack = empty stack
    result = empty list
    current = root

    WHILE (stack not empty OR current not NULL):

        WHILE current not NULL:
            stack.push(current)
            current = current.right

        current = stack.pop()
        result.add(current.value)

        IF result.size == k:
            BREAK

        current = current.left

    RETURN result
```

---

## 💻 C# Implementation

```csharp
using System.Collections.Generic;

public static List<int> FindKLargestIterative(TreeNode root, int k)
{
    Stack<TreeNode> stack = new Stack<TreeNode>();
    List<int> result = new List<int>();
    TreeNode current = root;

    while (stack.Count > 0 || current != null)
    {
        while (current != null)
        {
            stack.Push(current);
            current = current.right;
        }

        current = stack.Pop();
        result.Add(current.val);

        if (result.Count == k)
            break;

        current = current.left;
    }

    return result;
}
```

---

# 🔥 Alternative Approach: Using Min Heap

If BST property cannot be fully trusted:

1. Traverse entire tree
2. Maintain min-heap of size k
3. If heap size > k → remove smallest

### Time Complexity:

```
O(n log k)
```

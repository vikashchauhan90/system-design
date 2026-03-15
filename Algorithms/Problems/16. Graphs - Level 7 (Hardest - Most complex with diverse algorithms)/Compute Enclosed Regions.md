# 🟢 Problem Title: Compute Enclosed Regions

(Also known as **Surrounded Regions Problem**)

---

## 📘 Problem Description

You are given a 2D board containing:

```
'X' → blocked cell
'O' → open cell
```

An enclosed region is:

> A group of `'O'` cells completely surrounded by `'X'`.

Your task:

Convert all enclosed `'O'` regions into `'X'`.

---

## 🔎 Example

### Input

```
X X X X
X O O X
X X O X
X O X X
```

### Output

```
X X X X
X X X X
X X X X
X O X X
```

Explanation:

* The middle region is enclosed → convert to X
* The bottom `'O'` touches boundary → NOT enclosed

---

# 🔥 Key Insight

An `'O'` is **NOT enclosed** if:

* It is connected to the boundary.

So instead of finding enclosed regions directly:

✅ Mark all `'O'` cells connected to boundary.
❌ Convert remaining `'O'` cells to `'X'`.

---

# ✅ Approach: DFS / BFS from Boundary

---

## 🧠 Algorithm Steps

### Step 1:

Traverse boundary of board.

For every boundary `'O'`:

* Perform DFS/BFS
* Mark connected `'O'` as safe (e.g., `'S'`)

---

### Step 2:

Traverse entire board:

* Convert remaining `'O'` → `'X'`
* Convert `'S'` → `'O'`

---

# 🧠 Pseudocode

```
FUNCTION ComputeEnclosedRegions(board):

    rows = number of rows
    cols = number of cols

    FOR each boundary cell:
        IF cell == 'O':
            DFS mark safe

    FOR each cell in board:
        IF cell == 'O':
            convert to 'X'
        IF cell == 'S':
            convert back to 'O'
```

---

## DFS Marking

```
FUNCTION DFS(r, c):

    IF out of bounds OR board[r][c] != 'O':
        RETURN

    board[r][c] = 'S'

    DFS(r+1, c)
    DFS(r-1, c)
    DFS(r, c+1)
    DFS(r, c-1)
```

---

# 💻 C# Implementation

```csharp
public class Solution
{
    public void ComputeEnclosedRegions(char[][] board)
    {
        if (board == null || board.Length == 0)
            return;

        int rows = board.Length;
        int cols = board[0].Length;

        // Step 1: Mark boundary-connected 'O's
        for (int r = 0; r < rows; r++)
        {
            DFS(board, r, 0);
            DFS(board, r, cols - 1);
        }

        for (int c = 0; c < cols; c++)
        {
            DFS(board, 0, c);
            DFS(board, rows - 1, c);
        }

        // Step 2: Flip enclosed regions
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (board[r][c] == 'O')
                    board[r][c] = 'X';
                else if (board[r][c] == 'S')
                    board[r][c] = 'O';
            }
        }
    }

    private void DFS(char[][] board, int r, int c)
    {
        int rows = board.Length;
        int cols = board[0].Length;

        if (r < 0 || c < 0 || r >= rows || c >= cols)
            return;

        if (board[r][c] != 'O')
            return;

        board[r][c] = 'S';

        DFS(board, r + 1, c);
        DFS(board, r - 1, c);
        DFS(board, r, c + 1);
        DFS(board, r, c - 1);
    }
}
```

---

# ⏱ Complexity

```
Time: O(rows × cols)
Space: O(rows × cols)  (recursion stack worst case)
```

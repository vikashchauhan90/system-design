# Sudoku Solver Algorithm

## 📘 Description

Given a **9 × 9 Sudoku board**, fill the empty cells so that:

1. Each row contains digits **1–9** exactly once
2. Each column contains digits **1–9** exactly once
3. Each 3×3 subgrid contains digits **1–9** exactly once

Empty cells are usually represented as:

```
'.'  or  0
```

---

# ✅ Approach: Backtracking (Recursion)

Sudoku solving is a classic **constraint satisfaction + backtracking** problem.

---

# 🔥 Key Idea

1. Find an empty cell
2. Try digits 1 → 9
3. Check if valid
4. If valid → place digit
5. Recursively solve rest of board
6. If stuck → backtrack (undo placement)

---

# 🧠 Algorithm (Pseudocode)

```
FUNCTION SolveSudoku(board):

    FOR each row from 0 to 8:
        FOR each col from 0 to 8:

            IF board[row][col] is empty:

                FOR digit from '1' to '9':

                    IF IsValid(board, row, col, digit):

                        board[row][col] = digit

                        IF SolveSudoku(board) == true:
                            RETURN true

                        board[row][col] = empty   // backtrack

                RETURN false   // no valid digit found

    RETURN true   // board completely filled
```

---

# 🔎 Validity Check

We must ensure:

* Digit not in same row
* Digit not in same column
* Digit not in same 3×3 box

---

## 🧠 IsValid Pseudocode

```
FUNCTION IsValid(board, row, col, digit):

    FOR i from 0 to 8:

        IF board[row][i] == digit:
            RETURN false

        IF board[i][col] == digit:
            RETURN false

        boxRow = 3 * (row / 3) + i / 3
        boxCol = 3 * (col / 3) + i % 3

        IF board[boxRow][boxCol] == digit:
            RETURN false

    RETURN true
```

---

# 💻 C# Implementation

```csharp
public class Solution
{
    public void SolveSudoku(char[][] board)
    {
        Solve(board);
    }

    private bool Solve(char[][] board)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (board[row][col] == '.')
                {
                    for (char num = '1'; num <= '9'; num++)
                    {
                        if (IsValid(board, row, col, num))
                        {
                            board[row][col] = num;

                            if (Solve(board))
                                return true;

                            board[row][col] = '.'; // backtrack
                        }
                    }

                    return false; // no valid number
                }
            }
        }

        return true; // solved
    }

    private bool IsValid(char[][] board, int row, int col, char num)
    {
        for (int i = 0; i < 9; i++)
        {
            if (board[row][i] == num)
                return false;

            if (board[i][col] == num)
                return false;

            int boxRow = 3 * (row / 3) + i / 3;
            int boxCol = 3 * (col / 3) + i % 3;

            if (board[boxRow][boxCol] == num)
                return false;
        }

        return true;
    }
}
```

---

# ⏱ Complexity

Worst-case time complexity:

```
O(9^(n))
```

Where `n` = number of empty cells.

⚠️ In practice, pruning reduces it dramatically.

Space complexity:

```
O(n) recursion stack
```

---

# 🔥 Optimized Version (Faster)

Instead of checking row/col/box every time:

Maintain:

* `rows[9][10]`
* `cols[9][10]`
* `boxes[9][10]`

To track used digits.

Then validity check becomes:

```
O(1)
```

This greatly improves performance.

---

# 🎯 Interview Tips

Mention:

* This is backtracking
* Constraint checking is key
* Optimization using hash sets / boolean arrays
* Can be further optimized with bitmasking

---

# 🚀 Advanced Optimization: Bitmasking

Use 9 integers per row/col/box.

Each digit represented as a bit:

```
1 << digit
```

Validity check becomes:

```
(rowMask & bit) == 0
```

Very fast.

---


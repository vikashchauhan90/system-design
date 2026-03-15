# 🟢 Problem Title: Search a Maze (Path Exists in a Grid)

---

## 📘 Problem Description

You are given a 2D grid (maze):

* `0` → open cell (can move)
* `1` → blocked cell (wall)

Given:

* `start = (sr, sc)`
* `destination = (dr, dc)`

Determine:

> Is there a valid path from start to destination?

You can move:

```
Up, Down, Left, Right
```

This is a **graph traversal problem** because:

* Each cell = node
* Valid moves = edges

---

## 🔎 Example

### Maze

```
0 0 1 0
0 0 0 0
1 0 1 0
0 0 0 0
```

Start: `(0,0)`
Destination: `(3,3)`

Output:

```
True
```

---

# ✅ Approach 1: Depth-First Search (DFS)

---

## 🔥 Idea

1. Start from `(sr, sc)`
2. Explore all valid neighbors
3. Mark visited cells
4. Stop if destination reached

---

## 🧠 Algorithm (DFS - Recursive)

```
FUNCTION SearchMaze(maze, sr, sc, dr, dc):

    IF sr or sc out of bounds OR maze[sr][sc] == 1:
        RETURN false

    IF sr == dr AND sc == dc:
        RETURN true

    Mark maze[sr][sc] as visited

    FOR each direction (up, down, left, right):

        newRow = sr + directionRow
        newCol = sc + directionCol

        IF SearchMaze(maze, newRow, newCol, dr, dc):
            RETURN true

    RETURN false
```

---

## 💻 C# Implementation (DFS)

```csharp
public class Solution
{
    private int[][] directions = new int[][]
    {
        new int[]{1,0},   // down
        new int[]{-1,0},  // up
        new int[]{0,1},   // right
        new int[]{0,-1}   // left
    };

    public bool SearchMaze(int[][] maze, int sr, int sc, int dr, int dc)
    {
        int rows = maze.Length;
        int cols = maze[0].Length;

        bool[,] visited = new bool[rows, cols];

        return DFS(maze, sr, sc, dr, dc, visited);
    }

    private bool DFS(int[][] maze, int r, int c,
                     int dr, int dc, bool[,] visited)
    {
        if (r < 0 || c < 0 || r >= maze.Length || c >= maze[0].Length)
            return false;

        if (maze[r][c] == 1 || visited[r, c])
            return false;

        if (r == dr && c == dc)
            return true;

        visited[r, c] = true;

        foreach (var dir in directions)
        {
            int newRow = r + dir[0];
            int newCol = c + dir[1];

            if (DFS(maze, newRow, newCol, dr, dc, visited))
                return true;
        }

        return false;
    }
}
```

---

## ⏱ Complexity

```
Time: O(rows × cols)
Space: O(rows × cols)  (recursion + visited)
```

---

# ✅ Approach 2: Breadth-First Search (BFS)

Better when you need:

* Shortest path
* Minimum steps

---

## 🔥 Idea

Use queue.

---

## 🧠 Algorithm (BFS)

```
FUNCTION SearchMazeBFS(maze, start, destination):

    Create queue
    Mark start as visited
    Enqueue start

    WHILE queue not empty:

        cell = dequeue

        IF cell == destination:
            RETURN true

        FOR each direction:

            neighbor = valid neighbor

            IF not visited AND not blocked:
                mark visited
                enqueue neighbor

    RETURN false
```

---

## 💻 C# Implementation (BFS)

```csharp
using System.Collections.Generic;

public bool SearchMazeBFS(int[][] maze, int sr, int sc, int dr, int dc)
{
    int rows = maze.Length;
    int cols = maze[0].Length;

    bool[,] visited = new bool[rows, cols];
    Queue<(int, int)> queue = new Queue<(int, int)>();

    queue.Enqueue((sr, sc));
    visited[sr, sc] = true;

    int[][] directions = new int[][]
    {
        new int[]{1,0},
        new int[]{-1,0},
        new int[]{0,1},
        new int[]{0,-1}
    };

    while (queue.Count > 0)
    {
        var (r, c) = queue.Dequeue();

        if (r == dr && c == dc)
            return true;

        foreach (var dir in directions)
        {
            int nr = r + dir[0];
            int nc = c + dir[1];

            if (nr >= 0 && nc >= 0 &&
                nr < rows && nc < cols &&
                maze[nr][nc] == 0 &&
                !visited[nr, nc])
            {
                visited[nr, nc] = true;
                queue.Enqueue((nr, nc));
            }
        }
    }

    return false;
}
```

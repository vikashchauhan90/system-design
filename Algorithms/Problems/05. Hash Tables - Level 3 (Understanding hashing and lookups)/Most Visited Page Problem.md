## Most Visited Pages

---

## 📘 Problem Description

You are given a list of page visits:

```
pages = ["home", "about", "home", "contact", "home", "about"]
```

Each entry represents a page visited.

Your task:

> Return the page that was visited the **most number of times**.

---

## 🔎 Example

### Input

```
["home", "about", "home", "contact", "home", "about"]
```

### Output

```
"home"
```

Because:

```
home → 3 visits
about → 2 visits
contact → 1 visit
```

---

# ✅ Approach 1: Hash Map (Optimal)

---

## 🔥 Key Idea

1. Count frequency of each page.
2. Track maximum count while iterating.

This avoids sorting.

---

## 🧠 Algorithm (Pseudocode)

```
FUNCTION MostVisitedPage(pages):

    IF pages is empty:
        RETURN null

    frequencyMap = empty HashMap
    maxCount = 0
    result = null

    FOR each page in pages:

        IF page not in frequencyMap:
            frequencyMap[page] = 0

        frequencyMap[page]++

        IF frequencyMap[page] > maxCount:
            maxCount = frequencyMap[page]
            result = page

    RETURN result
```

---

## 💻 C# Implementation

```csharp
using System.Collections.Generic;

public static string MostVisitedPage(string[] pages)
{
    if (pages == null || pages.Length == 0)
        return null;

    Dictionary<string, int> frequency = new Dictionary<string, int>();
    int maxCount = 0;
    string mostVisited = null;

    foreach (string page in pages)
    {
        if (!frequency.ContainsKey(page))
            frequency[page] = 0;

        frequency[page]++;

        if (frequency[page] > maxCount)
        {
            maxCount = frequency[page];
            mostVisited = page;
        }
    }

    return mostVisited;
}
```

---

## ⏱ Complexity

```
Time: O(n)
Space: O(n)
```

Where `n` = number of visits.

---

# ✅ Variation 1: Handle Ties (Return All Most Visited)

---

## 🧠 Algorithm

```
FUNCTION MostVisitedPages(pages):

    frequencyMap = count frequencies

    maxCount = maximum frequency

    result = empty list

    FOR each (page, count) in frequencyMap:
        IF count == maxCount:
            add page to result

    RETURN result
```

---

# ✅ Variation 2: Return Top K Most Visited Pages

Use **Min Heap (Priority Queue)**.

---

## 🧠 Algorithm (Top K)

```
1. Count frequencies using HashMap
2. Create minHeap of size K
3. For each page in map:
       push into heap
       if heap size > K:
            remove smallest
4. Return heap elements
```

### Time Complexity:

```
O(n log k)
```

---

# ✅ Variation 3: Streaming Data (Large Scale System)

If logs are huge:

* Use HashMap + MinHeap
* Or use distributed processing (MapReduce)
* Or approximate using Count-Min Sketch

---

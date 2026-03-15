## First Letter to Appear Twice
Given a string s consisting of lowercase English letters, return the first letter to appear twice.

Note:

A letter a appears twice before another letter b if the second occurrence of a is before the second occurrence of b.
s will contain at least one letter that appears twice.


Example 1:

Input: s = "abccbaacz"
Output: "c"
Explanation:
The letter 'a' appears on the indexes 0, 5 and 6.
The letter 'b' appears on the indexes 1 and 4.
The letter 'c' appears on the indexes 2, 3 and 7.
The letter 'z' appears on the index 8.
The letter 'c' is the first letter to appear twice, because out of all the letters the index of its second occurrence is the smallest.
Example 2:

Input: s = "abcdd"
Output: "d"
Explanation:
The only letter that appears twice is 'd' so we return 'd'.


Constraints:

2 <= s.length <= 100
s consists of lowercase English letters.
s has at least one repeated letter.

### Using HashSet

```csharp

public static char RepeatedCharacter(string s) {
        HashSet<char> seen = new HashSet<char>();

        foreach (char c in s) {
            if (seen.Contains(c)) {
                return c;  // First character that appears twice
            }
            seen.Add(c);
        }

        return '\0'; // No repeated character (should not happen per problem constraints)
    }

```

### Using Boolean Array (More Efficient)

```csharp
public static char RepeatedCharacter(string s) {
        // Since input consists of lowercase English letters
        bool[] seen = new bool[26];

        foreach (char c in s) {
            int index = c - 'a';  // Convert 'a'→0, 'b'→1, etc.

            if (seen[index]) {
                return c;  // First character that appears twice
            }
            seen[index] = true;
        }

        return '\0';
    }
```

### Using Bit Manipulation (Most Efficient)

```csharp
public static char RepeatedCharacter(string s) {
        int seen = 0;  // 32-bit integer to track seen letters (26 bits needed)

        foreach (char c in s) {
            int bit = 1 << (c - 'a');  // Set bit for current character

            if ((seen & bit) != 0) {
                return c;  // Character already seen
            }
            seen |= bit;  // Mark character as seen
        }

        return '\0';
    }
```

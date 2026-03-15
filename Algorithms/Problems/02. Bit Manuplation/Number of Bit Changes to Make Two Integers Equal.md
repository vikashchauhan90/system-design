## Number of Bit Changes to Make Two Integers Equal

You are given two positive integers n and k.

You can choose any bit in the binary representation of n that is equal to 1 and change it to 0.

Return the number of changes needed to make n equal to k. If it is impossible, return -1.



Example 1:

Input: n = 13, k = 4

Output: 2

Explanation:
Initially, the binary representations of n and k are n = (1101)2 and k = (0100)2.
We can change the first and fourth bits of n. The resulting integer is n = (0100)2 = k.

Example 2:

Input: n = 21, k = 21

Output: 0

Explanation:
n and k are already equal, so no changes are needed.

Example 3:

Input: n = 14, k = 13

Output: -1

Explanation:
It is not possible to make n equal to k.



Constraints:

1 <= n, k <= 106


```csharp
public class Solution {
    public int MinChanges(int n, int k) {
        // Check if k is a subset of n's bits
        // If (n & k) is not equal to k, it means k has a 1 bit where n has a 0
        if ((n & k) != k) {
            return -1;
        }

        // XOR identifies the bits that are different between n and k
        // Since we can only change 1->0, these differing bits are the ones we need to flip
        int diff = n ^ k;

        // Count the number of 1s in diff - this is the number of bits to change
        return CountBits(diff);
    }

    private int CountBits(int x) {
        int count = 0;
        while (x > 0) {
            count += x & 1;  // Check if last bit is 1
            x >>= 1;          // Shift right to check next bit
        }
        return count;
    }
}
```

```csharp
public class Solution {
    public int MinChanges(int n, int k) {
        // Check if k has any 1 bits where n has 0
        if ((n & k) != k) {
            return -1;
        }

        // Count the bits that are 1 in n but 0 in k
        // This is essentially counting 1s in (n ^ k) but only where n has 1s
        int changes = 0;
        int xor = n ^ k;

        // Brian Kernighan's algorithm - counts set bits efficiently
        while (xor > 0) {
            xor &= (xor - 1);  // Clears the lowest set bit
            changes++;
        }

        return changes;
    }
}
```

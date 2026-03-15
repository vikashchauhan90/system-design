## Power of Two
Given an integer n, return true if it is a power of two. Otherwise, return false.

An integer n is a power of two, if there exists an integer x such that n == 2x.



Example 1:

Input: n = 1
Output: true
Explanation: 20 = 1
Example 2:

Input: n = 16
Output: true
Explanation: 24 = 16
Example 3:

Input: n = 3
Output: false


Constraints:

-231 <= n <= 231 - 1


Follow up: Could you solve it without loops/recursion?

```csharp
public static bool IsPowerOfTwo(int n)
{
        // A number is a power of two if:
        // 1. It's positive (>0)
        // 2. It has exactly one bit set in binary representation
        // 3. n & (n-1) equals 0 (clears the lowest set bit)

        return n > 0 && (n & (n - 1)) == 0;
}
```

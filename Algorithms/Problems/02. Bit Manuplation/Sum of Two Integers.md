Given two integers a and b, return the sum of the two integers without using the operators + and -.



Example 1:

Input: a = 1, b = 2
Output: 3
Example 2:

Input: a = 2, b = 3
Output: 5


Constraints:

-1000 <= a, b <= 1000



```csharp
public static int GetSum(int a, int b) {
       while (b != 0) {
            int carry = a & b;    // Find carry bits
            a = a ^ b;             // Sum without carries
            b = carry << 1;         // Shift carries left
        }
        return a;
    }
```

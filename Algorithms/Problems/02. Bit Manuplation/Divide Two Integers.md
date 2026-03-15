Given two integers dividend and divisor, divide two integers without using multiplication, division, and mod operator.

The integer division should truncate toward zero, which means losing its fractional part. For example, 8.345 would be truncated to 8, and -2.7335 would be truncated to -2.

Return the quotient after dividing dividend by divisor.

Note: Assume we are dealing with an environment that could only store integers within the 32-bit signed integer range: [−231, 231 − 1]. For this problem, if the quotient is strictly greater than 231 - 1, then return 231 - 1, and if the quotient is strictly less than -231, then return -231.



Example 1:

Input: dividend = 10, divisor = 3
Output: 3
Explanation: 10/3 = 3.33333.. which is truncated to 3.
Example 2:

Input: dividend = 7, divisor = -3
Output: -2
Explanation: 7/-3 = -2.33333.. which is truncated to -2.


Constraints:

-231 <= dividend, divisor <= 231 - 1
divisor != 0

```csharp
public static int Divide(int dividend, int divisor) {
        // Handle edge case: overflow when dividend is int.MinValue and divisor is -1
        if (dividend == int.MinValue && divisor == -1) {
            return int.MaxValue;
        }

        // Convert to long to handle int.MinValue
        long a = dividend;
        long b = divisor;

        // Determine sign
        bool isNegative = (a < 0) ^ (b < 0);

        // Use absolute values
        a = Math.Abs(a);
        b = Math.Abs(b);

        long quotient = 0;

        // Simple subtraction (but with long to prevent overflow)
        while (a >= b) {
            a -= b;
            quotient++;
        }

        // Apply sign
        if (isNegative) {
            quotient = -quotient;
        }

        return (int)quotient;
    }
```

```csharp
public int DivideOptimized(int dividend, int divisor) {
        // Handle edge case: division by zero (though not specified, it's good practice)
        if (divisor == 0) return int.MaxValue;

        // Handle edge case: overflow when dividend is int.MinValue and divisor is -1
        if (dividend == int.MinValue && divisor == -1) {
            return int.MaxValue;
        }

        // Determine sign of result
        bool isNegative = (dividend < 0) ^ (divisor < 0);

        // Convert to long to handle int.MinValue absolute value
        long absDividend = Math.Abs((long)dividend);
        long absDivisor = Math.Abs((long)divisor);

        long quotient = 0;

        // Bitwise division algorithm
        while (absDividend >= absDivisor) {
            long temp = absDivisor;
            long multiple = 1;

            // Find the largest multiple using bit shifting
            while (absDividend >= (temp << 1)) {
                temp <<= 1;
                multiple <<= 1;
            }

            // Subtract the largest chunk
            absDividend -= temp;
            quotient += multiple;
        }

        // Apply sign
        if (isNegative) {
            quotient = -quotient;
        }

        // Handle potential overflow (though we already handled the main case)
        if (quotient > int.MaxValue) {
            return int.MaxValue;
        }

        return (int)quotient;
    }
```

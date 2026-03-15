## Number of Steps to Reduce a Number to Zero
Given an integer num, return the number of steps to reduce it to zero.

In one step, if the current number is even, you have to divide it by 2, otherwise, you have to subtract 1 from it.



Example 1:

Input: num = 14
Output: 6
Explanation:
Step 1) 14 is even; divide by 2 and obtain 7.
Step 2) 7 is odd; subtract 1 and obtain 6.
Step 3) 6 is even; divide by 2 and obtain 3.
Step 4) 3 is odd; subtract 1 and obtain 2.
Step 5) 2 is even; divide by 2 and obtain 1.
Step 6) 1 is odd; subtract 1 and obtain 0.
Example 2:

Input: num = 8
Output: 4
Explanation:
Step 1) 8 is even; divide by 2 and obtain 4.
Step 2) 4 is even; divide by 2 and obtain 2.
Step 3) 2 is even; divide by 2 and obtain 1.
Step 4) 1 is odd; subtract 1 and obtain 0.
Example 3:

Input: num = 123
Output: 12


Constraints:

0 <= num <= 106


```csharp
public static int NumberOfSteps(int num) {
        int steps = 0;

        while (num > 0) {
            if (num % 2 == 0) {
                // If even, divide by 2
                num /= 2;
            } else {
                // If odd, subtract 1
                num -= 1;
            }
            steps++;
        }

        return steps;
    }
```

```csharp
public static int NumberOfSteps(int num)
{
        int steps = 0;

        while (num > 0) {
            if ((num & 1) == 0) {
                // Even - check if last bit is 0
                num >>= 1;  // Divide by 2 using right shift
            } else {
                // Odd - last bit is 1
                num -= 1;
            }
            steps++;
        }

        return steps;
    }
```

```csharp
public static int NumberOfSteps(int num) {
        if (num == 0) return 0;

        // For any number:
        // - Each '1' bit requires 2 steps (subtract 1 + divide by 2)
        // - Each '0' bit after the first 1 requires 1 step (divide by 2)
        // - Total steps = number of bits + number of 1's - 1

        int steps = 0;
        while (num > 0) {
            steps += (num & 1) == 1 ? 2 : 1;
            num >>= 1;
        }
        return steps - 1;
    }
```

## Convert a Number to Hexadecimal
Given a 32-bit integer num, return a string representing its hexadecimal representation. For negative integers, two’s complement method is used.

All the letters in the answer string should be lowercase characters, and there should not be any leading zeros in the answer except for the zero itself.

Note: You are not allowed to use any built-in library method to directly solve this problem.



Example 1:

Input: num = 26
Output: "1a"
Example 2:

Input: num = -1
Output: "ffffffff"


Constraints:

-231 <= num <= 231 - 1

```csharp
public static string ToHex(int num)
{
        if (num == 0)
        {
            return "0";
        }

        uint n = (uint)num;  // Treat as unsigned to handle two's complement
        StringBuilder hex = new StringBuilder();
     char[] hexChars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        while (n > 0)
        {
            hex.Insert(0, hexChars[n % 16]);
            n /= 16;
        }

        return hex.ToString();
}
```

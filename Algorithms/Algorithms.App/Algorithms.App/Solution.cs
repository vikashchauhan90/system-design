using System.Text;

namespace Algorithms.App;

public class ListNode
{
    public int val;
    public ListNode? next;
}


public class TreeNode
{
    public int val;
    public TreeNode? left;
    public TreeNode? right;

    public TreeNode(int value)
    {
        val = value;
    }
}
public class Solution
{
    private static string[] unitsMap = { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
    private static string[] tensMap = { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    public int Factorial(int number)
    {
        if (number < 0)
            throw new ArgumentException("Number must be non-negative.");
        if (number == 0 || number == 1)
            return 1;

        int result = 1;
        for (int i = 1; i <= number; i++)
        {
            result *= i; // Multiply result by i to get the factorial
        }
        return result; // Return the factorial of the number
    }

    public int Fibonacci(int number)
    {
        if (number <= 0)
            return 0;
        if (number == 1)
            return 1;

        int a = 0;
        int b = 1;

        for (int i = 2; i <= number; i++)
        {
            int c = a + b; // Calculate the next Fibonacci number
            a = b;
            b = c;
        }
        return b; // Return the nth Fibonacci number
    }

    public int CountBits(int number)
    {
        int count = 0;
        while (number > 0)
        {
            count += number & 1; // Increment count if the least significant bit is 1
            number >>= 1; // Right shift the bits to check the next bit
        }
        return count;
    }

    public int CountDigits(int number)
    {
        if (number == 0)
            return 1;

        int num = Math.Abs(number);

        int count = 0;

        while (num > 0)
        {
            count++;
            num /= 10; // Remove the last digit
        }

        return count;
    }

    public int SumOfDigits(int number)
    {
        int num = Math.Abs(number);
        int sum = 0;
        while (num > 0)
        {
            sum += num % 10; // Add the last digit to the sum
            num /= 10; // Remove the last digit
        }
        return sum;
    }

    public double GeometricSum(int number)
    {
        double factor = 1.0;
        double sum = 0.0;
        for (int i = 0; i <= number; i++)
        {
            sum += factor; // Add the current factor to the sum
            factor /= 2.0; // Halve the factor for the next term
        }
        return sum;
    }

    public int AddNumbers(int a, int b)
    {
        while (b != 0)
        {
            int carry = a & b; // Calculate the carry
            a = a ^ b; // Sum of bits of a and b where at least one of the bits is not set
            b = carry << 1; // Carry is shifted by one so that it can be added in the next iteration
        }
        return a;
    }

    public int ModNumbers(int a, int b)
    {
        if (b == 0)
            throw new ArgumentException("Divisor cannot be zero.");

        int remainder = a / b;

        return a - remainder * b; // Return the remainder of a divided by b
    }

    public int Divide(int dividend, int divisor)
    {
        // Handle edge case: overflow when dividend is int.MinValue and divisor is -1
        if (dividend == int.MinValue && divisor == -1)
        {
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
        while (a >= b)
        {
            a -= b;
            quotient++;
        }

        // Apply sign
        if (isNegative)
        {
            quotient = -quotient;
        }

        return (int)quotient;
    }

    public int SingleNumber(int[] nums)
    {
        int diff = 0;
        foreach (int num in nums)
        {
            diff ^= num; // XOR operation will cancel out pairs of identical numbers, leaving the single number
        }
        return diff;
    }

    public int MissingNumber(int[] nums)
    {
        int diff = 0;

        foreach (int num in nums)
        {
            diff ^= num; // XOR operation will cancel out pairs of identical numbers, leaving the missing number
        }

        for (int i = 0; i <= nums.Length; i++)
        {
            diff ^= i; // XOR with all numbers from 0 to n will cancel out pairs of identical numbers, leaving the missing number
        }

        return diff;

    }

    public int Power(int number, int power)
    {
        long exponent = power;

        if (exponent == 0)
        {
            return 1;
        }

        if (exponent < 0)
        {
            number = 1 / number; // Invert the base for negative exponent
            exponent = -exponent; // Make the exponent positive
        }

        long result = 1;
        double current = number;
        while (exponent > 0)
        {

            if ((exponent & 1) == 1)
            {
                result *= number; // If the least significant bit is 1, multiply the result by the current base
            }
            current *= current; // Square the base for the next iteration

            exponent >>= 1; // Right shift the exponent to check the next bit
        }

        return (int)result;
    }

    public string ToHex(int number)
    {
        if (number == 0)
            return "0";
        char[] hexChars = "0123456789abcdef".ToCharArray();
        StringBuilder hexBuilder = new StringBuilder();

        uint n = (uint)number;  // Treat as unsigned to handle two's complement

        while (n > 0)
        {
            int hexDigit = (int)(n & 0xF); // Get the last 4 bits or  (n % 16)
            hexBuilder.Insert(0, hexChars[hexDigit]); // Prepend the corresponding hex character
            n >>= 4; // Right shift by 4 bits to process the next hex digit or (n /= 16)
        }

        return hexBuilder.ToString();
    }

    public int ReverseBits(int n)
    {
        int result = 0;

        for (int i = 0; i < 32; i++)
        {
            // Shift result left to make room for next bit
            result <<= 1;

            // Add the least significant bit of n to result
            result |= (n & 1);

            // Shift n right to process next bit
            n >>= 1;
        }

        return result;
    }

    public int MinChanges(int number, int k)
    {
        if ((number & k) != k)
        {
            return -1; // If k has bits that are not set in number, it's impossible to change number to k
        }

        int diff = number ^ k; // XOR to find the bits that are different between number and k

        int changes = 0;

        while (diff > 0)
        {
            diff &= (diff - 1); // Remove the least significant bit that is set to 1
            changes++;
        }
        return changes;
    }

    public int MinimumFlips(int number)
    {
        string binaryString = Convert.ToString(number, 2); // Convert the number to its binary representation
        int flips = 0;
        for (int i = 1; i < binaryString.Length; i++)
        {
            if (binaryString[i] != binaryString[i - 1]) // Check if the current bit is different from the previous bit
            {
                flips++; // Increment the flip count if they are different
            }
        }
        return flips;
    }

    public char RepeatedCharacter(string s)
    {
        int see = 0;
        foreach (char c in s)
        {
            int index = c - 'a'; // Calculate the index for the character (assuming input is lowercase letters)
            int bit = (1 << index); // Create a bitmask for the character at the index
            if ((see & bit) != 0) // Check if the bit at the index is already set
            {
                return c; // If it is set, we have found the repeated character
            }
            see |= bit; // Set the bit at the index to mark that we have seen this character
        }
        return '\0'; // Return null character if no repeated character is found
    }

    public string DuplicateCharacters(string s)
    {
        char[] charCount = new char[256];
        foreach (char c in s)
        {
            charCount[c]++;
        }

        StringBuilder result = new StringBuilder();
        foreach (char c in s)
        {
            if (charCount[c] > 1)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    public string RemoveDuplicates(string s)
    {

        char[] charCount = new char[256];
        StringBuilder result = new StringBuilder();
        foreach (char c in s)
        {
            if (charCount[c] == 0) // Check if the character has not been seen before
            {
                result.Append(c); // Append the character to the result
                charCount[c]++; // Mark the character as seen
            }
        }
        return result.ToString();
    }

    public string RemoveAdjacentDuplicates(string s)
    {
        StringBuilder result = new StringBuilder();
        foreach (char c in s)
        {
            if (result.Length == 0 || result[result.Length - 1] != c) // Check if the current character is different from the last character in the result
            {
                result.Append(c); // Append the character to the result if it is different
            }
        }
        return result.ToString();
    }
    public int StringToInt(string s)
    {
        int result = 0;
        if (s.Length == 0)
        {
            return result;
        }

        int sign = 1;
        int index = 0;
        if (s[0] == '-')
        {
            sign = -1;
            index++;
        }
        else if (s[0] == '+')
        {
            index++;
        }

        for (; index < s.Length; index++)
        {
            char c = s[index];
            if (c < '0' || c > '9')
            {
                break; // Stop parsing if we encounter a non-digit character
            }
            result = result * 10 + (c - '0'); // Convert character to integer and accumulate the result
        }
        return sign * result; // Apply the sign to the result and return
    }

    public int TitleToNumber(string columnTitle)
    {
        int result = 0;
        if (string.IsNullOrEmpty(columnTitle))
        {
            return result;
        }

        for (int i = 0; i < columnTitle.Length; i++)
        {
            // convert to integer
            result = result * 26 + (columnTitle[i] - 'A' + 1);
        }

        return result;
    }

    public int LengthOfLastWord(string s)
    {
        int length = 0;
        int i = s.Length - 1;

        // Skip trailing spaces
        while (i >= 0 && s[i] == ' ') i--;

        // Count last word from end
        while (i >= 0 && s[i] != ' ')
        {
            length++;
            i--;
        }

        return length;
    }

    public int RomanToInt(string s)
    {
        Dictionary<char, int> romanValues = new()
        {
         { 'I', 1 },
         { 'V', 5 },
         { 'X', 10 },
         { 'L', 50 },
         { 'C', 100 },
         { 'D', 500 },
         { 'M', 1000 }
        };

        int result = 0;
        for (int i = 0; i < s.Length; i++)
        {
            // If current value is less than next value, subtract (cases like IV, IX, XL, etc.)
            if (i + 1 < s.Length && romanValues[s[i]] < romanValues[s[i + 1]])
            {
                result -= romanValues[s[i]];
            }
            else
            {
                result += romanValues[s[i]];
            }
        }

        return result;
    }

    public string Multiply(string num1, string num2)
    {
        // Both strings are not null and contains only positive numbers.
        if (num1 == "0" || num2 == "0")
        {
            return "0";
        }

        int m = num1.Length;
        int n = num2.Length;
        // (m + n)
        int[] result = new int[m + n];

        // right to left
        for (int i = m - 1; i >= 0; i--)
        {
            for (int j = n - 1; j >= 0; j--)
            {
                // convert to integer
                int digit1 = num1[i] - '0';
                int digit2 = num2[j] - '0';

                int mul = digit1 * digit2;

                int carryIndex = i + j;
                int valueIndex = i + j + 1;
                int sum = mul + result[valueIndex];  // This adds to existing
                                                     // (15/10 = 1)
                int carry = sum / 10;
                // (15%10 = 5)
                int digit = sum % 10;

                result[valueIndex] = digit; // Set to digit
                result[carryIndex] += carry; // Add carry (accumulate)

            }
        }

        var output = new StringBuilder();
        for (int a = 0; a < result.Length; a++)
        {
            if (output.Length == 0 && result[a] == 0)
            {
                // remove leading zeros
                continue;
            }
            output.Append(result[a]);
        }

        return output.Length > 0 ? output.ToString() : "0";
    }

    public string LongestCommonPrefix(string[] strs)
    {
        if (strs.Length == 0)
        {
            return "";
        }

        string smallStr = strs[0]; // Assume the first string is the smallest prefix

        for (int i = 0; i < smallStr.Length; i++)
        {
            for (int j = 1; j < strs.Length; j++)
            {
                if (i >= strs[j].Length || strs[j][i] != smallStr[i])
                {
                    return smallStr.Substring(0, i);
                }
            }
        }

        return smallStr;
    }

    public int LengthOfLongestSubstring(string s)
    {
        HashSet<char> seen = new();
        int left = 0;
        int maxLength = 0;
        for (int right = 0; right < s.Length; right++)
        {
            while (seen.Contains(s[right]))
            {
                seen.Remove(s[left]); // Remove the leftmost character until we can add the current character
                left++;
            }
            seen.Add(s[right]); // Add the current character to the set
            maxLength = Math.Max(maxLength, right - left + 1); // Update max length if needed
        }
        return maxLength;
    }

    public string ValidateIPv4(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return "Neither";

        foreach (var p in parts)
        {
            if (p.Length == 0 || p.Length > 3) return "Neither";
            if (p.Length > 1 && p[0] == '0') return "Neither"; // leading zero
            foreach (var ch in p) if (!char.IsDigit(ch)) return "Neither";
            if (!int.TryParse(p, out int val)) return "Neither";
            if (val < 0 || val > 255) return "Neither";
        }
        return "IPv4";
    }

    public string ValidateIPv6(string ip)
    {
        var parts = ip.Split(':');
        if (parts.Length != 8) return "Neither";

        foreach (var p in parts)
        {
            if (p.Length == 0 || p.Length > 4) return "Neither";
            foreach (var ch in p)
            {
                bool isHexDigit = (ch >= '0' && ch <= '9') ||
                                  (ch >= 'a' && ch <= 'f') ||
                                  (ch >= 'A' && ch <= 'F');
                if (!isHexDigit) return "Neither";
            }
        }
        return "IPv6";
    }

    public string CompressString(string str)
    {

        if (string.IsNullOrEmpty(str))
        {
            return str;
        }
        StringBuilder compressed = new StringBuilder();
        int count = 1;
        for (int i = 1; i < str.Length; i++)
        {
            if (str[i] == str[i - 1])
            {
                count++;
            }
            else
            {
                compressed.Append(str[i - 1]);
                compressed.Append(count);
                count = 1; // Reset count for the new character
            }
        }
        // Append the last character and its count
        compressed.Append(str[str.Length - 1]);
        compressed.Append(count);
        string compressedString = compressed.ToString();
        return compressedString.Length < str.Length ? compressedString : str;
    }

    public string DecompressString(string compressed)
    {
        if (string.IsNullOrEmpty(compressed))
        {
            return compressed;
        }
        StringBuilder decompressed = new StringBuilder();
        for (int i = 0; i < compressed.Length; i += 2)
        {
            char character = compressed[i];
            int count = compressed[i + 1] - '0'; // Convert char digit to int
            decompressed.Append(character, count); // Append the character 'count' times
        }
        return decompressed.ToString();
    }

    public void MoveZeroes(int[] nums)
    {
        int lastNonZeroIndex = 0;
        for (int i = 0; i < nums.Length; i++)
        {
            if (nums[i] != 0)
            {
                nums[lastNonZeroIndex++] = nums[i];
            }
        }

        for (int i = lastNonZeroIndex; i < nums.Length; i++)
        {
            nums[i] = 0;
        }
    }

    public bool IsValidParentheses(string s)
    {

        if (s.Length % 2 != 0)
        {
            return false;
        }

        Stack<char> braces = new Stack<char>();
        foreach (char ch in s)
        {

            if (braces.Count == 0)
            {
                braces.Push(ch);
                continue;
            }

            if (ch == ')' && braces.Peek() == '(')
            {
                braces.Pop();
            }
            else if (ch == '}' && braces.Peek() == '{')
            {
                braces.Pop();
            }
            else if (ch == ']' && braces.Peek() == '[')
            {
                braces.Pop();
            }
            else
            {
                braces.Push(ch);
            }

        }

        return braces.Count == 0;
    }

    public int EvalRPN(string[] tokens)
    {
        var stack = new Stack<int>();
        foreach (var token in tokens)
        {
            // if token is a number, push it onto the stack
            if (int.TryParse(token, out int number))
            {
                stack.Push(number);
            }
            else
            {
                // token is an operator, pop two numbers from the stack
                int b = stack.Pop();
                int a = stack.Pop();
                int result = token switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" => a / b,
                    _ => throw new InvalidOperationException("Invalid operator")
                };
                // push the result back onto the stack
                stack.Push(result);
            }
        }
        // return the final result
        return stack.Pop();

    }

    public int MajorityElement(int[] arrs)
    {
        // Moore's algorithm
        // Majority element is alway count is n/2 
        int candidate = arrs[0]; // Assuming first element is majority element
        int count = 1;

        for (int i = 1; i < arrs.Length; i++)
        {
            if (arrs[i] == candidate)
            {
                count++;
            }
            else
            {
                count--;

                if (count == 0) // change candidate element
                {
                    candidate = arrs[i];
                    count = 1;
                }
            }
        }
        return candidate;
    }

    public int[] MergeSortedArrays(int[] num1, int[] num2)
    {
        int m = num1.Length;
        int n = num2.Length;
        int[] mergedArray = new int[m + n];
        int i = 0, j = 0, k = 0;
        while (i < m && j < n)
        {
            if (num1[i] <= num2[j])
            {
                mergedArray[k++] = num1[i++];
            }
            else
            {
                mergedArray[k++] = num2[j++];
            }
        }
        // Copy remaining elements of num1, if any
        while (i < m)
        {
            mergedArray[k++] = num1[i++];
        }
        // Copy remaining elements of num2, if any
        while (j < n)
        {
            mergedArray[k++] = num2[j++];
        }
        return mergedArray;
    }

    public int MaxSubArray(int[] nums)
    {
        int currentSum = 0;
        int maxSum = int.MinValue;

        for (int i = 0; i < nums.Length; i++)
        {
            currentSum += nums[i];
            maxSum = Math.Max(currentSum, maxSum);
            if (currentSum < 0)
            {
                currentSum = 0;
            }
        }
        return maxSum;

    }

    public int[][] InvertImage(int[][] image)
    {
        int rows = image.Length;
        int cols = image[0].Length;
        int[][] invertedImage = new int[rows][];
        for (int i = 0; i < rows; i++)
        {
            invertedImage[i] = new int[cols];
            for (int j = 0; j < cols; j++)
            {
                invertedImage[i][j] = image[i][j] == 0 ? 1 : 0; // Invert the pixel value
            }
        }
        return invertedImage;
    }

    public static bool SearchSortedMatrix(int[][] matrix, int target)
    {
        int m = matrix.Length;        // rows
        int n = matrix[0].Length;      // columns

        // Start from top-right corner
        int row = 0;
        int col = n - 1;

        while (row < m && col >= 0)
        {
            if (matrix[row][col] == target)
            {
                return true;
            }

            if (matrix[row][col] < target)
            {
                // Current element too small, move down
                row++;
            }
            else
            {
                // Current element too large, move left
                col--;
            }
        }

        return false;
    }

    public List<int> FindFirstAndLastPositionOfAnElement(int[] sortedArray, int target)
    {
        var result = new List<int> { -1, -1 };
        int left = 0;
        int right = sortedArray.Length - 1;
        // Find the first occurrence
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (sortedArray[mid] >= target)
            {
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }
        // Check if the target is not found
        if (left >= sortedArray.Length || sortedArray[left] != target)
        {
            return result;
        }
        result[0] = left; // First occurrence
                          // Find the last occurrence
        right = sortedArray.Length - 1; // Reset right pointer
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (sortedArray[mid] <= target)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        result[1] = right; // Last occurrence
        return result;
    }

    public static int FindMinRotatedSortedArray(int[] nums)
    {
        int start = 0;
        int end = nums.Length - 1;

        while (start < end)
        {
            int mid = start + (end - start) / 2;

            // Minimum is in the right half
            if (nums[mid] > nums[end])
            {
                start = mid + 1;
            }
            // Minimum is in the left half (including mid)
            else
            {
                end = mid;
            }
        }

        return nums[start];
    }
    public int SearchRotatedSortedArray(int[] nums, int target)
    {
        // rotacted array binary search
        int left = 0;
        int right = nums.Length - 1;
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (nums[mid] == target)
            {
                return mid;
            }
            // Left half is sorted
            if (nums[left] <= nums[mid])
            {
                if (target >= nums[left] && target < nums[mid])
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            else
            { // Right half is sorted
                if (target > nums[mid] && target <= nums[right])
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
        }
        return -1;
    }

    public int FindKthLargest(int[] nums, int k)
    {
        PriorityQueue<int, int> minHeap = new PriorityQueue<int, int>();

        foreach (int num in nums)
        {
            minHeap.Enqueue(num, num);

            if (minHeap.Count > k)
            {
                minHeap.Dequeue();
            }
        }

        return minHeap.Peek();
    }

    public List<int> UniqueIntersection(int[] arr1, int[] arr2)
    {
        int i = 0, j = 0;
        List<int> result = new List<int>();

        while (i < arr1.Length && j < arr2.Length)
        {
            if (arr1[i] == arr2[j])
            {
                if (result.Count == 0 || result[result.Count - 1] != arr1[i])
                {
                    result.Add(arr1[i]);
                }
                i++;
                j++;
            }
            else if (arr1[i] < arr2[j]) // If current element in arr1 is smaller, move the pointer in arr1
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return result;
    }

    public ListNode? ReverseLinkedList(ListNode head)
    {
        ListNode? prev = null;
        ListNode? current = head;
        while (current != null)
        {
            ListNode? nextTemp = current.next; // Store the next node
            current.next = prev; // Reverse the current node's pointer
            prev = current; // Move prev to the current node
            current = nextTemp; // Move to the next node
        }
        return prev; // At the end, prev will be the new head of the reversed list
    }

    public bool HasCycle(ListNode head)
    {
        if (head == null) return false;
        ListNode? slow = head;
        ListNode? fast = head;
        while (fast != null && fast.next != null)
        {
            slow = slow.next; // Move slow by 1
            fast = fast.next.next; // Move fast by 2
            if (slow == fast) // A cycle is detected
            {
                return true;
            }
        }
        return false; // No cycle found
    }

    public ListNode? MergeTwoSortedLists(ListNode? l1, ListNode? l2)
    {
        ListNode dummy = new ListNode(); // Dummy node to simplify edge cases
        ListNode current = dummy;
        while (l1 != null && l2 != null)
        {
            if (l1.val < l2.val)
            {
                current.next = l1; // Link the smaller node to the merged list
                l1 = l1.next; // Move to the next node in l1
            }
            else
            {
                current.next = l2; // Link the smaller node to the merged list
                l2 = l2.next; // Move to the next node in l2
            }
            current = current.next; // Move the current pointer in the merged list
        }
        // If there are remaining nodes in either list, link them to the merged list
        if (l1 != null)
        {
            current.next = l1;
        }
        else if (l2 != null)
        {
            current.next = l2;
        }
        return dummy.next; // Return the head of the merged list (skipping the dummy node)
    }

    public ListNode? RemoveNthFromEnd(ListNode head, int n)
    {
        ListNode dummy = new ListNode { next = head }; // Dummy node to handle edge cases
        ListNode? first = dummy;
        ListNode? second = dummy;
        // Move first pointer n+1 steps ahead to maintain a gap of n between first and second
        for (int i = 0; i <= n; i++)
        {
            first = first.next;
        }
        // Move both pointers until first reaches the end
        while (first != null)
        {
            first = first.next;
            second = second.next;
        }
        // Now second is at the node before the one we want to remove
        second.next = second.next?.next; // Skip the nth node from the end
        return dummy.next; // Return the head of the modified list
    }

    public ListNode? GetIntersectionNode(ListNode? headA, ListNode? headB)
    {
        if (headA == null || headB == null) return null;
        ListNode? pointerA = headA;
        ListNode? pointerB = headB;
        while (pointerA != pointerB)
        {
            pointerA = pointerA == null ? headB : pointerA.next; // Switch to the other list when reaching the end
            pointerB = pointerB == null ? headA : pointerB.next; // Switch to the other list when reaching the end
        }
        return pointerA; // This will be the intersection node or null if there is no intersection
    }

    public ListNode? FindMiddle(ListNode head)
    {
        ListNode? slow = head;
        ListNode? fast = head;

        while (fast != null && fast.next != null)
        {
            slow = slow.next;
            fast = fast.next.next;
        }

        return slow;
    }


    public string ConvertNumberToText(int number)
    {
        if (number == 0)
            return unitsMap[0];

        if (number < 20)
            return unitsMap[number];

        if (number < 100)
            return tensMap[number / 10] + ((number % 10 > 0) ? " " + ConvertNumberToText(number % 10) : "");

        if (number < 1000)
            return unitsMap[number / 100] + " Hundred" + ((number % 100 > 0) ? " " + ConvertNumberToText(number % 100) : "");

        if (number < 100000)
            return ConvertNumberToText(number / 1000) + " Thousand" + ((number % 1000 > 0) ? " " + ConvertNumberToText(number % 1000) : "");

        return ConvertNumberToText(number / 100000) + " Lac" + ((number % 100000 > 0) ? " " + ConvertNumberToText(number % 100000) : "");
    }

    public List<object> FlattenList(IEnumerable<object> nestedList)
    {
        List<object> result = new List<object>();

        foreach (var item in nestedList)
        {
            if (item is Array && !(item is string))
            {
                result.AddRange(FlattenList((IEnumerable<object>)item));
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    public int TowerOfHanoi(int n)
    {
        // Base case
        if (n == 0)
        {
            return 0;
        }
        //Inductive Hypothesis
        int smallResult = TowerOfHanoi(n - 1);
        //Inductive Step
        return 2 * smallResult + 1;
    }

    public void PrintAllSubsequence(string input, string output)
    {
        // Base case
        if (input.Length == 0)
        {
            Console.WriteLine(output);
            return;
        }

        // Inductive Hypothesis
        char firstChar = input[0];
        string restOfString = input.Substring(1);
        // Inductive Step
        // Include the first character
        PrintAllSubsequence(restOfString, output + firstChar);
        // Exclude the first character
        PrintAllSubsequence(restOfString, output);
    }

    public void PrintTowerOfHanoiMoves(int n, char source, char destination, char auxiliary)
    {
        // Base case
        if (n == 0)
        {
            return;
        }
        //Inductive Hypothesis and Inductive Step
        // Move n-1 disks from source to auxiliary
        PrintTowerOfHanoiMoves(n - 1, source, auxiliary, destination);
        // Move the nth disk from source to destination
        Console.WriteLine($"Move disk {n} from {source} to {destination}");
        // Move n-1 disks from auxiliary to destination
        PrintTowerOfHanoiMoves(n - 1, auxiliary, destination, source);
    }

    public int Rob(int[] nums, int n)
    {
        if (n < 0)
        {
            return 0;
        }
        int includeCurrent = nums[n] + Rob(nums, n - 2); // Include the current house and skip the adjacent one
        int excludeCurrent = Rob(nums, n - 1); // Exclude the current house and consider the next one
        return Math.Max(includeCurrent, excludeCurrent);
    }

    public int ClimbStairs(int n)
    {
        // Base case
        if (n == 0 || n == 1)
        {
            return 1;
        }

        if (n == 2)
        {
            return 2;
        }

        //Inductive Hypothesis
        int waysFromNMinus1 = ClimbStairs(n - 1); // Number of ways to climb from the step just before the current step
        int waysFromNMinus2 = ClimbStairs(n - 2); // Number of ways to climb from the step two steps before the current step
        //Inductive Step
        return waysFromNMinus1 + waysFromNMinus2;
    }

    public bool CanJump(int[] nums)
    {
        int maxReachable = 0;
        for (int i = 0; i < nums.Length; i++)
        {
            if (i > maxReachable)
            {
                return false; // If the current index is beyond the maximum reachable index, we cannot jump to it
            }
            maxReachable = Math.Max(maxReachable, i + nums[i]); // Update the maximum reachable index
        }
        return true; // If we can reach or exceed the last index, return true
    }

    public int MaxProfit(int[] prices)
    {
        int minPrice = int.MaxValue;
        int maxProfit = 0;
        foreach (int price in prices)
        {
            if (price < minPrice)
            {
                minPrice = price; // Update the minimum price found so far
            }
            else if (price - minPrice > maxProfit)
            {
                maxProfit = price - minPrice; // Update the maximum profit if the current price minus the minimum price is greater than the current maximum profit
            }
        }
        return maxProfit;
    }

    public int MaxArea(int[] height)
    {
        int left = 0;
        int right = height.Length - 1;
        int maxArea = 0;
        while (left < right)
        {
            int currentArea = Math.Min(height[left], height[right]) * (right - left);
            maxArea = Math.Max(maxArea, currentArea);
            // Move the pointer that points to the shorter line
            if (height[left] < height[right])
            {
                left++;
            }
            else
            {
                right--;
            }
        }
        return maxArea;
    }



    public int TrapWater(int[] height)
    {
        if (height.Length == 0) return 0;

        int water = 0;
        int n = height.Length;
        int[] leftMax = new int[n];
        int[] rightMax = new int[n];
        leftMax[0] = height[0]; // Initialize the first element of leftMax to the first height
        rightMax[n - 1] = height[n - 1]; // Initialize the last element of rightMax to the last height

        for (int i = 1; i < n; i++)
        {
            leftMax[i] = Math.Max(leftMax[i - 1], height[i]); // Calculate the maximum height to the left of the current index
        }

        for (int i = n - 2; i >= 0; i--)
        {
            rightMax[i] = Math.Max(rightMax[i + 1], height[i]); // Calculate the maximum height to the right of the current index
        }

        for (int i = 1; i < n; i++)
        {
            water += Math.Min(leftMax[i], rightMax[i]) - height[i]; // Calculate trapped water at each index
        }

        return water;
    }

    public int OrangesRotting(int[][] grid)
    {
        int rows = grid.Length;
        int cols = grid[0].Length;

        var queue = new Queue<(int, int, int)>(); // (row, col, time)
        int freshCount = 0;

        // Find all rotten oranges and count fresh ones
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (grid[i][j] == 2)
                {
                    queue.Enqueue((i, j, 0));
                }
                else if (grid[i][j] == 1)
                {
                    freshCount++;
                }
            }
        }

        // If no fresh oranges, return 0
        if (freshCount == 0) return 0;

        // BFS to rot adjacent oranges
        int[][] directions = { new[] { -1, 0 }, new[] { 1, 0 },
                          new[] { 0, -1 }, new[] { 0, 1 } };
        int maxTime = 0;

        while (queue.Count > 0)
        {
            var (row, col, time) = queue.Dequeue();
            maxTime = Math.Max(maxTime, time);

            // Check all 4 directions
            foreach (var dir in directions)
            {
                int newRow = row + dir[0];
                int newCol = col + dir[1];

                // If within bounds and fresh, rot it
                if (newRow >= 0 && newRow < rows &&
                    newCol >= 0 && newCol < cols &&
                    grid[newRow][newCol] == 1)
                {
                    grid[newRow][newCol] = 2;
                    queue.Enqueue((newRow, newCol, time + 1));
                    freshCount--;
                }
            }
        }

        // If fresh oranges remain, return -1
        return freshCount == 0 ? maxTime : -1;
    }
    public bool CanCompleteCircuit(int[] gas, int[] cost)
    {
        int totalGas = 0;
        int totalCost = 0;
        for (int i = 0; i < gas.Length; i++)
        {
            totalGas += gas[i];
            totalCost += cost[i];
        }
        if (totalGas < totalCost)
        {
            return false; // If total gas is less than total cost, it's impossible to complete the circuit
        }
        int currentGas = 0;
        int startIndex = 0;
        for (int i = 0; i < gas.Length; i++)
        {
            currentGas += gas[i] - cost[i]; // Calculate the net gas after visiting the current station
            if (currentGas < 0)
            {
                startIndex = i + 1; // Move the starting index to the next station
                currentGas = 0; // Reset current gas for the new starting point
            }
        }
        return true; // If we can complete the circuit, return true
    }

    public int[] SortKSortedArray(int[] arr, int k)
    {
        int n = arr.Length;
        PriorityQueue<int, int> minHeap = new PriorityQueue<int, int>();

        // Step 1: Add first k+1 elements
        for (int i = 0; i <= k && i < n; i++)
        {
            minHeap.Enqueue(arr[i], arr[i]);
        }

        int index = 0;

        // Step 2: Process rest
        for (int i = k + 1; i < n; i++)
        {
            arr[index++] = minHeap.Dequeue();
            minHeap.Enqueue(arr[i], arr[i]);
        }

        // Step 3: Empty remaining heap
        while (minHeap.Count > 0)
        {
            arr[index++] = minHeap.Dequeue();
        }

        return arr;
    }


    public List<int> InorderTraversal(TreeNode root)
    {
        Stack<TreeNode> stack = new Stack<TreeNode>();
        List<int> result = new List<int>();
        TreeNode? current = root;
        while (stack.Count > 0 || current != null)
        {
            while (current != null)
            {
                stack.Push(current);
                current = current.left;
            }
            current = stack.Pop();
            result.Add(current.val);
            current = current.right;
        }
        return result;
    }

    public List<int> PreorderTraversal(TreeNode root)
    {
        Stack<TreeNode> stack = new Stack<TreeNode>();
        List<int> result = new List<int>();
        if (root != null)
        {
            stack.Push(root);
        }
        while (stack.Count > 0)
        {
            TreeNode current = stack.Pop();
            result.Add(current.val);
            if (current.right != null)
            {
                stack.Push(current.right);
            }
            if (current.left != null)
            {
                stack.Push(current.left);
            }
        }
        return result;
    }

    public List<int> PostorderTraversal(TreeNode root)
    {
        Stack<TreeNode> stack = new Stack<TreeNode>();
        List<int> result = new List<int>();
        TreeNode? current = root;
        TreeNode? lastVisited = null;
        while (stack.Count > 0 || current != null)
        {
            if (current != null)
            {
                stack.Push(current);
                current = current.left;
            }
            else
            {
                TreeNode peekNode = stack.Peek();
                if (peekNode.right != null && lastVisited != peekNode.right)
                {
                    current = peekNode.right;
                }
                else
                {
                    result.Add(peekNode.val);
                    lastVisited = stack.Pop();
                }
            }
        }
        return result;
    }

    public int MaxDepth(TreeNode root)
    {
        if (root == null)
        {
            return 0; // Base case: the depth of an empty tree is 0
        }
        int leftDepth = MaxDepth(root.left); // Recursive call to find the depth of the left subtree
        int rightDepth = MaxDepth(root.right); // Recursive call to find the depth of the right subtree
        return Math.Max(leftDepth, rightDepth) + 1; // Return the maximum of left and right depths plus one for the current node
    }

    public List<int> FindKLargest(TreeNode root, int k)
    {
        Stack<TreeNode> stack = new Stack<TreeNode>();
        List<int> result = new List<int>();
        TreeNode? current = root;
        while (stack.Count > 0 || current != null)
        {
            while (current != null)
            {
                stack.Push(current);
                current = current.right;
            }

            current = stack.Pop();
            result.Add(current.val);

            if (result.Count == k)
                break;

            current = current.left;
        }

        return result;
    }
}

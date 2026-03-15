## Flipping an Image
Given an n x n binary matrix image, flip the image horizontally, then invert it, and return the resulting image.

To flip an image horizontally means that each row of the image is reversed.

For example, flipping [1,1,0] horizontally results in [0,1,1].
To invert an image means that each 0 is replaced by 1, and each 1 is replaced by 0.

For example, inverting [0,1,1] results in [1,0,0].


Example 1:

Input: image = [[1,1,0],[1,0,1],[0,0,0]]
Output: [[1,0,0],[0,1,0],[1,1,1]]
Explanation: First reverse each row: [[0,1,1],[1,0,1],[0,0,0]].
Then, invert the image: [[1,0,0],[0,1,0],[1,1,1]]
Example 2:

Input: image = [[1,1,0,0],[1,0,0,1],[0,1,1,1],[1,0,1,0]]
Output: [[1,1,0,0],[0,1,1,0],[0,0,0,1],[1,0,1,0]]
Explanation: First reverse each row: [[0,0,1,1],[1,0,0,1],[1,1,1,0],[0,1,0,1]].
Then invert the image: [[1,1,0,0],[0,1,1,0],[0,0,0,1],[1,0,1,0]]


Constraints:

n == image.length
n == image[i].length
1 <= n <= 20
images[i][j] is either 0 or 1.


```csharp
public static int[][] FlipAndInvertImage(int[][] image)
{
    int n = image.Length;

        for (int row = 0; row < n; row++) {
            int left = 0;
            int right = n - 1;

            while (left <= right) {
                // Swap and invert in one step
                if (image[row][left] == image[row][right]) {
                    // If both are same, they both flip (0→1, 1→0)
                    image[row][left] = image[row][left] == 0 ? 1 : 0;
                    image[row][right] = image[row][left]; // They become the same
                }
                // If they are different, swapping + inverting cancels out
                // So we just leave them as is (they effectively swap places)

                left++;
                right--;
            }
        }

        return image;
}
```


# Unique Number Generator
```C#
public class UniqueNumberGenerator
{
    private HashSet<int> generatedNumbers;
    private Random random;

    public UniqueNumberGenerator()
    {
        generatedNumbers = new HashSet<int>();
        random = new Random();
    }

    public int GenerateUniqueNumber()
    {
        int number;

        // Generate a new number until we get one that hasn't been generated before
        do
        {
            number = random.Next();
        }
        while (generatedNumbers.Contains(number));

        // Add the unique number to the set of generated numbers
        generatedNumbers.Add(number);

        return number;
    }
}

```
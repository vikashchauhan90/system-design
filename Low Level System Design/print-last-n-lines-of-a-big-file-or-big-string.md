# Print last `n` lines of a big file or big string.

```C#
public static void PrintLastLines(string fileName, int n)
{
    LinkedList<string> lines = new LinkedList<string>();
    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
    {
        using (StreamReader sr = new StreamReader(fs))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.AddLast(line);
                if (lines.Count > n)
                    lines.RemoveFirst(); //remove first line.
            }
        }
    }
    foreach (string line in lines)
        Console.WriteLine(line);
}

```
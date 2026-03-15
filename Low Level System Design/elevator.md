# Elevator

```C#

public class Elevator
{
    private int _currentFloor = 0;
    private Queue<int> _requests = new Queue<int>();

    public void RequestFloor(int floor)
    {
        _requests.Enqueue(floor);
    }

    public void ProcessRequests()
    {
        while (_requests.Count > 0)
        {
            var targetFloor = _requests.Dequeue();
            MoveToFloor(targetFloor);
        }
    }

    private void MoveToFloor(int floor)
    {
        Console.WriteLine($"Elevator moving from floor {_currentFloor} to floor {floor}");
        _currentFloor = floor;
        Console.WriteLine($"Elevator arrived at floor {_currentFloor}");
    }
}

```
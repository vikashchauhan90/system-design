# Event Calendar

```C#
using System;
using System.Collections.Generic;
using System.Linq;

public class Event
{
    public string Title { get; private set; }
    public DateTime Start { get; private set; }
    public DateTime End { get; private set; }

    public Event(string title, DateTime start, DateTime end)
    {
        Title = title;
        Start = start;
        End = end;
    }
}

public class Calendar
{
    private List<Event> _events = new List<Event>();

    public bool HasOverlap(Event eventToAdd)
    {
        return _events.Any(e => (e.Start <= eventToAdd.End) && (e.End >= eventToAdd.Start));
    }

    public void AddEvent(Event eventToAdd)
    {
        if (HasOverlap(eventToAdd))
        {
            Console.WriteLine($"Cannot add event {eventToAdd.Title}. It overlaps with another event.");
            return;
        }

        _events.Add(eventToAdd);
    }

    public List<Event> GetEventsOnDate(DateTime date)
    {
        return _events.Where(e => e.Start.Date <= date.Date && e.End.Date >= date.Date).ToList();
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var calendar = new Calendar();

        var event1 = new Event("Meeting with Bob", new DateTime(2024, 5, 1, 10, 0, 0), new DateTime(2024, 5, 1, 11, 0, 0));
        var event2 = new Event("Dentist appointment", new DateTime(2024, 5, 1, 10, 30, 0), new DateTime(2024, 5, 1, 11, 30, 0));
        var event3 = new Event("Lunch with Alice", new DateTime(2024, 5, 1, 12, 0, 0), new DateTime(2024, 5, 1, 13, 0, 0));

        calendar.AddEvent(event1);
        calendar.AddEvent(event2);  // This should fail because it overlaps with event1
        calendar.AddEvent(event3);

        var eventsOnMay1 = calendar.GetEventsOnDate(new DateTime(2024, 5, 1));

        foreach (var eventOnMay1 in eventsOnMay1)
        {
            Console.WriteLine($"{eventOnMay1.Start} to {eventOnMay1.End}: {eventOnMay1.Title}");
        }
    }
}

```
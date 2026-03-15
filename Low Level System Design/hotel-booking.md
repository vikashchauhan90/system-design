# Hotel Booking

```C#
using System;
using System.Collections.Generic;
using System.Linq;

public enum RoomType
{
    DeluxeSingleBed,
    DeluxeDoubleBed
}

public class Room
{
    public int Number { get; private set; }
    public RoomType Type { get; private set; }

    public Room(int number, RoomType type)
    {
        Number = number;
        Type = type;
    }
}

public class Booking
{
    public Room Room { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    public Booking(Room room, DateTime startDate, DateTime endDate)
    {
        Room = room;
        StartDate = startDate;
        EndDate = endDate;
    }
}

public class Hotel
{
    private List<Room> _rooms = new List<Room>();
    private List<Booking> _bookings = new List<Booking>();

    public Hotel(List<Room> rooms)
    {
        _rooms = rooms;
    }

    public bool IsRoomAvailable(RoomType roomType, DateTime startDate, DateTime endDate)
    {
        return !_bookings.Any(b => b.Room.Type == roomType && b.StartDate < endDate && b.EndDate > startDate);
    }

    public void AddBooking(RoomType roomType, DateTime startDate, DateTime endDate)
    {
        if (!IsRoomAvailable(roomType, startDate, endDate))
        {
            Console.WriteLine($"Cannot add booking. No available {roomType} from {startDate} to {endDate}.");
            return;
        }

        var room = _rooms.First(r => r.Type == roomType && !_bookings.Any(b => b.Room.Number == r.Number && b.StartDate < endDate && b.EndDate > startDate));
        var booking = new Booking(room, startDate, endDate);
        _bookings.Add(booking);
    }

    public List<Booking> GetBookingsForRoomType(RoomType roomType)
    {
        return _bookings.Where(b => b.Room.Type == roomType).ToList();
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var rooms = new List<Room>
        {
            new Room(1, RoomType.DeluxeSingleBed),
            new Room(2, RoomType.DeluxeSingleBed),
            new Room(3, RoomType.DeluxeDoubleBed),
            // Add more rooms as needed
        };

        var hotel = new Hotel(rooms);

        hotel.AddBooking(RoomType.DeluxeSingleBed, new DateTime(2024, 5, 1), new DateTime(2024, 5, 10));
        hotel.AddBooking(RoomType.DeluxeSingleBed, new DateTime(2024, 5, 5), new DateTime(2024, 5, 15));  // This should fail because it overlaps with the previous booking

        var bookingsForDeluxeSingleBed = hotel.GetBookingsForRoomType(RoomType.DeluxeSingleBed);

        foreach (var booking in bookingsForDeluxeSingleBed)
        {
            Console.WriteLine($"{booking.Room.Type} is booked from {booking.StartDate} to {booking.EndDate}");
        }
    }
}

```
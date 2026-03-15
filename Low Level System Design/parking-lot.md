# ParkingLot

Parking lot system, each floor has parking slots and ramps to move on to the next floor, and each floor has two sensors, one for detech check-in and another for checkout.  One more important point: there is an entry gate and one exit gate on the ground floor, and there is one direct movement of vichels, like within the same floor, moving from left to right, not right to left. one direct way to move, and the user needs to complete a cycle to move on next floor as well. This is one of the important points, and each floor has a number of slots. We also need to track available slots. Design an algorithm to get the available slots. 

## Use case diagram
Here are the main Actors in our system:
* **Admin:** Mainly responsible for adding and modifying parking floors, parking slots,
entrance and exit panels, adding/removing parking attendants, etc.
* **Customer:** All customers can get a parking ticket and pay for it.
* **Parking attendant:** Parking attendants can do all the activities on the customer’s
behalf, and can take cash for ticket payment.
* **System:** To show messages on to different info panels, as well as assigning and
removing a vehicle from a parking slot.

```C#
public class ParkingLot
{
    private int numberOfFloors;
    private List<Floor> floors;

    public ParkingLot(int numberOfFloors)
    {
        this.numberOfFloors = numberOfFloors;
        floors = new List<Floor>();

        for (int i = 0; i < numberOfFloors; i++)
        {
            floors.Add(new Floor(i));
        }
    }

    public void SensorDataReceived(int floorNumber, string sensorType)
    {
        if (sensorType == "A") // Sensor A indicates a vehicle has entered a slot
        {
            if (floors[floorNumber].GetAvailableSlots() > 0)
            {
                floors[floorNumber].DecreaseAvailableSlots();
            }
            else if (floorNumber < numberOfFloors - 1) // If the current floor is full, check the next floor
            {
                SensorDataReceived(floorNumber + 1, sensorType);
            }
            else
            {
                Console.WriteLine("Parking lot is full.");
            }
        }
        else if (sensorType == "B") // Sensor B indicates a vehicle has left a slot
        {
            floors[floorNumber].IncreaseAvailableSlots();
        }
    }

    public void DisplayAvailableSlots()
    {
        foreach (var floor in floors)
        {
            Console.WriteLine("Floor " + floor.GetFloorNumber() + ": " + floor.GetAvailableSlots() + " slots available.");
        }
    }
}

public class Floor
{
    private int floorNumber;
    private int availableSlots;

    public Floor(int floorNumber)
    {
        this.floorNumber = floorNumber;
        this.availableSlots = 10; // Assuming each floor has 10 slots
    }

    public void DecreaseAvailableSlots()
    {
        if (availableSlots > 0)
        {
            availableSlots--;
        }
    }

    public void IncreaseAvailableSlots()
    {
        if (availableSlots < 10) // Assuming each floor has 10 slots
        {
            availableSlots++;
        }
    }

    public int GetAvailableSlots()
    {
        return availableSlots;
    }

    public int GetFloorNumber()
    {
        return floorNumber;
    }
}

```

## Parking Lot

Please design a parking lot system. There are three types of slots: small, compact, and large. A bike can park in any type of slot, and a car can park in a compact or large spot, but a bus can only park in a large spot. 

## Use case diagram
Here are the main Actors in our system:
* **Admin:** Mainly responsible for adding and modifying parking floors, parking slots,
entrance and exit panels, adding/removing parking attendants, etc.
* **Customer:** All customers can get a parking ticket and pay for it.
* **Parking attendant:** Parking attendants can do all the activities on the customer’s
behalf, and can take cash for ticket payment.
* **System:** To show messages on to different info panels, as well as assigning and
removing a vehicle from a parking slot.

```C#

public enum VehicleSize
{
    Small,
    Compact,
    Large
}

public abstract class Vehicle
{
    protected Vehicle(string number)
    {
        this.Number = number;
    }
    public string Number { get; }
    public abstract VehicleSize Size { get; }

}

public class Bike : Vehicle
{     
    public Bike(string number):base(number)
    {
    }
    public override VehicleSize Size => VehicleSize.Small;

}

public class Car : Vehicle
{
    public Car(string number) : base(number)
    {
    }
    public override VehicleSize Size => VehicleSize.Compact;
}

public class Bus : Vehicle
{
    public Bus(string number) : base(number)
    {
    }
    public override VehicleSize Size => VehicleSize.Large;
}

public class ParkingSpot
{
    public VehicleSize Size { get; private set; }
    public Vehicle CurrentVehicle { get; private set; }

    public ParkingSpot(VehicleSize size)
    {
        Size = size;
    }

    public bool IsAvailable=> CurrentVehicle == null;

    public bool CanFitVehicle(Vehicle vehicle)
    {
        return IsAvailable && (int)vehicle.Size <= (int)Size;
    }

    public bool ParkVehicle(Vehicle vehicle)
    {
        if (!CanFitVehicle(vehicle))
        {
            return false;
        }

        CurrentVehicle = vehicle;
        return true;
    }

    public void RemoveVehicle()
    {
        CurrentVehicle = null;
    }
}

public class ParkingFloor
{
    private List<ParkingSpot> spots;

    public ParkingFloor(int numSpots)
    {
        spots = new List<ParkingSpot>(numSpots);
    }

    public bool ParkVehicle(Vehicle vehicle)
    {
        foreach (var spot in spots)
        {
            if (spot.CanFitVehicle(vehicle))
            {
                return spot.ParkVehicle(vehicle);
            }
        }

        return false;
    }
}

public class ParkingLot
{
    private List<ParkingFloor> floors;

    public ParkingLot(int numFloors, int numSpotsPerFloor)
    {
        floors = new List<ParkingFloor>(numFloors);

        for (int i = 0; i < numFloors; i++)
        {
            floors.Add(new ParkingFloor(numSpotsPerFloor));
        }
    }

    public bool ParkVehicle(Vehicle vehicle)
    {
        foreach (var floor in floors)
        {
            if (floor.ParkVehicle(vehicle))
            {
                return true;
            }
        }

        return false;
    }
}

```
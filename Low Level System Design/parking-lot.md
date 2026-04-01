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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingLotSystem
{
    #region Enums and Constants

    public enum VehicleSize
    {
        Small = 1,    // Bike
        Compact = 2,  // Car
        Large = 3     // Bus
    }

    public enum SlotType
    {
        Small = 1,    // For bikes only
        Compact = 2,  // For bikes and cars
        Large = 3     // For all vehicles
    }

    public enum SensorType
    {
        Entry,    // Entry sensor (check-in)
        Exit      // Exit sensor (check-out)
    }

    public enum MovementDirection
    {
        LeftToRight,
        RightToLeft
    }

    public enum TicketStatus
    {
        Active,
        Paid,
        Completed
    }

    #endregion

    #region Vehicle Classes

    public abstract class Vehicle
    {
        protected Vehicle(string licenseNumber, string ownerName = null)
        {
            LicenseNumber = licenseNumber;
            OwnerName = ownerName;
            EntryTime = DateTime.Now;
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }
        public string LicenseNumber { get; }
        public string OwnerName { get; }
        public DateTime EntryTime { get; private set; }
        public DateTime? ExitTime { get; private set; }
        public abstract VehicleSize Size { get; }
        public virtual SlotType RequiredSlotType => SlotType.Small;

        public void Exit()
        {
            ExitTime = DateTime.Now;
        }

        public double GetParkingDurationHours()
        {
            var endTime = ExitTime ?? DateTime.Now;
            return (endTime - EntryTime).TotalHours;
        }
    }

    public class Bike : Vehicle
    {
        public Bike(string licenseNumber, string ownerName = null) : base(licenseNumber, ownerName)
        {
        }

        public override VehicleSize Size => VehicleSize.Small;
        public override SlotType RequiredSlotType => SlotType.Small;
    }

    public class Car : Vehicle
    {
        public Car(string licenseNumber, string ownerName = null) : base(licenseNumber, ownerName)
        {
        }

        public override VehicleSize Size => VehicleSize.Compact;
        public override SlotType RequiredSlotType => SlotType.Compact;
    }

    public class Bus : Vehicle
    {
        public Bus(string licenseNumber, string ownerName = null) : base(licenseNumber, ownerName)
        {
        }

        public override VehicleSize Size => VehicleSize.Large;
        public override SlotType RequiredSlotType => SlotType.Large;
    }

    #endregion

    #region Parking Spot

    public class ParkingSpot
    {
        public string Id { get; }
        public int FloorNumber { get; }
        public int SlotNumber { get; }
        public SlotType Type { get; }
        public bool IsAvailable { get; private set; }
        public Vehicle CurrentVehicle { get; private set; }
        public DateTime? OccupiedSince { get; private set; }

        public ParkingSpot(int floorNumber, int slotNumber, SlotType type)
        {
            Id = Guid.NewGuid().ToString();
            FloorNumber = floorNumber;
            SlotNumber = slotNumber;
            Type = type;
            IsAvailable = true;
        }

        public bool CanFitVehicle(Vehicle vehicle)
        {
            if (!IsAvailable) return false;

            return vehicle.Size switch
            {
                VehicleSize.Small => true, // Bikes can park anywhere
                VehicleSize.Compact => Type == SlotType.Compact || Type == SlotType.Large,
                VehicleSize.Large => Type == SlotType.Large,
                _ => false
            };
        }

        public bool ParkVehicle(Vehicle vehicle)
        {
            if (!CanFitVehicle(vehicle)) return false;

            CurrentVehicle = vehicle;
            IsAvailable = false;
            OccupiedSince = DateTime.Now;
            return true;
        }

        public Vehicle RemoveVehicle()
        {
            var vehicle = CurrentVehicle;
            CurrentVehicle = null;
            IsAvailable = true;
            OccupiedSince = null;
            return vehicle;
        }

        public override string ToString()
        {
            return $"Floor {FloorNumber}, Slot {SlotNumber}, Type: {Type}, Available: {IsAvailable}";
        }
    }

    #endregion

    #region Parking Ticket

    public class ParkingTicket
    {
        public string TicketId { get; }
        public string VehicleLicenseNumber { get; }
        public string VehicleType { get; }
        public int FloorNumber { get; }
        public int SlotNumber { get; }
        public DateTime EntryTime { get; }
        public DateTime? ExitTime { get; private set; }
        public double Amount { get; private set; }
        public TicketStatus Status { get; private set; }
        public string PaymentMethod { get; private set; }

        public ParkingTicket(Vehicle vehicle, int floorNumber, int slotNumber)
        {
            TicketId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            VehicleLicenseNumber = vehicle.LicenseNumber;
            VehicleType = vehicle.GetType().Name;
            FloorNumber = floorNumber;
            SlotNumber = slotNumber;
            EntryTime = DateTime.Now;
            Status = TicketStatus.Active;
        }

        public void Pay(double amount, string paymentMethod)
        {
            Amount = amount;
            PaymentMethod = paymentMethod;
            Status = TicketStatus.Paid;
        }

        public void Complete()
        {
            ExitTime = DateTime.Now;
            Status = TicketStatus.Completed;
        }

        public double CalculateAmount(double hourlyRate)
        {
            var duration = (DateTime.Now - EntryTime).TotalHours;
            return Math.Ceiling(duration) * hourlyRate;
        }

        public override string ToString()
        {
            return $"Ticket: {TicketId} | Vehicle: {VehicleLicenseNumber} | Floor: {FloorNumber} | Slot: {SlotNumber} | Status: {Status}";
        }
    }

    #endregion

    #region Floor with One-Way Movement

    public class ParkingFloor
    {
        private readonly int _floorNumber;
        private readonly List<ParkingSpot> _spots;
        private readonly Dictionary<SlotType, int> _availableSpotsByType;
        private readonly object _lock = new object();
        private readonly Queue<Vehicle> _waitingQueue;
        
        // One-way movement tracking
        private bool _isCycleCompleted;
        private int _currentPosition;
        private readonly MovementDirection _allowedDirection;
        
        // Sensors
        private readonly Sensor _entrySensor;
        private readonly Sensor _exitSensor;
        
        public int FloorNumber => _floorNumber;
        public bool HasAvailableSpots => _availableSpotsByType.Values.Any(count => count > 0);
        public int TotalSpots => _spots.Count;
        
        // Events
        public event EventHandler<ParkingSpot> SpotOccupied;
        public event EventHandler<ParkingSpot> SpotVacated;

        public ParkingFloor(int floorNumber, MovementDirection direction = MovementDirection.LeftToRight)
        {
            _floorNumber = floorNumber;
            _allowedDirection = direction;
            _spots = new List<ParkingSpot>();
            _availableSpotsByType = new Dictionary<SlotType, int>();
            _waitingQueue = new Queue<Vehicle>();
            _isCycleCompleted = true;
            _currentPosition = 0;
            
            _entrySensor = new Sensor(floorNumber, SensorType.Entry);
            _exitSensor = new Sensor(floorNumber, SensorType.Exit);
            
            InitializeSlotTypes();
        }

        private void InitializeSlotTypes()
        {
            foreach (SlotType type in Enum.GetValues(typeof(SlotType)))
            {
                _availableSpotsByType[type] = 0;
            }
        }

        public void AddParkingSpot(SlotType type)
        {
            var spot = new ParkingSpot(_floorNumber, _spots.Count + 1, type);
            _spots.Add(spot);
            _availableSpotsByType[type]++;
        }

        public int GetAvailableSpotsByType(SlotType type)
        {
            lock (_lock)
            {
                return _availableSpotsByType.ContainsKey(type) ? _availableSpotsByType[type] : 0;
            }
        }

        public int GetTotalAvailableSpots()
        {
            lock (_lock)
            {
                return _availableSpotsByType.Values.Sum();
            }
        }

        public ParkingSpot FindAvailableSpot(Vehicle vehicle)
        {
            lock (_lock)
            {
                // One-way movement: start from current position and move in allowed direction
                var spotsToCheck = new List<ParkingSpot>();
                
                if (_allowedDirection == MovementDirection.LeftToRight)
                {
                    for (int i = _currentPosition; i < _spots.Count; i++)
                        spotsToCheck.Add(_spots[i]);
                    for (int i = 0; i < _currentPosition; i++)
                        spotsToCheck.Add(_spots[i]);
                }
                else
                {
                    for (int i = _currentPosition; i >= 0; i--)
                        spotsToCheck.Add(_spots[i]);
                    for (int i = _spots.Count - 1; i > _currentPosition; i--)
                        spotsToCheck.Add(_spots[i]);
                }

                // Find first available spot that fits the vehicle
                foreach (var spot in spotsToCheck)
                {
                    if (spot.CanFitVehicle(vehicle))
                    {
                        _currentPosition = spot.SlotNumber - 1;
                        return spot;
                    }
                }

                return null;
            }
        }

        public bool ParkVehicle(Vehicle vehicle, out ParkingSpot assignedSpot)
        {
            assignedSpot = null;
            
            lock (_lock)
            {
                var spot = FindAvailableSpot(vehicle);
                if (spot != null && spot.ParkVehicle(vehicle))
                {
                    _availableSpotsByType[spot.Type]--;
                    assignedSpot = spot;
                    
                    // Trigger sensor
                    _entrySensor.Trigger(vehicle);
                    OnSpotOccupied(spot);
                    
                    return true;
                }
                
                // Add to waiting queue if no spot available
                _waitingQueue.Enqueue(vehicle);
                return false;
            }
        }

        public bool RemoveVehicle(string spotId, out Vehicle vehicle)
        {
            vehicle = null;
            
            lock (_lock)
            {
                var spot = _spots.FirstOrDefault(s => s.Id == spotId);
                if (spot != null && !spot.IsAvailable)
                {
                    vehicle = spot.RemoveVehicle();
                    _availableSpotsByType[spot.Type]++;
                    
                    // Trigger exit sensor
                    _exitSensor.Trigger(vehicle);
                    OnSpotVacated(spot);
                    
                    // Process waiting queue
                    ProcessWaitingQueue();
                    
                    return true;
                }
            }
            
            return false;
        }

        private void ProcessWaitingQueue()
        {
            while (_waitingQueue.Count > 0 && HasAvailableSpots)
            {
                var waitingVehicle = _waitingQueue.Peek();
                if (FindAvailableSpot(waitingVehicle) != null)
                {
                    _waitingQueue.Dequeue();
                    ParkVehicle(waitingVehicle, out _);
                }
                else
                {
                    break;
                }
            }
        }

        public void CompleteCycle()
        {
            _isCycleCompleted = true;
            _currentPosition = 0;
        }

        public bool MoveToNextFloor()
        {
            if (!_isCycleCompleted)
                return false;
            
            _isCycleCompleted = false;
            return true;
        }

        protected virtual void OnSpotOccupied(ParkingSpot spot)
        {
            SpotOccupied?.Invoke(this, spot);
        }

        protected virtual void OnSpotVacated(ParkingSpot spot)
        {
            SpotVacated?.Invoke(this, spot);
        }

        public Dictionary<SlotType, int> GetAvailabilityReport()
        {
            lock (_lock)
            {
                return new Dictionary<SlotType, int>(_availableSpotsByType);
            }
        }
    }

    #endregion

    #region Sensor

    public class Sensor
    {
        public int FloorNumber { get; }
        public SensorType Type { get; }
        public DateTime LastTriggered { get; private set; }
        public Vehicle LastVehicle { get; private set; }
        
        public event EventHandler<SensorEventArgs> SensorTriggered;

        public Sensor(int floorNumber, SensorType type)
        {
            FloorNumber = floorNumber;
            Type = type;
        }

        public void Trigger(Vehicle vehicle)
        {
            LastTriggered = DateTime.Now;
            LastVehicle = vehicle;
            
            var args = new SensorEventArgs
            {
                FloorNumber = FloorNumber,
                SensorType = Type,
                Vehicle = vehicle,
                Timestamp = LastTriggered
            };
            
            SensorTriggered?.Invoke(this, args);
            
            Console.WriteLine($"[Sensor] Floor {FloorNumber} - {Type} sensor triggered by {vehicle.LicenseNumber} at {LastTriggered:HH:mm:ss}");
        }
    }

    public class SensorEventArgs : EventArgs
    {
        public int FloorNumber { get; set; }
        public SensorType SensorType { get; set; }
        public Vehicle Vehicle { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion

    #region Entry and Exit Gates

    public class EntryGate
    {
        public int GateId { get; }
        public bool IsOpen { get; private set; }
        private readonly ParkingLot _parkingLot;
        private readonly List<ParkingTicket> _issuedTickets;

        public EntryGate(int gateId, ParkingLot parkingLot)
        {
            GateId = gateId;
            _parkingLot = parkingLot;
            _issuedTickets = new List<ParkingTicket>();
            IsOpen = true;
        }

        public ParkingTicket IssueTicket(Vehicle vehicle)
        {
            if (!IsOpen)
                throw new InvalidOperationException("Entry gate is closed");

            var result = _parkingLot.ParkVehicle(vehicle);
            if (result.Success)
            {
                var ticket = new ParkingTicket(vehicle, result.FloorNumber, result.SlotNumber);
                _issuedTickets.Add(ticket);
                Console.WriteLine($"[Entry Gate {GateId}] Issued ticket {ticket.TicketId} for {vehicle.LicenseNumber}");
                return ticket;
            }
            
            throw new InvalidOperationException("No parking spots available");
        }

        public void Close() => IsOpen = false;
        public void Open() => IsOpen = true;
    }

    public class ExitGate
    {
        public int GateId { get; }
        public bool IsOpen { get; private set; }
        private readonly ParkingLot _parkingLot;

        public ExitGate(int gateId, ParkingLot parkingLot)
        {
            GateId = gateId;
            _parkingLot = parkingLot;
            IsOpen = true;
        }

        public bool ProcessExit(ParkingTicket ticket, double amount, string paymentMethod)
        {
            if (!IsOpen)
                throw new InvalidOperationException("Exit gate is closed");

            ticket.Pay(amount, paymentMethod);
            
            var success = _parkingLot.RemoveVehicle(ticket.FloorNumber, ticket.SlotNumber);
            
            if (success)
            {
                ticket.Complete();
                Console.WriteLine($"[Exit Gate {GateId}] Processed exit for ticket {ticket.TicketId}. Amount: ${amount:F2}");
                return true;
            }
            
            return false;
        }

        public void Close() => IsOpen = false;
        public void Open() => IsOpen = true;
    }

    #endregion

    #region Parking Lot Main Class

    public class ParkingResult
    {
        public bool Success { get; set; }
        public int FloorNumber { get; set; }
        public int SlotNumber { get; set; }
        public string Message { get; set; }
    }

    public class ParkingLot
    {
        private readonly List<ParkingFloor> _floors;
        private readonly Dictionary<string, ParkingTicket> _activeTickets;
        private readonly object _lock = new object();
        private readonly double _hourlyRate;
        
        // Gates
        private readonly List<EntryGate> _entryGates;
        private readonly List<ExitGate> _exitGates;
        
        // Statistics
        private int _totalVehiclesServed;
        private double _totalRevenue;
        
        public int TotalFloors => _floors.Count;
        public int TotalSpots => _floors.Sum(f => f.TotalSpots);
        
        // Events
        public event EventHandler<ParkingResult> VehicleParked;
        public event EventHandler<string> VehicleExited;

        public ParkingLot(int numberOfFloors, int spotsPerFloor, double hourlyRate = 5.0)
        {
            _hourlyRate = hourlyRate;
            _floors = new List<ParkingFloor>();
            _activeTickets = new Dictionary<string, ParkingTicket>();
            _entryGates = new List<EntryGate>();
            _exitGates = new List<ExitGate>();
            
            InitializeFloors(numberOfFloors, spotsPerFloor);
            InitializeGates();
        }

        private void InitializeFloors(int numberOfFloors, int spotsPerFloor)
        {
            for (int i = 0; i < numberOfFloors; i++)
            {
                var floor = new ParkingFloor(i, i % 2 == 0 ? MovementDirection.LeftToRight : MovementDirection.RightToLeft);
                
                // Distribute spots evenly among types
                int spotsPerType = spotsPerFloor / 3;
                
                for (int j = 0; j < spotsPerType; j++)
                {
                    floor.AddParkingSpot(SlotType.Small);
                    floor.AddParkingSpot(SlotType.Compact);
                    floor.AddParkingSpot(SlotType.Large);
                }
                
                // Add remaining spots
                int remaining = spotsPerFloor - (spotsPerType * 3);
                for (int j = 0; j < remaining; j++)
                {
                    floor.AddParkingSpot(SlotType.Compact);
                }
                
                _floors.Add(floor);
                
                // Subscribe to floor events
                floor.SpotOccupied += OnSpotOccupied;
                floor.SpotVacated += OnSpotVacated;
            }
        }

        private void InitializeGates()
        {
            // Add 2 entry gates and 2 exit gates
            for (int i = 1; i <= 2; i++)
            {
                _entryGates.Add(new EntryGate(i, this));
                _exitGates.Add(new ExitGate(i, this));
            }
        }

        public ParkingResult ParkVehicle(Vehicle vehicle)
        {
            lock (_lock)
            {
                // Try to park on each floor
                for (int i = 0; i < _floors.Count; i++)
                {
                    var floor = _floors[i];
                    
                    if (floor.ParkVehicle(vehicle, out var spot))
                    {
                        var result = new ParkingResult
                        {
                            Success = true,
                            FloorNumber = i,
                            SlotNumber = spot.SlotNumber,
                            Message = $"Vehicle {vehicle.LicenseNumber} parked on floor {i}, slot {spot.SlotNumber}"
                        };
                        
                        Interlocked.Increment(ref _totalVehiclesServed);
                        VehicleParked?.Invoke(this, result);
                        
                        return result;
                    }
                }
                
                return new ParkingResult
                {
                    Success = false,
                    Message = "No parking spots available"
                };
            }
        }

        public bool RemoveVehicle(int floorNumber, int slotNumber)
        {
            lock (_lock)
            {
                if (floorNumber >= 0 && floorNumber < _floors.Count)
                {
                    var floor = _floors[floorNumber];
                    var spot = floor.GetType().GetField("_spots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(floor) as List<ParkingSpot>;
                    
                    var targetSpot = spot?.FirstOrDefault(s => s.SlotNumber == slotNumber);
                    if (targetSpot != null && floor.RemoveVehicle(targetSpot.Id, out var vehicle))
                    {
                        VehicleExited?.Invoke(this, vehicle?.LicenseNumber);
                        return true;
                    }
                }
                
                return false;
            }
        }

        public ParkingTicket GetActiveTicket(string ticketId)
        {
            lock (_lock)
            {
                return _activeTickets.TryGetValue(ticketId, out var ticket) ? ticket : null;
            }
        }

        public void ProcessPayment(string ticketId, string paymentMethod)
        {
            lock (_lock)
            {
                if (_activeTickets.TryGetValue(ticketId, out var ticket))
                {
                    var amount = ticket.CalculateAmount(_hourlyRate);
                    ticket.Pay(amount, paymentMethod);
                    _totalRevenue += amount;
                }
            }
        }

        public List<int> GetFloorsWithAvailableSpots()
        {
            return _floors
                .Where(f => f.HasAvailableSpots)
                .Select(f => f.FloorNumber)
                .ToList();
        }

        public Dictionary<int, Dictionary<SlotType, int>> GetAllAvailability()
        {
            var availability = new Dictionary<int, Dictionary<SlotType, int>>();
            
            for (int i = 0; i < _floors.Count; i++)
            {
                availability[i] = _floors[i].GetAvailabilityReport();
            }
            
            return availability;
        }

        public void DisplayAvailability()
        {
            Console.WriteLine("\n=== Parking Availability ===");
            var availability = GetAllAvailability();
            
            foreach (var floor in availability)
            {
                Console.WriteLine($"Floor {floor.Key}:");
                foreach (var slotType in floor.Value)
                {
                    Console.WriteLine($"  {slotType.Key} spots: {slotType.Value}");
                }
                Console.WriteLine($"  Total available: {_floors[floor.Key].GetTotalAvailableSpots()}");
            }
            
            Console.WriteLine($"\nTotal vehicles served: {_totalVehiclesServed}");
            Console.WriteLine($"Total revenue: ${_totalRevenue:F2}");
        }

        public void MoveVehicleToNextFloor(string vehicleLicenseNumber)
        {
            // Find vehicle and move to next floor
            for (int i = 0; i < _floors.Count - 1; i++)
            {
                if (_floors[i].MoveToNextFloor())
                {
                    Console.WriteLine($"Vehicle {vehicleLicenseNumber} moving from floor {i} to floor {i + 1}");
                    break;
                }
            }
        }

        // Event handlers
        private void OnSpotOccupied(object sender, ParkingSpot spot)
        {
            Console.WriteLine($"[Event] Spot occupied: {spot}");
        }

        private void OnSpotVacated(object sender, ParkingSpot spot)
        {
            Console.WriteLine($"[Event] Spot vacated: {spot}");
        }

        public ParkingLotStatistics GetStatistics()
        {
            return new ParkingLotStatistics
            {
                TotalFloors = TotalFloors,
                TotalSpots = TotalSpots,
                AvailableSpots = _floors.Sum(f => f.GetTotalAvailableSpots()),
                TotalVehiclesServed = _totalVehiclesServed,
                TotalRevenue = _totalRevenue,
                OccupancyRate = (double)(TotalSpots - _floors.Sum(f => f.GetTotalAvailableSpots())) / TotalSpots * 100
            };
        }
    }

    #endregion

    #region Statistics

    public class ParkingLotStatistics
    {
        public int TotalFloors { get; set; }
        public int TotalSpots { get; set; }
        public int AvailableSpots { get; set; }
        public int TotalVehiclesServed { get; set; }
        public double TotalRevenue { get; set; }
        public double OccupancyRate { get; set; }
        
        public override string ToString()
        {
            return $"Parking Lot Stats: {AvailableSpots}/{TotalSpots} spots available ({OccupancyRate:F1}% occupied), " +
                   $"Vehicles Served: {TotalVehiclesServed}, Revenue: ${TotalRevenue:F2}";
        }
    }

    #endregion

    #region Admin and Payment

    public class Admin
    {
        private readonly ParkingLot _parkingLot;
        
        public Admin(ParkingLot parkingLot)
        {
            _parkingLot = parkingLot;
        }
        
        public void AddParkingFloor(int spotsCount)
        {
            // Implementation for adding new floor
            Console.WriteLine($"Admin: Adding new floor with {spotsCount} spots");
        }
        
        public void ModifyParkingSpot(int floorNumber, int slotNumber, SlotType newType)
        {
            Console.WriteLine($"Admin: Modifying spot {floorNumber}-{slotNumber} to {newType}");
        }
        
        public void GenerateReport()
        {
            var stats = _parkingLot.GetStatistics();
            Console.WriteLine(stats);
            _parkingLot.DisplayAvailability();
        }
        
        public void SetHourlyRate(double rate)
        {
            // Implementation for setting hourly rate
            Console.WriteLine($"Admin: Setting hourly rate to ${rate}");
        }
    }

    public class PaymentProcessor
    {
        public enum PaymentMethod
        {
            Cash,
            CreditCard,
            DebitCard,
            MobilePayment
        }
        
        public static double ProcessPayment(ParkingTicket ticket, double amount, PaymentMethod method)
        {
            // Simulate payment processing
            Console.WriteLine($"Processing {method} payment of ${amount:F2} for ticket {ticket.TicketId}");
            return amount;
        }
    }

    #endregion
 
}
```
# Elevator

```C#

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ElevatorSystem
{
    public enum Direction
    {
        Up = 1,
        Down = -1,
        Idle = 0
    }

    public enum DoorState
    {
        Open,
        Closed,
        Opening,
        Closing
    }

    public enum ElevatorState
    {
        Moving,
        Stopped,
        Idle,
        Maintenance,
        Emergency
    }

    public class ElevatorRequest
    {
        public int Floor { get; set; }
        public Direction Direction { get; set; }
        public DateTime RequestTime { get; set; }
        public bool IsInternal { get; set; } // Internal button vs external call

        public ElevatorRequest(int floor, Direction direction, bool isInternal)
        {
            Floor = floor;
            Direction = direction;
            RequestTime = DateTime.Now;
            IsInternal = isInternal;
        }
    }

    public class Elevator
    {
        private readonly int _id;
        private int _currentFloor;
        private Direction _currentDirection;
        private ElevatorState _state;
        private DoorState _doorState;
        private readonly List<int> _destinationFloors;
        private readonly Queue<ElevatorRequest> _pendingRequests;
        private readonly HashSet<int> _stopRequests;
        private readonly object _lock = new object();
        private readonly int _maxCapacity;
        private int _currentPassengers;
        private bool _isEmergency;
        private Timer _doorTimer;
        private const int DoorOpenTime = 3000; // 3 seconds
        private const int FloorTravelTime = 1000; // 1 second per floor

        public int Id => _id;
        public int CurrentFloor => _currentFloor;
        public Direction CurrentDirection => _currentDirection;
        public ElevatorState State => _state;
        public DoorState DoorState => _doorState;
        public int CurrentPassengers => _currentPassengers;
        public bool IsFull => _currentPassengers >= _maxCapacity;
        public bool HasPendingRequests => _pendingRequests.Count > 0 || _stopRequests.Count > 0;

        // Events
        public event EventHandler<ElevatorEventArgs> FloorArrived;
        public event EventHandler<ElevatorEventArgs> DoorOpened;
        public event EventHandler<ElevatorEventArgs> DoorClosed;
        public event EventHandler<ElevatorEventArgs> PassengerBoarded;
        public event EventHandler<ElevatorEventArgs> PassengerDisembarked;

        public Elevator(int id, int maxCapacity = 10)
        {
            _id = id;
            _currentFloor = 0; // Ground floor
            _currentDirection = Direction.Idle;
            _state = ElevatorState.Idle;
            _doorState = DoorState.Closed;
            _destinationFloors = new List<int>();
            _pendingRequests = new Queue<ElevatorRequest>();
            _stopRequests = new HashSet<int>();
            _maxCapacity = maxCapacity;
            _currentPassengers = 0;
            _isEmergency = false;
        }

        /// <summary>
        /// Request elevator from external floor button
        /// </summary>
        public void CallElevator(int floor, Direction direction)
        {
            if (_isEmergency)
            {
                Console.WriteLine($"Elevator {_id} is in emergency mode. Cannot accept calls.");
                return;
            }

            lock (_lock)
            {
                var request = new ElevatorRequest(floor, direction, false);
                _pendingRequests.Enqueue(request);
                Console.WriteLine($"Elevator {_id} received external call at floor {floor} going {direction}");
            }
            
            ProcessRequests();
        }

        /// <summary>
        /// Internal floor selection from inside elevator
        /// </summary>
        public void SelectFloor(int floor)
        {
            if (_isEmergency)
            {
                Console.WriteLine($"Elevator {_id} is in emergency mode. Cannot select floors.");
                return;
            }

            lock (_lock)
            {
                if (floor != _currentFloor && !_stopRequests.Contains(floor))
                {
                    _stopRequests.Add(floor);
                    Console.WriteLine($"Elevator {_id} added floor {floor} to stop requests");
                }
            }
            
            ProcessRequests();
        }

        /// <summary>
        /// Main processing loop
        /// </summary>
        public async void ProcessRequests()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    while (HasPendingRequests && !_isEmergency)
                    {
                        DetermineNextStop();
                        
                        if (_destinationFloors.Count > 0)
                        {
                            _state = ElevatorState.Moving;
                            int nextFloor = _destinationFloors[0];
                            MoveToFloor(nextFloor);
                        }
                        else
                        {
                            _state = ElevatorState.Idle;
                            _currentDirection = Direction.Idle;
                            break;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Intelligent algorithm to determine next stop
        /// </summary>
        private void DetermineNextStop()
        {
            _destinationFloors.Clear();
            
            // Add internal stop requests
            _destinationFloors.AddRange(_stopRequests);
            
            // Add external pending requests based on current direction
            var relevantRequests = _pendingRequests
                .Where(r => IsRequestInDirection(r))
                .OrderBy(r => r.Floor)
                .ToList();
            
            foreach (var request in relevantRequests)
            {
                if (!_destinationFloors.Contains(request.Floor))
                {
                    _destinationFloors.Add(request.Floor);
                }
            }
            
            // Sort based on direction
            if (_currentDirection == Direction.Up || _currentDirection == Direction.Idle)
            {
                _destinationFloors.Sort();
                _currentDirection = Direction.Up;
            }
            else if (_currentDirection == Direction.Down)
            {
                _destinationFloors.Sort((a, b) => b.CompareTo(a));
            }
            
            // Remove requests behind current direction
            _destinationFloors.RemoveAll(f => IsBehindCurrentDirection(f));
        }

        private bool IsRequestInDirection(ElevatorRequest request)
        {
            if (_currentDirection == Direction.Idle)
                return true;
            
            if (_currentDirection == Direction.Up)
                return request.Floor > _currentFloor && request.Direction == Direction.Up;
            else
                return request.Floor < _currentFloor && request.Direction == Direction.Down;
        }

        private bool IsBehindCurrentDirection(int floor)
        {
            if (_currentDirection == Direction.Up)
                return floor < _currentFloor;
            else if (_currentDirection == Direction.Down)
                return floor > _currentFloor;
            return false;
        }

        /// <summary>
        /// Move elevator to specified floor
        /// </summary>
        private async void MoveToFloor(int targetFloor)
        {
            int floorsToMove = Math.Abs(targetFloor - _currentFloor);
            
            for (int i = 0; i < floorsToMove; i++)
            {
                if (_isEmergency) break;
                
                await Task.Delay(FloorTravelTime);
                
                _currentFloor += (_currentDirection == Direction.Up) ? 1 : -1;
                
                // Check if we need to stop at this floor
                if (ShouldStopAtFloor(_currentFloor))
                {
                    StopAtFloor();
                }
                
                OnFloorArrived(_currentFloor);
                Console.WriteLine($"Elevator {_id} arrived at floor {_currentFloor}");
            }
        }

        private bool ShouldStopAtFloor(int floor)
        {
            // Check internal requests
            if (_stopRequests.Contains(floor))
                return true;
            
            // Check external requests
            var pendingRequest = _pendingRequests
                .FirstOrDefault(r => r.Floor == floor && IsRequestInDirection(r));
            
            if (pendingRequest != null)
                return true;
            
            return false;
        }

        private async void StopAtFloor()
        {
            _state = ElevatorState.Stopped;
            
            // Remove from requests
            _stopRequests.Remove(_currentFloor);
            
            var pendingRequests = _pendingRequests
                .Where(r => r.Floor == _currentFloor)
                .ToList();
            
            foreach (var request in pendingRequests)
            {
                // Remove from queue
                var tempQueue = new Queue<ElevatorRequest>();
                while (_pendingRequests.Count > 0)
                {
                    var item = _pendingRequests.Dequeue();
                    if (!(item.Floor == request.Floor && item.Direction == request.Direction))
                    {
                        tempQueue.Enqueue(item);
                    }
                }
                while (tempQueue.Count > 0)
                {
                    _pendingRequests.Enqueue(tempQueue.Dequeue());
                }
            }
            
            // Open doors
            await OpenDoors();
            
            // Simulate passenger exchange
            await SimulatePassengerExchange();
            
            // Close doors
            await CloseDoors();
            
            _state = ElevatorState.Moving;
        }

        private async Task OpenDoors()
        {
            _doorState = DoorState.Opening;
            OnDoorOpened(_currentFloor);
            Console.WriteLine($"Elevator {_id} opening doors at floor {_currentFloor}");
            
            await Task.Delay(500); // Door opening time
            
            _doorState = DoorState.Open;
            Console.WriteLine($"Elevator {_id} doors are open at floor {_currentFloor}");
        }

        private async Task CloseDoors()
        {
            _doorState = DoorState.Closing;
            Console.WriteLine($"Elevator {_id} closing doors at floor {_currentFloor}");
            
            await Task.Delay(500); // Door closing time
            
            _doorState = DoorState.Closed;
            Console.WriteLine($"Elevator {_id} doors are closed");
        }

        private async Task SimulatePassengerExchange()
        {
            // Simulate passengers exiting
            int exitingPassengers = new Random().Next(0, _currentPassengers + 1);
            if (exitingPassengers > 0)
            {
                _currentPassengers -= exitingPassengers;
                OnPassengerDisembarked(exitingPassengers);
                Console.WriteLine($"{exitingPassengers} passengers exited elevator {_id}. Remaining: {_currentPassengers}");
            }
            
            // Simulate passengers boarding
            int boardingPassengers = 0;
            if (!IsFull)
            {
                boardingPassengers = new Random().Next(0, _maxCapacity - _currentPassengers + 1);
                _currentPassengers += boardingPassengers;
                OnPassengerBoarded(boardingPassengers);
                Console.WriteLine($"{boardingPassengers} passengers boarded elevator {_id}. Now: {_currentPassengers}");
            }
            
            await Task.Delay(DoorOpenTime); // Time for passenger exchange
        }

        /// <summary>
        /// Emergency stop button
        /// </summary>
        public async Task EmergencyStop()
        {
            _isEmergency = true;
            _state = ElevatorState.Emergency;
            Console.WriteLine($"Elevator {_id} EMERGENCY STOP at floor {_currentFloor}");
            
            await OpenDoors();
            
            // Emergency protocols
            Console.WriteLine($"Elevator {_id} in emergency mode. Call emergency services.");
        }

        /// <summary>
        /// Reset after emergency
        /// </summary>
        public void ResetEmergency()
        {
            _isEmergency = false;
            _state = ElevatorState.Idle;
            _currentDirection = Direction.Idle;
            Console.WriteLine($"Elevator {_id} emergency mode reset. Returning to service.");
        }

        /// <summary>
        /// Weight sensor trigger
        /// </summary>
        public bool CheckWeightLimit(int weight)
        {
            int maxWeight = _maxCapacity * 75; // Assuming 75kg per person
            if (weight > maxWeight)
            {
                Console.WriteLine($"Elevator {_id} weight limit exceeded!");
                return false;
            }
            return true;
        }

        // Event invokers
        protected virtual void OnFloorArrived(int floor)
        {
            FloorArrived?.Invoke(this, new ElevatorEventArgs(_id, floor, _currentDirection, _currentPassengers));
        }

        protected virtual void OnDoorOpened(int floor)
        {
            DoorOpened?.Invoke(this, new ElevatorEventArgs(_id, floor, _currentDirection, _currentPassengers));
        }

        protected virtual void OnDoorClosed(int floor)
        {
            DoorClosed?.Invoke(this, new ElevatorEventArgs(_id, floor, _currentDirection, _currentPassengers));
        }

        protected virtual void OnPassengerBoarded(int count)
        {
            PassengerBoarded?.Invoke(this, new ElevatorEventArgs(_id, _currentFloor, _currentDirection, count));
        }

        protected virtual void OnPassengerDisembarked(int count)
        {
            PassengerDisembarked?.Invoke(this, new ElevatorEventArgs(_id, _currentFloor, _currentDirection, count));
        }

        public ElevatorStatus GetStatus()
        {
            return new ElevatorStatus
            {
                Id = _id,
                CurrentFloor = _currentFloor,
                Direction = _currentDirection,
                State = _state,
                DoorState = _doorState,
                PassengerCount = _currentPassengers,
                PendingRequests = _pendingRequests.Count,
                StopRequests = _stopRequests.Count
            };
        }
    }

    public class ElevatorController
    {
        private readonly List<Elevator> _elevators;
        private readonly object _lock = new object();
        private readonly Dictionary<int, List<ElevatorRequest>> _floorRequests;

        public ElevatorController(int numberOfElevators, int maxCapacity = 10)
        {
            _elevators = new List<Elevator>();
            _floorRequests = new Dictionary<int, List<ElevatorRequest>>();
            
            for (int i = 1; i <= numberOfElevators; i++)
            {
                var elevator = new Elevator(i, maxCapacity);
                _elevators.Add(elevator);
                
                // Subscribe to elevator events
                elevator.FloorArrived += OnElevatorFloorArrived;
                elevator.DoorOpened += OnElevatorDoorOpened;
                elevator.DoorClosed += OnElevatorDoorClosed;
            }
        }

        /// <summary>
        /// Call elevator from floor
        /// </summary>
        public void CallElevator(int floor, Direction direction)
        {
            var bestElevator = FindBestElevator(floor, direction);
            
            if (bestElevator != null)
            {
                Console.WriteLine($"Assigning elevator {bestElevator.Id} to floor {floor} going {direction}");
                bestElevator.CallElevator(floor, direction);
            }
            else
            {
                // Store request for when elevators become available
                if (!_floorRequests.ContainsKey(floor))
                {
                    _floorRequests[floor] = new List<ElevatorRequest>();
                }
                _floorRequests[floor].Add(new ElevatorRequest(floor, direction, false));
                Console.WriteLine($"No elevators available. Request for floor {floor} queued.");
            }
        }

        /// <summary>
        /// Find best elevator using intelligent algorithm
        /// </summary>
        private Elevator FindBestElevator(int floor, Direction direction)
        {
            lock (_lock)
            {
                var availableElevators = _elevators
                    .Where(e => e.State != ElevatorState.Maintenance && e.State != ElevatorState.Emergency)
                    .ToList();
                
                if (!availableElevators.Any())
                    return null;
                
                // Score each elevator
                var scoredElevators = availableElevators.Select(e => new
                {
                    Elevator = e,
                    Score = CalculateElevatorScore(e, floor, direction)
                }).OrderBy(s => s.Score);
                
                return scoredElevators.FirstOrDefault()?.Elevator;
            }
        }

        private int CalculateElevatorScore(Elevator elevator, int floor, Direction direction)
        {
            int score = 0;
            
            // Idle elevators get best score
            if (elevator.State == ElevatorState.Idle)
            {
                score += Math.Abs(elevator.CurrentFloor - floor) * 10;
            }
            // Moving in same direction and passing floor
            else if (elevator.CurrentDirection == direction)
            {
                if (direction == Direction.Up && elevator.CurrentFloor <= floor)
                    score += (floor - elevator.CurrentFloor) * 20;
                else if (direction == Direction.Down && elevator.CurrentFloor >= floor)
                    score += (elevator.CurrentFloor - floor) * 20;
                else
                    score += 1000; // Wrong direction penalty
            }
            // Moving opposite direction
            else
            {
                score += 500 + Math.Abs(elevator.CurrentFloor - floor) * 30;
            }
            
            // Add penalty for busy elevators
            score += elevator.CurrentPassengers * 50;
            score += elevator.HasPendingRequests ? 100 : 0;
            
            return score;
        }

        private void OnElevatorFloorArrived(object sender, ElevatorEventArgs e)
        {
            // Check for pending floor requests
            if (_floorRequests.ContainsKey(e.Floor))
            {
                var requests = _floorRequests[e.Floor].ToList();
                foreach (var request in requests)
                {
                    var elevator = sender as Elevator;
                    elevator?.SelectFloor(e.Floor);
                }
                _floorRequests.Remove(e.Floor);
            }
        }

        private void OnElevatorDoorOpened(object sender, ElevatorEventArgs e)
        {
            Console.WriteLine($"Elevator {e.ElevatorId} doors opened at floor {e.Floor}");
        }

        private void OnElevatorDoorClosed(object sender, ElevatorEventArgs e)
        {
            Console.WriteLine($"Elevator {e.ElevatorId} doors closed at floor {e.Floor}");
        }

        public List<ElevatorStatus> GetAllElevatorStatus()
        {
            return _elevators.Select(e => e.GetStatus()).ToList();
        }

        public void DisplayAllStatus()
        {
            Console.WriteLine("\n=== Elevator Status ===");
            foreach (var status in GetAllElevatorStatus())
            {
                Console.WriteLine($"Elevator {status.Id}: Floor {status.CurrentFloor}, " +
                                  $"Direction: {status.Direction}, State: {status.State}, " +
                                  $"Door: {status.DoorState}, Passengers: {status.PassengerCount}, " +
                                  $"Requests: {status.PendingRequests}");
            }
        }
    }

    public class ElevatorEventArgs : EventArgs
    {
        public int ElevatorId { get; }
        public int Floor { get; }
        public Direction Direction { get; }
        public int PassengerCount { get; }

        public ElevatorEventArgs(int elevatorId, int floor, Direction direction, int passengerCount)
        {
            ElevatorId = elevatorId;
            Floor = floor;
            Direction = direction;
            PassengerCount = passengerCount;
        }
    }

    public class ElevatorStatus
    {
        public int Id { get; set; }
        public int CurrentFloor { get; set; }
        public Direction Direction { get; set; }
        public ElevatorState State { get; set; }
        public DoorState DoorState { get; set; }
        public int PassengerCount { get; set; }
        public int PendingRequests { get; set; }
        public int StopRequests { get; set; }
    }

}
```
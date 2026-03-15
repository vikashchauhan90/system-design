# OOPs Fundamentals

This document covers the core concepts of Object-Oriented Programming (OOP).

## 1 Object
- **Definition:** An object is a real-world entity or concept represented in software. It is an instance of a class, containing state (attributes) and behavior (methods).
- **Example:**

```csharp
// Class definition
public class Dog {
    public string Name;
    public void Bark() {
        Console.WriteLine($"{Name} says Woof!");
    }
}

// Creating an object (instance)
Dog myDog = new Dog();
myDog.Name = "Buddy";
myDog.Bark(); // Output: Buddy says Woof!
```

## 2 Instance
- **Definition:** An instance is a concrete occurrence of any object, existing in memory. When a class is defined, no memory is allocated until an object (instance) is created.
- **Example:**

```csharp
Dog dog1 = new Dog(); // dog1 is an instance of Dog
Dog dog2 = new Dog(); // dog2 is another instance
```

## 3 Encapsulation
- **Definition:** Bundling data (fields) and methods that operate on the data into a single unit (class), restricting direct access to some of the object's components.
- **Example:**

```csharp
public class Dog {
    private string name;
    public Dog(string name) {
        this.name = name;
    }
    public void Bark() {
        Console.WriteLine($"{name} says Woof!");
    }
}
```

Encapsulation keep similar things in a separate class and encapsulate them, it means:
- Identify related data and behavior
- Group them into a single class
- Hide the internal implementation
- Expose only necessary operations


Benefits:
- Protects internal state
- Reduces coupling
- Improves maintainability


#### Example Without Encapsulation (Bad Design)

```csharp
public class EmailService
{
    public void SendEmail(string to, string subject, string body)
    {
        // validate email
        if (!to.Contains("@"))
            throw new Exception("Invalid email");

        // format message
        string message = $"Subject:{subject}\n{body}";

        // send logic
        Console.WriteLine("Sending email: " + message);
    }
}
```
Here the class handles too many responsibilities:
- validation
- formatting
- sending

#### Example With Encapsulation (Better Design)

```csharp
public class EmailValidator
{
    public bool IsValid(string email)
    {
        return email.Contains("@");
    }
}

public class EmailFormatter
{
    public string Format(string subject, string body)
    {
        return $"Subject:{subject}\n{body}";
    }
}

public class EmailSender
{
    public void Send(string message)
    {
        Console.WriteLine("Sending email: " + message);
    }
}

public class EmailService
{
    private readonly EmailValidator _validator = new();
    private readonly EmailFormatter _formatter = new();
    private readonly EmailSender _sender = new();

    public void SendEmail(string to, string subject, string body)
    {
        if (!_validator.IsValid(to))
            throw new Exception("Invalid email");

        var message = _formatter.Format(subject, body);
        _sender.Send(message);
    }
}
```

Encapsulation helps achieve:
- Low coupling
- High cohesion
- Better testability
- Easier refactoring

> **Note:** If several methods operate on the same data or concept, they probably belong in the same class.

## 4 Inheritance
- **Definition:** Mechanism where a new class derives properties and behavior from an existing class.
- **Example:**

```csharp
public class Animal {
    public void Eat() {
        Console.WriteLine("Animal eats");
    }
}
public class Dog : Animal {
    public void Bark() {
        Console.WriteLine("Dog barks");
    }
}
```
Benefits:
- Code reuse
- Logical hierarchy
- Extensibility

## 5 Polymorphism
- **Definition:** Ability to present the same interface for different data types.
- **Example:**

```csharp
public class Animal {
    public virtual void Speak() {
        Console.WriteLine("Animal speaks");
    }
}
public class Dog : Animal {
    public override void Speak() {
        Console.WriteLine("Dog barks");
    }
}
public class Cat : Animal {
    public override void Speak() {
        Console.WriteLine("Cat meows");
    }
}
```


## 6 Abstraction
- **Definition:** Hiding complex implementation details and showing only the necessary features.
- **Example:**

```csharp
public abstract class Animal {
    public abstract void MakeSound();
}
public class Dog : Animal {
    public override void MakeSound() {
        Console.WriteLine("Woof!");
    }
}
```

## 7 Association
- **Definition:**  Association represents a relationship between two independent classes where one object uses or interacts with another.

Unlike composition or aggregation, association does not imply ownership. The objects can exist independently.

Example relationships:
- Customer → Order
- Teacher → Student
- Doctor → Patient

Association can be:
- One-to-One
- One-to-Many
- Many-to-Many

- **Example:**
```csharp
public class Customer
{
    public string Name { get; set; }

    public void PlaceOrder(Order order)
    {
        Console.WriteLine($"{Name} placed order #{order.Id}");
    }
}

public class Order
{
    public int Id { get; set; }
}
```

## 8 Aggregation
- **Definition:** Aggregation is a special form of association that represents a "has-a" relationship where the child can exist independently of the parent. It is a weak association.
- **Example:**

```csharp
public class Team {
    public List<Player> Players { get; set; }
    public Team(List<Player> players) {
        Players = players;
    }
}
public class Player {
    public string Name { get; set; }
    public Player(string name) {
        Name = name;
    }
}
```

## 9 Composition
- **Definition:** Composition is a strong form of association where the child cannot exist independently of the parent. If the parent is destroyed, so are the children.
- **Example:**

```csharp
public class House {
    public Room Room { get; set; }
    public House() {
        Room = new Room();
    }
}
public class Room {
    public Room() {
        // Room is created with House
    }
}
```

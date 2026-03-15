# SOLID Principles (with C# Examples)

This document explains the SOLID principles of object-oriented design, each with a clear definition and C# code example.

These principles help create **maintainable, scalable, and flexible software systems**.

## 1. Single Responsibility Principle (SRP)

### Definition

A class should have **only one reason to change**, meaning it should have **only one responsibility**.

If a class handles multiple responsibilities, changes in one area may break unrelated functionality.


### Violation Example (Bad Design)

```csharp
public class Report
{
    public string Content { get; set; }

    public void Print()
    {
        Console.WriteLine(Content);
    }

    public void SaveToFile(string path)
    {
        File.WriteAllText(path, Content);
    }
}
```

Problems:

* `Report` represents **data**
* It also handles **printing**
* It also handles **file storage**

This creates **multiple reasons to change**.


### Correct Implementation (Good Design)

```csharp
public class Report
{
    public string Content { get; set; }
}

public class ReportPrinter
{
    public void Print(Report report)
    {
        Console.WriteLine(report.Content);
    }
}

public class ReportRepository
{
    public void SaveToFile(Report report, string path)
    {
        File.WriteAllText(path, report.Content);
    }
}
```

Benefits:

* Each class has **one responsibility**
* Easier to maintain
* Easier to test


## 2. Open/Closed Principle (OCP)

### Definition

Software entities should be:

* **Open for extension**
* **Closed for modification**

This means **new behavior should be added without modifying existing code**.

---

### Violation Example (Bad Design)

```csharp
public class AreaCalculator
{
    public double Calculate(object shape)
    {
        if (shape is Circle c)
            return Math.PI * c.Radius * c.Radius;

        if (shape is Square s)
            return s.Side * s.Side;

        throw new Exception("Unknown shape");
    }
}

public class Circle
{
    public double Radius { get; set; }
}

public class Square
{
    public double Side { get; set; }
}
```

Problems:

* Every new shape requires modifying `AreaCalculator`
* Violates **Open/Closed Principle**

---

### Correct Implementation (Good Design)

```csharp
public abstract class Shape
{
    public abstract double Area();
}

public class Circle : Shape
{
    public double Radius { get; set; }

    public override double Area()
    {
        return Math.PI * Radius * Radius;
    }
}

public class Square : Shape
{
    public double Side { get; set; }

    public override double Area()
    {
        return Side * Side;
    }
}
```

Now new shapes can be added without modifying existing code.


### OCP Using Extension Methods (C#)

Extension methods allow us to **extend existing classes without modifying them**, which perfectly aligns with OCP.

Suppose we cannot modify an existing `Order` class.

```csharp
public class Order
{
    public decimal Price { get; set; }
}
```

We want to add tax calculation.

```csharp
public static class OrderExtensions
{
    public static decimal CalculateTax(this Order order)
    {
        return order.Price * 0.18m;
    }
}
```

Usage:

```csharp
var order = new Order { Price = 1000 };
var tax = order.CalculateTax();
```

Benefits:

* No modification to existing class
* New behavior added externally
* Supports **Open/Closed Principle**


## 3. Liskov Substitution Principle (LSP)

### Definition

Objects of a superclass should be replaceable with objects of its subclasses **without breaking the application**.


### Violation Example (Bad Design)

```csharp
public class Bird
{
    public virtual void Fly()
    {
        Console.WriteLine("Bird flies");
    }
}

public class Sparrow : Bird
{
}

public class Ostrich : Bird
{
    public override void Fly()
    {
        throw new NotSupportedException("Ostrich can't fly");
    }
}
```

Problem:

* `Ostrich` is a `Bird`
* But it **cannot fly**

This breaks the **substitutability rule**.


### Correct Implementation

```csharp
public abstract class Bird
{
}

public interface IFlyingBird
{
    void Fly();
}

public class Sparrow : Bird, IFlyingBird
{
    public void Fly()
    {
        Console.WriteLine("Sparrow flies");
    }
}

public class Ostrich : Bird
{
}
```

Now:

* Flying birds implement `IFlyingBird`
* Non-flying birds don't

This respects LSP.


## 4. Interface Segregation Principle (ISP)

### Definition

Clients should **not be forced to depend on interfaces they do not use**.

Large interfaces should be split into **smaller specific interfaces**.


### Violation Example (Bad Design)

```csharp
public interface IMachine
{
    void Print();
    void Scan();
    void Fax();
}
```

Problem:

A simple printer must implement methods it doesn't support.

```csharp
public class BasicPrinter : IMachine
{
    public void Print()
    {
        Console.WriteLine("Printing...");
    }

    public void Scan()
    {
        throw new NotImplementedException();
    }

    public void Fax()
    {
        throw new NotImplementedException();
    }
}
```


### Correct Implementation

Split interfaces.

```csharp
public interface IPrinter
{
    void Print();
}

public interface IScanner
{
    void Scan();
}

public interface IFax
{
    void Fax();
}
```

Implementation:

```csharp
public class MultiFunctionPrinter : IPrinter, IScanner
{
    public void Print()
    {
        Console.WriteLine("Printing...");
    }

    public void Scan()
    {
        Console.WriteLine("Scanning...");
    }
}
```

Benefits:

* Smaller interfaces
* More flexible design
* Avoid unnecessary implementations


## 5. Dependency Inversion Principle (DIP)

### Definition

High-level modules should **not depend on low-level modules**.
Both should depend on **abstractions**.


### Violation Example (Bad Design)

```csharp
public class EmailSender
{
    public void Send(string message)
    {
        Console.WriteLine("Sending Email: " + message);
    }
}

public class NotificationService
{
    private EmailSender sender = new EmailSender();

    public void Notify(string message)
    {
        sender.Send(message);
    }
}
```

Problems:

* High-level module depends directly on `EmailSender`
* Hard to switch to SMS or Push notifications

---

# Correct Implementation

```csharp
public interface IMessageSender
{
    void Send(string message);
}

public class EmailSender : IMessageSender
{
    public void Send(string message)
    {
        Console.WriteLine("Sending Email: " + message);
    }
}

public class SmsSender : IMessageSender
{
    public void Send(string message)
    {
        Console.WriteLine("Sending SMS: " + message);
    }
}

public class Notification
{
    private readonly IMessageSender _sender;

    public Notification(IMessageSender sender)
    {
        _sender = sender;
    }

    public void Notify(string message)
    {
        _sender.Send(message);
    }
}
```

Usage:

```csharp
var notification = new Notification(new EmailSender());
notification.Notify("Hello");
```

Benefits:

* Loose coupling
* Easy to add new message types
* Better testability

---

# Summary of SOLID

| Principle | Meaning                                     |
| --------- | ------------------------------------------- |
| **SRP**   | One class → one responsibility              |
| **OCP**   | Open for extension, closed for modification |
| **LSP**   | Subtypes must work as their base types      |
| **ISP**   | Prefer small, specific interfaces           |
| **DIP**   | Depend on abstractions, not implementations |

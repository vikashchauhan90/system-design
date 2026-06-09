# C# Runbook

## Introduction

C# is a modern, object-oriented language for building web, desktop, mobile, cloud, and game applications. It runs on the .NET platform and blends productivity with strong typing, garbage collection, and powerful asynchronous and concurrency features.

This runbook covers:
- C# fundamentals
- Core object-oriented concepts
- Generics and LINQ
- Exception handling
- Threading and concurrency
- `async`/`await` and performance optimization

---

## 1. C# Fundamentals

### 1.1 .NET and C# project structure

Modern C# projects use the SDK-style project format.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Typical files:
- `Program.cs` — app entry point
- `*.csproj` — project file
- `appsettings.json` — configuration data

### 1.2 Basic syntax

```csharp
using System;

class Program
{
    static void Main()
    {
        var name = "World";
        Console.WriteLine($"Hello, {name}!");
    }
}
```

### 1.3 Variables and types

C# has simple value types and reference types.

```csharp
int age = 30;
string name = "Alice";
double price = 19.99;
bool isActive = true;
const int MAX_COUT = 10;
```

`object` is the base type of all C# types.

```csharp
object value = 123;
value = "hello";
```

`dynamic` bypasses compile-time type checking and resolves member calls at runtime.

```csharp
dynamic dyn = 10;
dyn = dyn + 5;
Console.WriteLine(dyn);
```

Use `dynamic` sparingly because it removes compile-time safety.

### Boxing and unboxing

When a value type is converted to `object` or an interface type, it is boxed: a heap object is created to hold the value.

```csharp
int n = 42;
object boxed = n; // boxing
int unboxed = (int)boxed; // unboxing
```

Boxing and unboxing are relatively expensive, so avoid them in hot paths.

`readonly` is used on fields and structs to enforce immutability.

```csharp
public readonly struct Point
{
    public int X { get; }
    public int Y { get; }
    public Point(int x, int y) => (X, Y) = (x, y);
}

public class Config
{
    public readonly string Name;
    public Config(string name) => Name = name;
}
```

### 1.4 Type inference

Use `var` when the type is clear.

```csharp
var count = 5;
var message = "hello";
```

### 1.5 Nullable reference types

Enable nullable annotations to catch null bugs.

```csharp
string? maybeNull = null;
string notNull = "text";
```

---

## 2. Object-Oriented Programming

### 2.1 Classes and structs

```csharp
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public struct Point
{
    public int X;
    public int Y;
}
```

Use classes for reference semantics and structs for small value types.

### 2.2 Inheritance and polymorphism

```csharp
public class Animal
{
    public virtual void Speak() => Console.WriteLine("...\n");
}

public class Dog : Animal
{
    public override void Speak() => Console.WriteLine("Woof");
}
```

### 2.3 Virtual methods and `base`

Virtual methods enable derived classes to override behavior from a base class.

```csharp
public class BaseAnimal
{
    public BaseAnimal()
    {
        Console.WriteLine("BaseAnimal constructor");
    }

    static BaseAnimal()
    {
        Console.WriteLine("BaseAnimal static constructor");
    }

    public virtual void Speak()
    {
        Console.WriteLine("BaseAnimal speaks");
    }
}

public class Dog : BaseAnimal
{
    public Dog()
    {
        Console.WriteLine("Dog constructor");
    }

    static Dog()
    {
        Console.WriteLine("Dog static constructor");
    }

    public override void Speak()
    {
        Console.WriteLine("Dog says woof");
    }

    public void SpeakAsBase()
    {
        base.Speak();
    }
}

var dog = new Dog();
// Static constructors run once before the first instance or static member access.
// Output order:
// BaseAnimal static constructor
// Dog static constructor
// BaseAnimal constructor
// Dog constructor

dog.Speak();        // Dog says woof

dog.SpeakAsBase();  // BaseAnimal speaks
```

The `base` keyword calls the base class implementation from a derived class. It is useful when overriding a virtual method but still needing the base behavior.

### 2.3.1 Method dispatch and inheritance

When a virtual method is called on a reference, the runtime chooses the most-derived override available.

```csharp
public class Animal
{
    public virtual string GetSound() => "generic sound";
}

public class Cat : Animal
{
    public override string GetSound() => "meow";
}

public class PersianCat : Cat
{
    public override string GetSound() => "purr";
}

Animal animal = new PersianCat();
Console.WriteLine(animal.GetSound()); // purr

Cat cat = new PersianCat();
Console.WriteLine(cat.GetSound());    // purr

PersianCat pc = new PersianCat();
Console.WriteLine(pc.GetSound());     // purr
```

If the base method is not virtual, the derived method hides it rather than overriding it.

```csharp
public class BaseLogger
{
    public void Log() => Console.WriteLine("Base log");
}

public class FileLogger : BaseLogger
{
    public new void Log() => Console.WriteLine("File log");
}

BaseLogger logger = new FileLogger();
logger.Log(); // Base log
```

### 2.3.2 Constructor order in class inheritance

In a class inheritance chain, constructors run from the top-most base class down to the most-derived class.

```csharp
public class Parent
{
    public Parent()
    {
        Console.WriteLine("Parent ctor");
    }
}

public class Child : Parent
{
    public Child()
    {
        Console.WriteLine("Child ctor");
    }
}

new Child();
// Output:
// Parent ctor
// Child ctor
```

If a base constructor requires arguments, the derived constructor must explicitly call it with `: base(...)`.

```csharp
public class Base
{
    public Base(string name)
    {
        Console.WriteLine($"Base ctor {name}");
    }
}

public class Derived : Base
{
    public Derived() : base("Derived")
    {
        Console.WriteLine("Derived ctor");
    }
}
```

### 2.3.3 Static constructors in non-static classes

A non-static class can still have a static constructor. It runs once before any instance of the class is created or any static member is accessed.

```csharp
public class Example
{
    static Example()
    {
        Console.WriteLine("Example static ctor");
    }

    public Example()
    {
        Console.WriteLine("Example instance ctor");
    }
}

new Example();
// Output:
// Example static ctor
// Example instance ctor
```

Static constructors are useful for type initialization, such as creating static read-only data or registering metadata.

### 2.3.4 `sealed` classes and members

The `sealed` keyword prevents a class from being inherited or a virtual member from being overridden.

```csharp
public sealed class FinalLogger
{
    public void Log(string message) => Console.WriteLine(message);
}

public class BaseWriter
{
    public virtual void Write() => Console.WriteLine("Base");
}

public class SpecialWriter : BaseWriter
{
    public sealed override void Write() => Console.WriteLine("Special");
}

public class DerivedWriter : SpecialWriter
{
    // Cannot override Write() here because it is sealed in SpecialWriter.
}
```

`sealed` helps enforce class hierarchies and avoid unintended overrides.

### 2.3.5 Access modifiers and file scope

C# access modifiers control visibility of types and members.

- `public` - accessible from any code.
- `private` - accessible only inside the containing type.
- `protected` - accessible inside the containing type and derived types.
- `internal` - accessible within the same assembly.
- `protected internal` - accessible in the same assembly or in derived types.
- `private protected` - accessible in derived types, but only within the same assembly.

```csharp
public class PublicClass
{
    private int _privateField;
    protected int ProtectedValue;
    internal void InternalMethod() { }
    protected internal void ProtectedInternalMethod() { }
    private protected void PrivateProtectedMethod() { }
}
```

C# 11 introduced file-scoped types using the `file` modifier. A `file class` is visible only within the same source file.

```csharp
file class FileOnlyHelper
{
    public static void Help() => Console.WriteLine("Helper in same file");
}
```

A file-scoped type is useful for implementation details that should not leak outside the source file.

### 2.4 Interfaces

```csharp
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine(message);
}
```

### 2.4 Method ambiguity

Method ambiguity in C# occurs when the compiler cannot determine which method overload or implementation to call. This is always a compile-time error, because the compiler resolves overloads and interface implementations before the program runs.

Common ambiguity scenarios:

- Overloads with the same name and arguments that match multiple parameter sets.
- Interfaces exposing the same method names where the implementing class must disambiguate.
- Multiple methods with the same name and signature in the same type or inheritance chain.

Example of overload ambiguity:

```csharp
void Write(int value) { }
void Write(string value) { }

Write(42);
Write("hello");

// This is ambiguous if both overloads can accept null:
Write(null); // compile-time error
```

Example with interfaces and explicit implementation:

```csharp
interface IReader { void Read(); }
interface IWriter { void Read(); }

class FileHandler : IReader, IWriter
{
    void IReader.Read() => Console.WriteLine("Reader");
    void IWriter.Read() => Console.WriteLine("Writer");
}
```

If the compiler cannot choose between candidates, it reports an error rather than producing runtime ambiguity.

### 2.5 Interface default implementations

C# lets interfaces provide a default implementation for methods. This helps evolve interfaces without breaking existing implementers.

```csharp
public interface ILogger
{
    void Log(string message);
    void LogVerbose(string message) => Log($"VERBOSE: {message}");
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine(message);
}
```

Implementing types can override the default method, but they do not have to.

### 2.6 Records

Records are useful for immutable data.

```csharp
public record User(string Name, int Age);
```

---

## 3. Generics and Collections

### 3.1 Generics

```csharp
public class Repository<T>
{
    private readonly List<T> _items = new();
    public void Add(T item) => _items.Add(item);
    public IReadOnlyList<T> GetAll() => _items;
}
```

Generic constraints limit what types are allowed for a type parameter.

```csharp
public class Factory<T> where T : new()
{
    public T Create() => new T();
}

public class NullableHolder<T> where T : class
{
    public T? Value { get; set; }
}

public class ValueHolder<T> where T : struct
{
    public T Value { get; set; }
}
```

Common generic constraints:

- `where T : class` - `T` must be a reference type.
- `where T : struct` - `T` must be a non-nullable value type.
- `where T : new()` - `T` must have a public parameterless constructor.
- `where T : SomeBase` - `T` must derive from `SomeBase`.
- `where T : ISomeInterface` - `T` must implement `ISomeInterface`.
- `where T : notnull` - `T` cannot be nullable.

The `default` expression returns the default value for a type parameter.

```csharp
public static T GetDefault<T>() => default;

Console.WriteLine(GetDefault<int>());    // 0
Console.WriteLine(GetDefault<string>()); //
```

For a value type, `default` is the zeroed value. For a reference type, `default` is `null`.

### 3.2 Collection types

Common collections:
- `List<T>`
- `Dictionary<TKey, TValue>`
- `HashSet<T>`
- `Queue<T>`
- `Stack<T>`

### 3.3 LINQ

LINQ enables expressive queries over collections.

```csharp
var numbers = new[] { 1, 2, 3, 4, 5 };
var evens = numbers.Where(n => n % 2 == 0).ToList();
var sum = numbers.Sum();
```

### 3.4 IList vs ICollection

- `ICollection<T>` provides size, enumeration, add/remove operations, and support for collection semantics.
- `IList<T>` extends `ICollection<T>` with indexed access and insert/remove at a specific position.

Use `ICollection<T>` when you need collection semantics without random access. Use `IList<T>` when order and indexing matter.

```csharp
void ProcessCollection(ICollection<string> items)
{
    foreach (var item in items)
    {
        Console.WriteLine(item);
    }
}

void UpdateList(IList<string> items)
{
    items.Insert(0, "first");
    Console.WriteLine(items[0]);
}
```

### 3.5 IEnumerable vs IEnumerator

- `IEnumerable<T>` exposes an enumerator and supports `foreach` iteration.
- `IEnumerator<T>` represents the actual iteration state and current value.

`IEnumerable<T>` is the common abstraction for data sources. `IEnumerator<T>` is typically used by the runtime or for custom iteration routines.

```csharp
IEnumerable<int> GetNumbers() => new[] { 1, 2, 3 };

using var enumerator = GetNumbers().GetEnumerator();
while (enumerator.MoveNext())
{
    Console.WriteLine(enumerator.Current);
}
```

### 3.6 Read-only, frozen, and immutable collections

- `IReadOnlyCollection<T>` and `IReadOnlyList<T>` expose read-only collection views.
- `ReadOnlyCollection<T>` wraps an existing list to prevent mutation through its API.
- Immutable collections in `System.Collections.Immutable` provide truly immutable data structures.

```csharp
IReadOnlyList<int> readOnly = new List<int> { 1, 2, 3 };
var frozen = new ReadOnlyCollection<int>(new List<int> { 1, 2, 3 });

using System.Collections.Immutable;
var immutable = ImmutableList.Create(1, 2, 3);
var added = immutable.Add(4); // returns a new list
```

Use read-only views when you want to prevent consumer modification and immutable collections when you want no mutation at all.

### 3.7 Concurrent collections

The `System.Collections.Concurrent` namespace provides thread-safe collection types.

- `ConcurrentDictionary<TKey, TValue>`
- `ConcurrentQueue<T>`
- `ConcurrentStack<T>`
- `BlockingCollection<T>`

```csharp
var dict = new ConcurrentDictionary<string, int>();
dict.TryAdd("a", 1);
dict.AddOrUpdate("a", 1, (_, existing) => existing + 1);
```

Use these collections for shared data structures in multithreaded applications.

---

## 4. Exception Handling

### 4.1 Try/Catch

```csharp
try
{
    var result = 10 / 0;
}
catch (DivideByZeroException ex)
{
    Console.WriteLine("Division by zero: " + ex.Message);
}
```

### 4.2 Finally

```csharp
try
{
    // work
}
finally
{
    // cleanup
}
```

### 4.3 Using declarations

Use `using` for disposable resources.

```csharp
using var file = File.OpenRead("data.txt");
```

---

## 5. Delegates, Events, and Lambda Expressions

### 5.1 Delegates

A delegate is a type-safe function reference.

```csharp
public delegate int MathOp(int x, int y);

MathOp add = (x, y) => x + y;
Console.WriteLine(add(3, 4));
```

### 5.2 Events

```csharp
public class Clock
{
    public event EventHandler? Tick;
    protected virtual void OnTick() => Tick?.Invoke(this, EventArgs.Empty);
}
```

### 5.3 Lambda expressions

```csharp
var squares = numbers.Select(n => n * n).ToList();
```

### 5.4 `ref`, `out`, and `in` parameter modifiers

C# supports parameter modifiers for passing by reference or enforcing readonly semantics.

```csharp
void Increment(ref int value)
{
    value += 1;
}

void LoadValue(out int result)
{
    result = 42;
}

void PrintPoint(in Point point)
{
    Console.WriteLine($"{point.X}, {point.Y}");
}

int a = 5;
Increment(ref a);   // a is initialized and updated

int b;
LoadValue(out b);    // b does not need to be initialized before the call

var p = new Point { X = 1, Y = 2 };
PrintPoint(in p);    // p is passed by readonly reference
```

- `ref` passes a variable by reference and allows the method to read/write the caller's storage. The variable must be initialized before calling.
- `out` passes by reference for output-only values. The caller's variable need not be initialized, but the method must assign it before returning.
- `in` passes by readonly reference. It avoids copying large value types while preventing modification inside the method.

You can also use `ref` locals and returns to work with references directly:

```csharp
int[] values = { 10, 20, 30 };
ref int second = ref values[1];
second = 50; // updates values[1]

ref int GetReference(int[] arr, int index)
{
    return ref arr[index];
}

ref int item = ref GetReference(values, 2);
item = 60; // updates values[2]
```

Use these modifiers when you need direct access to caller storage, want to return mutable references, or wish to avoid copies of large structs.

### 5.5 Closures

C# closures capture variables from the surrounding scope, including loop variables.

```csharp
Action?[] actions = new Action?[3];
for (int i = 0; i < 3; i++)
{
    int copy = i;
    actions[i] = () => Console.WriteLine(copy);
}

foreach (var action in actions)
{
    action?.Invoke();
}
```

This prints `0`, `1`, `2` because each closure captures its own copy of the variable.

### 5.6 Why extension methods are static

Extension methods are static because they are syntactic sugar for calling a static helper method with the target instance as the first parameter. They allow adding methods to existing types without inheritance.

```csharp
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }
}

string? text = null;
Console.WriteLine(text.IsNullOrEmpty());
```

Behind the scenes, the call compiles to `StringExtensions.IsNullOrEmpty(text)`.

---

## 6. Advanced Language Features

### 6.1 `ref` returns and locals

```csharp
int[] values = { 1, 2, 3 };
ref int second = ref values[1];
second = 20;
```

### 6.2 `Span<T>` and `Memory<T>`

`Span<T>` is a stack-only view over contiguous memory.

```csharp
Span<char> buffer = stackalloc char[100];
```

`Memory<T>` works on the heap and can be used async.

---

## 7. Threading and Concurrency

### 7.1 `Thread`

```csharp
var thread = new Thread(() => Console.WriteLine("running"));
thread.Start();
thread.Join();
```

### 7.2 `Task`

Tasks represent asynchronous work.

```csharp
Task.Run(() => Console.WriteLine("background work"));
```

### 7.3 Synchronization primitives

- `lock` — mutual exclusion
- `Mutex` — cross-process lock
- `SemaphoreSlim` — limited concurrency
- `ManualResetEventSlim` / `AutoResetEvent`

Example:

```csharp
private readonly object _lock = new();

lock (_lock)
{
    // critical section
}
```

### 7.4 `Interlocked`

For low-level atomic operations:

```csharp
Interlocked.Increment(ref counter);
```

### 7.5 Race conditions

A race condition occurs when multiple threads access shared state without proper synchronization, and the result depends on execution order.

```csharp
int counter = 0;
Parallel.For(0, 1000, _ =>
{
    counter++; // not thread-safe
});
```

The increment is not atomic, so the final value may be less than 1000.

### 7.6 Thread synchronization

Common synchronization primitives:

- `lock` — protects critical sections
- `Monitor` — advanced locking
- `Mutex` — cross-process lock
- `SemaphoreSlim` — limits concurrency
- `ManualResetEventSlim` — wait signal

Example:

```csharp
private readonly object _lock = new();

lock (_lock)
{
    // critical section
}
```

Use synchronization when threads share mutable state.

---

## 8. Async/Await and Asynchronous Programming

### 8.1 `async`/`await`

```csharp
public async Task<string> FetchUrlAsync(string url)
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}
```

### 8.2 Task-based async pattern

Use `Task` for asynchronous work and `Task<T>` for results.

````csharp
public async Task ProcessAsync()
{
    var task = DoWorkAsync();
    await task;
}
````

### 8.3 Avoid blocking

Never call `.Result` or `.Wait()` on `Task` in async code unless you are in a synchronous entry point.

### 8.4 ConfigureAwait

Use `ConfigureAwait(false)` in library code to avoid capturing context:

```csharp
await something.ConfigureAwait(false);
```

### 8.5 `ValueTask`

Use `ValueTask<T>` for high-performance async paths when the result is often already available.

```csharp
public async ValueTask<int> GetValueAsync()
{
    return 42;
}
```

---

## 9. Performance and Optimization Techniques

### 9.1 Avoid allocations

- Reuse buffers with `ArrayPool<T>`
- Use `Span<T>` for stack-based slicing
- Prefer `string.Create` over concatenation in hot paths

### 9.1.1 Memory management in C#

.NET uses managed memory and a garbage collector. Objects are allocated on the managed heap, and the runtime periodically reclaims unreachable memory.

- Small objects are allocated in generation 0.
- Long-lived objects are promoted to higher generations.
- `IDisposable` is used for unmanaged resources and should be released with `using`.
- `GC.Collect()` can be invoked manually, but it is usually best left to the runtime.

```csharp
using var stream = File.OpenRead("data.txt");
```

Minimize allocations, keep objects short-lived when possible, and prefer value types for small data when appropriate.

### 9.2 Use `readonly struct`

For small value types that should not mutate.

```csharp
public readonly struct Point
{
    public int X { get; }
    public int Y { get; }
}
```

`readonly struct` makes all instance fields readonly and prevents mutation through its instance members. It is ideal for small immutable value types.

A regular `struct` can also have individual readonly members:

```csharp
public struct Rectangle
{
    public readonly int Width;
    public readonly int Height;
}
```

This is not the same as `readonly struct`; it only protects those fields, not all instance members.

### 9.3 Use `in` parameters

Pass large structs by reference without allowing mutation.

```csharp
public void Process(in Point point) { }
```

### 9.4 Minimize boxing

Avoid passing value types to APIs that require `object`.

### 9.5 Use `async` sparingly in hot paths

Async is powerful but has overhead. Use it primarily for I/O-bound work.

### 9.6 `Task.Run` and thread pool use

Avoid `Task.Run` for CPU-bound work in ASP.NET; use it for background work in desktop apps.

---

## 10. Advanced Threading Concepts

### 10.1 `ThreadPool`

The .NET thread pool manages worker threads for short-lived tasks.

### 10.2 `Parallel` and PLINQ

For data-parallel work:

```csharp
Parallel.For(0, 10, i => Console.WriteLine(i));
```

### 10.3 `async` and `IAsyncEnumerable<T>`

Stream data asynchronously with `await foreach`.

```csharp
public async IAsyncEnumerable<int> CountAsync()
{
    for (var i = 0; i < 5; i++)
    {
        await Task.Delay(100);
        yield return i;
    }
}
```

### 10.4 Task parallelism vs multithreading vs the Parallel class

- **Multithreading** is the general concept of running multiple threads in parallel, which can be managed directly with `Thread` or thread-pool abstractions.
- **Task parallelism** uses `Task`, `Task<T>`, and the task scheduler to represent asynchronous or concurrent work, often with higher-level coordination and better exception handling.
- **`Parallel`** methods like `Parallel.For` and `Parallel.ForEach` are built on the thread pool and optimized for data-parallel workloads.

Use `Task` for asynchronous and potentially non-CPU-bound operations. Use `Parallel` when you have a CPU-bound loop or batch that can be executed in parallel across multiple cores.

### 10.5 Cancellation

Use `CancellationToken` to stop work cooperatively.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    await Task.Delay(1000, cancellationToken);
}
```

### 10.5 `IAsyncDisposable`

Dispose async resources with `await using`.

```csharp
await using var stream = new FileStream(...);
```

---

## 11. Application and Architecture Patterns

### 11.1 Dependency Injection

Use DI to manage object lifetimes.

```csharp
services.AddSingleton<ILogger, ConsoleLogger>();
```

### 11.2 Logging and configuration

Use `Microsoft.Extensions.Logging` and `Microsoft.Extensions.Configuration`.

### 11.3 Microservices and web APIs

ASP.NET Core is the primary framework for building web services.

---

## 12. Practical tips for modern C# development

- Use `dotnet format` and `dotnet analyzers`.
- Prefer `var` when the type is clear.
- Prefer expression-bodied members for small methods.
- Keep async code composable and avoid synchronous waits.
- Benchmark with `BenchmarkDotNet` when optimizing.

---

## Quick Reference: threading, async, and optimization

- Use `Task` and `async` for asynchronous I/O.
- Use `ConfigureAwait(false)` in libraries.
- Use `ValueTask` when you need low-overhead hot-path results.
- Use `lock`, `SemaphoreSlim`, and `Interlocked` for synchronization.
- Use `Span<T>`/`Memory<T>` for zero-copy memory access.
- Use `Task.Run` carefully, avoiding it in server request contexts unless absolutely necessary.

---

## Closing

This runbook is intended as a practical guide from C# fundamentals to advanced concurrency and async programming. Use it as a reference while building .NET applications.

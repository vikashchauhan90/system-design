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

### 2.3 Interfaces

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

### 2.4 Records

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

### 9.2 Use `readonly struct`

For small value types that should not mutate.

```csharp
public readonly struct Point
{
    public int X { get; }
    public int Y { get; }
}
```

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

### 10.4 Cancellation

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

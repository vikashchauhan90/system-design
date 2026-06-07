# FluentAssertions: Complete Fluent Assertion Library Reference

## Document Overview

This document provides a comprehensive analysis of FluentAssertions' architectural patterns, assertion execution strategies, formatting systems, and extensibility mechanisms. FluentAssertions is a .NET library that provides a fluent interface for writing assertions in unit tests. Unlike traditional assertion libraries that use method names like `Assert.AreEqual()`, FluentAssertions enables natural language assertions such as `actual.Should().Be(expected).Because("...")`. This document covers the core architecture, assertion execution chains, formatting and reporting systems, object graph equivalency algorithms, extensibility points, and integration patterns that power this popular testing library.

---

## Core Architectural Patterns

### 1. Fluent Interface Pattern

**Purpose**: Enable expressive, readable assertions by chaining method calls in a natural language style.

**Core Principle**: Instead of writing `Assert.AreEqual(expected, actual)`, developers write `actual.Should().Be(expected)`. This "grammar" of assertions reads like English, making tests more declarative and self-documenting.

**The Entry Point (`Should()`)** :
FluentAssertions adds an extension method named `Should()` to every .NET type. This method is the gateway to all assertion functionality:

```csharp
// The Should() extension method is the entry point
public static class AssertionExtensions
{
    public static BooleanAssertions Should(this bool? actual)
    {
        return new BooleanAssertions(actual);
    }
    
    public static StringAssertions Should(this string actual)
    {
        return new StringAssertions(actual);
    }
    
    public static GenericObjectAssertions Should(this object actual)
    {
        return new GenericObjectAssertions(actual);
    }
}
```

**Fluent Interface Characteristics**:

| Characteristic | Implementation | Benefit |
|----------------|----------------|---------|
| Method chaining | Each assertion returns `AndConstraint<T>` | Multiple assertions on same object |
| Natural language | `Be()`, `BeGreaterThan()`, `Contain()` | Readable test output |
| Self-referential generics | `ReferenceTypeAssertions<TSubject, TAssertions>` | Preserves assertion type through chain |
| Context preservation | `Subject` property exposes original object | Access to original value |

**Method Chaining Example**:
```csharp
// Each assertion returns an AndConstraint allowing chaining
customer.Should()
    .NotBeNull()
    .And.BeActive()
    .And.HaveName("John");
```

### 2. Separation of Assertion and Execution

**Purpose**: Decouple assertion logic from execution context, enabling flexible failure handling strategies.

**Architectural Layers**:

| Layer | Responsibility | Key Components |
|-------|----------------|----------------|
| **Assertion API** | Define what to assert | `Should()` extensions, assertion classes |
| **Assertion Chain** | Build and evaluate conditions | `AssertionChain`, `ForCondition`, `FailWith` |
| **Execution Strategy** | Handle failures (throw/collect) | `IAssertionStrategy`, `DefaultAssertionStrategy`, `CollectingAssertionStrategy` |
| **Test Framework Adapter** | Raise framework-specific exceptions | `ITestFramework`, `TestFrameworkProvider` |

**Why Separation Matters**:

| Scenario | Without Strategy Pattern | With Strategy Pattern |
|----------|-------------------------|----------------------|
| Single assertion failure | Exception thrown immediately | Configurable behavior |
| Multiple assertions in a scope | First failure stops all assertions | All failures collected and reported together |
| Custom test framework | Hard-coded exception types | Pluggable via `ITestFramework` |

### 3. Strategy Pattern for Failure Handling

**Purpose**: Allow different assertion failure handling strategies depending on execution context.

**DefaultAssertionStrategy** (immediate throw):
- Used for individual assertions outside of an `AssertionScope`
- Throws the appropriate test framework exception immediately
- Behavior matches traditional assertion libraries

**CollectingAssertionStrategy** (batch collection):
- Used within an `AssertionScope` context
- Collects all failures without throwing
- On scope disposal, throws aggregated exception containing all failures

```csharp
// Without AssertionScope: first failure throws
var customer = new Customer();
customer.Should().NotBeNull();  // Passes
customer.Name.Should().Be("John");  // Throws if Name is null

// With AssertionScope: all failures reported together
using (new AssertionScope())
{
    customer.Should().NotBeNull();
    customer.Name.Should().Be("John");
    customer.Age.Should().Be(25);
    // Disposal aggregates all failures into one exception
}
```

---

## Assertion Chain & Execution Engine

### 4. AssertionChain Architecture

**Purpose**: Provide the internal fluent API for building assertion conditions and formatting failure messages.

**AssertionChain Flow**:

```
AssertionChain
    │
    ├── BecauseOf() ──► Set reason/context
    │
    ├── ForCondition() ──► Evaluate boolean condition
    │         │
    │         ▼
    │    condition true? ──Yes──► Continue to next assertion
    │         │
    │         No
    │         ▼
    ├── FailWith() ──► Format and throw exception
    │
    └── Then ──► Chain to next assertion in sequence
```

**Key Methods of AssertionChain**:

| Method | Purpose | Example |
|--------|---------|---------|
| `BecauseOf(string, params object[])` | Adds explanation for assertion | `chain.BecauseOf("validation failed", args)` |
| `ForCondition(bool)` | Evaluates the assertion condition | `chain.ForCondition(value != null)` |
| `FailWith(string, params object[])` | Formats and throws failure | `chain.FailWith("Expected {0}, found {1}", expected, actual)` |
| `Then` | Continues to next assertion | `chain.Then.ForCondition(anotherCondition)` |
| `Given<T>(Func<T>)` | Lazily evaluates projection | `chain.Given(() => Subject.GetFiles())` |

**Complete Example** (custom assertion using AssertionChain):

```csharp
public AndConstraint<DirectoryInfoAssertions> ContainFile(
    string filename, string because = "", params object[] becauseArgs)
{
    Execute.Assertion
        .BecauseOf(because, becauseArgs)                    // Set context
        .ForCondition(!string.IsNullOrEmpty(filename))     // Validate input
        .FailWith("You can't assert a file exist if you don't pass a proper name")
        .Then
        .Given(() => Subject.GetFiles())                    // Project to files
        .ForCondition(files => files.Any(f => f.Name == filename))
        .FailWith("Expected {context:directory} to contain {0}{reason}, but found {1}.",
            _ => filename, 
            files => files.Select(f => f.Name));
    
    return new AndConstraint<DirectoryInfoAssertions>(this);
}
```

### 5. WithExpectation for Reusable Messages

**Purpose**: Reuse the same failure message prefix across multiple nested assertions.

```csharp
// Sets expectation message reused by all nested FailWith calls
chain
    .BecauseOf(because, becauseArgs)
    .WithExpectation("Expected the month part of {context:the date} to be {0}{reason}", expected, 
        chain => chain
            .ForCondition(Subject.HasValue)
            .FailWith(", but found a <null> DateOnly.")
            .Then
            .ForCondition(Subject.Value.Month == expected)
            .FailWith(", but found {0}.", Subject.Value.Month));
```

### 6. AssertionScope Implementation

**Purpose**: Provide hierarchical context for collecting multiple assertion failures and managing formatting options.

**AssertionScope Capabilities**:

| Feature | Implementation |
|---------|----------------|
| Implicit scoping | Uses `AsyncLocal<T>` for ambient context |
| Explicit scoping | `using (new AssertionScope()) { ... }` |
| Context hierarchy | Nested scopes for sub-assertions |
| Formatting control | Override `context` placeholder |
| Failure aggregation | Collects failures until disposal |

**Custom Context Override**:
```csharp
foreach (DirectoryInfo subDirectory in Subject.GetDirectories())
{
    using (new AssertionScope(subDirectory.FullName))  // Overrides {context}
    {
        subDirectory.Should().ContainFile(filename, because, becauseArgs);
        // Failure messages will show the specific subdirectory
    }
}
```

---

## Formatting & Object Rendering

### 7. Value Formatter System

**Purpose**: Convert objects to human-readable strings for inclusion in assertion failure messages.

**IValueFormatter Interface**:

```csharp
public interface IValueFormatter
{
    bool CanHandle(object value);
    void Format(object value, FormattedObjectGraph formattedGraph, 
                FormattingContext context, FormatChild formatChild);
}
```

**Key Components**:

| Component | Responsibility |
|-----------|----------------|
| `Formatter` | Registry of all formatters; selects appropriate formatter |
| `FormattedObjectGraph` | Collects text fragments; handles indentation; enforces line limits |
| `FormattingContext` | Provides formatting configuration (useLineBreaks, etc.) |
| `FormatChild` | Delegate for recursive formatting of nested objects |

**Custom Formatter Example**:

```csharp
public class DirectoryInfoValueFormatter : IValueFormatter
{
    public bool CanHandle(object value) => value is DirectoryInfo;
    
    public void Format(object value, FormattedObjectGraph formattedGraph, 
                       FormattingContext context, FormatChild formatChild)
    {
        var info = (DirectoryInfo)value;
        string result = $"{info.FullName} ({info.GetFiles().Length} files, " +
                        $"{info.GetDirectories().Length} directories)";
        
        if (context.UseLineBreaks)
            formattedGraph.AddLine(result);      // Separate line
        else
            formattedGraph.AddFragment(result);   // Same line
    }
}
```

**Registration**:
```csharp
// Global registration
Formatter.AddFormatter(new DirectoryInfoValueFormatter());

// Per-scope registration
using var scope = new AssertionScope();
scope.FormattingOptions.AddFormatter(new DirectoryInfoValueFormatter());
```

### 8. Specialized Formatter Capabilities

**Cyclic Reference Detection**:
The formatting system automatically detects cyclic references in object graphs to prevent infinite recursion and excessive output.

**Conditional Formatting**:
Formatters can respond to `FormattingContext` properties to adjust output format:
- `UseLineBreaks`: Determines whether output should be on its own line
- `MaximumDepth`: Limits recursion depth to prevent excessive output
- `FormattingAction`: Controls whether formatting is for display or comparison

**Limiting Output for Collections**:
```csharp
public class EnumerableValueFormatter : IValueFormatter
{
    protected override int MaxItems => 10;  // Only show first 10 items
    // ...
}
```

---

## Object Graph Equivalency (BeEquivalentTo)

### 9. Equivalency Algorithm Overview

**Purpose**: Perform deep comparison of object graphs, identifying structural differences between expected and actual objects.

**Core Principles**:

| Principle | Description |
|-----------|-------------|
| **Structural comparison** | Compares object properties/members, not reference equality |
| **Recursive by default** | Traverses entire object graph (configurable depth limit) |
| **Configurable** | Extensive options to customize comparison rules |
| **Cyclic reference tolerant** | Detects and handles circular references |
| **Format-aware** | Distinguishes value types and reference types automatically |

**Key Features**:

```csharp
// Basic equivalency comparison
actual.Should().BeEquivalentTo(expected);

// With configuration
actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(e => e.Id)                    // Ignore specific property
    .IncludingNestedObjects()                 // Include all nested objects
    .WithStrictOrdering()                     // Enforce collection order
    .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, 1.Seconds()))
    .WhenTypeIs<DateTime>());
```

### 10. Equivalency Plan & Step Execution

**Purpose**: Process object graphs through ordered steps, each responsible for a specific type of comparison.

**IEquivalencyStep Interface**:

```csharp
public interface IEquivalencyStep
{
    EquivalencyResult Handle(
        Comparands comparands, 
        IEquivalencyValidationContext context, 
        IEquivalencyValidator nestedValidator);
}
```

**Built-in Step Order** (simplified):

| Step | Responsibility | Handles |
|------|----------------|---------|
| **TryConversionEquivalencyStep** | Type conversion | Different but convertible types |
| **ReferenceEqualityEquivalencyStep** | Reference equality | Same object reference |
| **SimpleEqualityEquivalencyStep** | Value equality | Types overriding `Equals()` |
| **GenericDictionaryEquivalencyStep** | Dictionary comparison | `IDictionary` types |
| **EnumerableEquivalencyStep** | Collection comparison | `IEnumerable` types |
| **StructuralEqualityEquivalencyStep** | Property-by-property | Complex objects |

**Custom Step Implementation**:

```csharp
public class SimpleEqualityEquivalencyStep : IEquivalencyStep
{
    public EquivalencyResult Handle(Comparands comparands, 
        IEquivalencyValidationContext context,
        IEquivalencyValidator nestedValidator)
    {
        if (!context.Options.IsRecursive && !context.CurrentNode.IsRoot)
        {
            comparands.Subject.Should().Be(comparands.Expectation, 
                context.Reason.FormattedMessage, 
                context.Reason.Arguments);
            return EquivalencyResult.EquivalencyProven;
        }
        return EquivalencyResult.ContinueWithNext;
    }
}

// Usage
subject.Should().BeEquivalentTo(expected, options => 
    options.Using(new CustomEquivalencyStep()));
```

### 11. Value Type vs. Complex Type Detection

**Purpose**: Determine whether to recursively traverse an object or treat it as atomic.

**Default Behavior**:
- Types that override `Object.Equals()` are treated as value types
- Primitive types are always value types
- Anonymous types are always compared by members (not treated as values)

**Explicit Configuration**:
```csharp
// Treat DirectoryInfo as value type (compare by Equals)
subject.Should().BeEquivalentTo(expected, options => 
    options.ComparingByValue<DirectoryInfo>());

// Treat as complex type (compare members)
subject.Should().BeEquivalentTo(expected, options => 
    options.ComparingByMembers<DirectoryInfo>());

// Global configuration
AssertionConfiguration.Current.Equivalency.Modify(options => 
    options.ComparingByValue<DirectoryInfo>());
```

### 12. Cyclic Reference Handling

**Purpose**: Prevent infinite loops when comparing object graphs with circular references.

**Detection Mechanism**:
- The equivalency validation tracks object pairs already visited
- When a cycle is detected, the comparison for that branch terminates
- Prevents `StackOverflowException` and excessive execution time

**Custom Cyclic Reference Resolution**:
```csharp
public class CyclicReferenceTrackingRule : IEquivalencyStep
{
    private HashSet<(object, object)> _visited = new();
    
    public EquivalencyResult Handle(...)
    {
        var pair = (comparands.Subject, comparands.Expectation);
        if (_visited.Contains(pair))
            return EquivalencyResult.EquivalencyProven;  // Already compared
            
        _visited.Add(pair);
        return EquivalencyResult.ContinueWithNext;
    }
}
```

### 13. Member Selection & Matching Rules

**Member Selection** (which members to include):

| Rule | Purpose |
|------|---------|
| `IncludeAllDeclaredProperties` | Include all properties |
| `IncludeAllRuntimeProperties` | Include all runtime properties |
| `ExcludeMember` | Exclude specific members by expression |
| `IncludeMember` | Include specific members |

**Member Matching** (how to map subject to expectation):

| Rule | Behavior |
|------|----------|
| `MustMatchByNameRule` | Members must match by exact name |
| `TryMatchByNameRule` | Try matching by name; fallback to nothing |
| `Custom matching rule` | Implement `IMemberMatchingRule` for custom logic |

---

## Extensibility Points

### 14. Custom Assertions Framework

**Purpose**: Create domain-specific assertion methods that follow FluentAssertions conventions.

**Extension Point Components**:

| Component | Purpose |
|-----------|---------|
| `Should()` extension | Entry point returning custom assertion class |
| Custom assertion class | Contains assertion methods |
| `ReferenceTypeAssertions<T, TSelf>` base | Provides common reference assertions |
| `AndConstraint<T>` | Enables method chaining |
| `AndWhichConstraint<T, TWhich>` | Enables chaining with result access |

**Custom Assertion Implementation**:

```csharp
// Step 1: Extension method
public static class DirectoryInfoExtensions
{
    public static DirectoryInfoAssertions Should(this DirectoryInfo instance)
    {
        return new DirectoryInfoAssertions(instance, AssertionChain.GetOrCreate());
    }
}

// Step 2: Assertion class
public class DirectoryInfoAssertions : 
    ReferenceTypeAssertions<DirectoryInfo, DirectoryInfoAssertions>
{
    private readonly AssertionChain _chain;
    
    public DirectoryInfoAssertions(DirectoryInfo instance, AssertionChain chain) 
        : base(instance)
    {
        _chain = chain;
    }
    
    protected override string Identifier => "directory";
    
    public AndConstraint<DirectoryInfoAssertions> ContainFile(
        string filename, string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(!string.IsNullOrEmpty(filename))
            .FailWith("You can't assert a file exists with an empty name")
            .Then
            .Given(() => Subject.GetFiles())
            .ForCondition(files => files.Any(f => f.Name == filename))
            .FailWith("Expected {context:directory} to contain file {0}{reason}, but found {1}.",
                _ => filename, 
                files => files.Select(f => f.Name));
        
        return new AndConstraint<DirectoryInfoAssertions>(this);
    }
}
```

### 15. AndWhichConstraint for Chained Result Access

**Purpose**: Enable chained assertions on the result of an operation.

```csharp
public AndWhichConstraint<DirectoryInfoAssertions, FileInfo> ContainFile(
    string filename, string because = "", params object[] becauseArgs)
{
    FileInfo foundFile = null;
    
    _chain
        .BecauseOf(because, becauseArgs)
        .Given(() => Subject.GetFiles())
        .ForCondition(files => 
        {
            foundFile = files.FirstOrDefault(f => f.Name == filename);
            return foundFile != null;
        })
        .FailWith("Expected {context:directory} to contain file {0}{reason}", filename);
    
    // AndWhichConstraint enables access to the found file
    return new AndWhichConstraint<DirectoryInfoAssertions, FileInfo>(this, foundFile);
}

// Usage: chain multiple assertions on the result
directoryInfo.Should()
    .ContainFile("trace.dmp")
    .Which.Path.Should()
    .BeginWith("c:\\files");
```

### 16. Custom Value Formatters

**Purpose**: Control how custom types are displayed in failure messages.

**Implementation Options**:

| Approach | When to Use |
|----------|-------------|
| Direct `IValueFormatter` implementation | Full control over formatting behavior |
| `DefaultValueFormatter` override | Simplified for common scenarios |
| `ToString()` override | Simple cases (library will use ToString as fallback) |

**DefaultValueFormatter Example**:
```csharp
public class CustomTypeFormatter : DefaultValueFormatter
{
    public override bool CanHandle(object value) => value is CustomType;
    
    protected override MemberInfo[] GetMembers(Type type) =>
        base.GetMembers(type).Where(m => m.Name != "SensitiveData").ToArray();
}
```

### 17. Custom Equivalency Steps

**Purpose**: Inject custom comparison logic into the `BeEquivalentTo` pipeline.

**Integration Options**:

| Scope | API |
|-------|-----|
| Single assertion | `options.Using(new CustomStep())` |
| Global | `AssertionConfiguration.Current.Equivalency.Modify(options => options.Using(...))` |
| Order control | Steps execute in registration order; final default step is last |

---

## Specialized Assertions

### 18. Execution Time Assertions

**Purpose**: Verify that method or action execution times stay within defined limits.

**Supported Assertions**:

| Method | Description |
|--------|-------------|
| `BeLessThanOrEqualTo(TimeSpan)` | Execution ≤ limit |
| `BeLessThan(TimeSpan)` | Execution < limit |
| `BeGreaterThanOrEqualTo(TimeSpan)` | Execution ≥ limit |
| `BeGreaterThan(TimeSpan)` | Execution > limit |
| `BeCloseTo(TimeSpan, TimeSpan)` | Execution within delta of target |
| `CompleteWithinAsync(TimeSpan)` | Async task completes within limit |
| `NotCompleteWithinAsync(TimeSpan)` | Async task does not complete within limit |
| `ThrowWithinAsync<TException>(TimeSpan)` | Async task throws exception within limit |

**Usage Examples**:

```csharp
// Synchronous method
var subject = new SomeClass();
subject.ExecutionTimeOf(s => s.ExpensiveMethod())
    .Should().BeLessThanOrEqualTo(500.Milliseconds());

// Action delegate
var someAction = () => Thread.Sleep(100);
someAction.ExecutionTime().Should().BeLessThan(200.Milliseconds());

// Async task
Func<Task<int>> asyncFunc = () => SomeAsyncMethod();
await asyncFunc.Should()
    .CompleteWithinAsync(100.Milliseconds())
    .WithResult(42);
```

### 19. Exception Assertions

**Purpose**: Verify that code throws expected exceptions with specific properties.

**Exception Assertion API**:
```csharp
// Basic exception assertion
Action act = () => subject.ThrowException();
act.Should().Throw<InvalidOperationException>();

// With additional checks
act.Should().Throw<InvalidOperationException>()
    .Where(ex => ex.Message.Contains("specific"))
    .And.Message.Should().StartWith("Error");

// No exception thrown
act.Should().NotThrow();

// Exception not of specific type
act.Should().NotThrow<ArgumentNullException>();
```

---

## Integration & Configuration

### 20. Test Framework Integration

**Purpose**: Raise appropriate exception types for each test framework without hardcoding.

**Supported Frameworks**:

| Framework | Exception Type |
|-----------|----------------|
| xUnit | `Xunit.Sdk.XunitException` |
| NUnit | `NUnit.Framework.AssertionException` |
| MSTest | `Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException` |
| **Fallback** | `System.Exception` |

**ITestFramework Interface**:
```csharp
public interface ITestFramework
{
    void Throw(string message);
}
```

### 21. Configuration Options

**Global Configuration**:
```csharp
// Equivalency assertion defaults
AssertionConfiguration.Current.Equivalency.Modify(options => 
    options.WithStrictOrdering()
           .ComparingByValue<DirectoryInfo>());

// Value formatting defaults
Formatter.AddFormatter(new CustomFormatter());
```

**Per-Assertion Options**:
```csharp
actual.Should().BeEquivalentTo(expected, options => 
    options.Excluding(x => x.Id)
           .WithStrictOrdering()
           .Using(new CustomEquivalencyStep()));
```

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            TEST CODE                                         │
│                                                                              │
│  actual.Should().Be(expected, "validation failed");                         │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        EXTENSION METHOD (Should)                             │
│                                                                              │
│  public static BooleanAssertions Should(this bool actual)                   │
│  {                                                                          │
│      return new BooleanAssertions(actual, AssertionChain.GetOrCreate());    │
│  }                                                                          │
│                                                                              │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ASSERTION CHAIN CONSTRUCTION                          │
│                                                                              │
│  AssertionChain.GetOrCreate()                                               │
│      │                                                                       │
│      ▼                                                                       │
│  AssertionChain (with IAssertionStrategy from current scope)               │
│      │                                                                       │
│      ▼                                                                       │
│  Chain Methods (.BecauseOf → .ForCondition → .FailWith)                    │
│                                                                              │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    │                                   │
                    ▼                                   ▼
┌───────────────────────────────┐   ┌───────────────────────────────────────┐
│   DEFAULT ASSERTION STRATEGY   │   │      COLLECTING ASSERTION STRATEGY     │
│   (Outside AssertionScope)     │   │      (Inside AssertionScope)           │
│                                │   │                                         │
│   • Throw immediately          │   │   • Collect failures                   │
│   • Exception type from        │   │   • On scope dispose: throw            │
│     test framework             │   │     aggregated exception               │
│                                │   │                                         │
└───────────────┬───────────────┘   └─────────────────┬───────────────────────┘
                │                                     │
                └─────────────────┬───────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         EXCEPTION THROWN                                     │
│                                                                              │
│  Test framework-specific exception with formatted message                  │
│  • Includes {reason} from BecauseOf                                         │
│  • Includes formatted values via IValueFormatter                           │
│  • Includes {context} from AssertionScope                                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         BEQUIVALENTTO PIPELINE                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  actual.Should().BeEquivalentTo(expected)                                   │
│         │                                                                    │
│         ▼                                                                    │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         Equivalency Plan                              │    │
│  │                                                                       │    │
│  │  Step 1: TryConversionEquivalencyStep                               │    │
│  │         │                                                            │    │
│  │         ▼ (no conversion)                                            │    │
│  │  Step 2: ReferenceEqualityEquivalencyStep                            │    │
│  │         │ (not same reference)                                       │    │
│  │         ▼                                                            │    │
│  │  Step 3: SimpleEqualityEquivalencyStep                               │    │
│  │         │ (not value type)                                           │    │
│  │         ▼                                                            │    │
│  │  Step 4: DictionaryEquivalencyStep                                   │    │
│  │         │ (not dictionary)                                           │    │
│  │         ▼                                                            │    │
│  │  Step 5: EnumerableEquivalencyStep                                   │    │
│  │         │ (not collection)                                           │    │
│  │         ▼                                                            │    │
│  │  Step 6: StructuralEqualityEquivalencyStep (Recursive)              │    │
│  │         │                                                            │    │
│  │         └──► For each property:                                      │    │
│  │              • Select members (selection rules)                     │    │
│  │              • Match members (matching rules)                       │    │
│  │              • Compare values (recursive through steps)             │    │
│  │              • Track cycles (prevent infinite recursion)            │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Result: Detailed failure message showing all differences                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | FluentAssertions Component |
|---|------------------|-----------------|---------------------------|
| 1 | Fluent Interface | Method chaining for readable assertions | `Should()`, `AndConstraint` |
| 2 | Strategy Pattern | Pluggable failure handling | `IAssertionStrategy` |
| 3 | Builder Pattern | Complex assertion construction | `AssertionChain` |
| 4 | Template Method | Consistent assertion execution | Base assertion classes |
| 5 | Decorator Pattern | Extend assertions via extension methods | All `Should()` extensions |
| 6 | Visitor Pattern | Object graph traversal | `BeEquivalentTo` |
| 7 | Pipeline Pattern | Sequential equivalency steps | `IEquivalencyStep` |
| 8 | Chain of Responsibility | Step-by-step equivalency | Equivalency plan execution |
| 9 | Observer Pattern | Failure collection | `AssertionScope` |
| 10 | Strategy Pattern (Formatting) | Pluggable object rendering | `IValueFormatter` |
| 11 | Registry Pattern | Formatter lookup | `Formatter` class |
| 12 | Cyclic Detection | Prevent infinite recursion | ObjectTracker in equivalency |
| 13 | Lazy Evaluation | Deferred projection evaluation | `Given<T>()` |
| 14 | Ambient Context | Implicit scope propagation | `AsyncLocal<T>` in `AssertionScope` |
| 15 | AndWhichConstraint | Result access in chain | `AndWhichConstraint<T, TResult>` |

---

## Configuration Reference

### Global Configuration

```csharp
// Formatter registration
Formatter.AddFormatter(new CustomFormatter());

// Equivalency defaults
AssertionConfiguration.Current.Equivalency.Modify(options => 
    options.Using<MyComparer>()
           .When(info => info.RuntimeType == typeof(MyType))
           .WithStrictOrdering());

// Default assertion strategy (via AssertionScope)
using (new AssertionScope()) { /* collecting mode */ }
// Outside scope: immediate throw mode
```

### Per-Assertion Options

```csharp
// Basic comparison options
actual.Should().BeEquivalentTo(expected, options => options
    .Excluding(o => o.Id)
    .ExcludingMissingMembers()
    .WithStrictOrdering()
    .IncludingNestedObjects()
    .AllowingInfiniteRecursion()
    .ComparingByValue<DirectoryInfo>()
    .Using(new CustomEquivalencyStep()));

// Runtime type handling
actual.Should().BeEquivalentTo(expected, options => options
    .ComparingObjectsByValue()
    .RespectingRuntimeTypes());

// Collection-specific options
actual.Should().BeEquivalentTo(expected, options => options
    .WithStrictOrderingFor(x => x.Property)
    .WithoutStrictOrderingFor(x => x.OtherProperty));
```

---

## Performance Characteristics

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Simple assertion (Be, NotBe) | O(1) | Direct comparison |
| Collection assertions (Contain, HaveCount) | O(n) | n = collection size |
| Object equivalency (BeEquivalentTo) | O(object graph size) | Configurable depth |
| Formatting complex object | O(object graph size) | Recursive traversal |
| AssertionScope | O(failure count) | Collects all failures |
| Value formatter chain | O(formatter count) | Linear lookup |

---

## Source Code Reference

| Component | Location (GitHub: fluentassertions/fluentassertions) |
|-----------|------------------------------------------------------|
| Core Execution | `Src/FluentAssertions/Execution/` |
| Assertion Chains | `Src/FluentAssertions/Execution/AssertionChain.cs` |
| Value Formatting | `Src/FluentAssertions/Formatting/` |
| Equivalency | `Src/FluentAssertions/Equivalency/` |
| Primitive Assertions | `Src/FluentAssertions/Primitives/` |
| Collection Assertions | `Src/FluentAssertions/Collections/` |
| Specialized Assertions | `Src/FluentAssertions/Specialized/` |

---

## Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `FluentAssertions` | Core library |
| `FluentAssertions.Analyzers` | Roslyn analyzers for better assertions |
| `FluentAssertions.Json` | JSON-specific assertions |
| `FluentAssertions.Extensions` | TimeSpan extensions (.Milliseconds()) |
| `FluentAssertions.Web` | ASP.NET Core test helpers |

---

## Conclusion

FluentAssertions' design philosophy emphasizes:

- **Expressiveness**: Assertions read like natural language, making tests self-documenting
- **Extensibility**: Multiple extension points for custom assertions, formatters, and equivalency rules
- **Test framework agnosticism**: Pluggable exception handling for all major frameworks
- **Detailed failure messages**: Rich object formatting shows exactly what went wrong
- **Collection of failures**: `AssertionScope` enables multiple assertions before reporting

Key innovations and algorithms include:

- **Fluent Interface Pattern**: Extension methods returning self-referential generic assertion classes for method chaining
- **Strategy Pattern for failures**: `IAssertionStrategy` enables immediate throw or batch collection via `AssertionScope`
- **AssertionChain**: Internal fluent API for building assertion conditions with `ForCondition` → `FailWith` → `Then` chaining
- **WithExpectation**: Reuse failure message prefixes across nested assertions
- **IValueFormatter system**: Pluggable, recursive object rendering with cyclic detection and configurable line limits
- **BeEquivalentTo equivalency engine**: Pipeline of `IEquivalencyStep` for configurable deep object comparison
- **AndWhichConstraint**: Access results within assertion chains for further assertions
- **Cyclic reference handling**: Prevents infinite recursion during equivalency comparison
- **ExecutionTime assertions**: Specialized APIs for performance testing

This combination of algorithms and patterns makes FluentAssertions suitable for:
- **Unit and integration testing**: Expressive assertions that improve test readability
- **Domain-Driven Design**: Test domain logic with natural language expectations
- **API testing**: Validate complex object graphs with `BeEquivalentTo`
- **Performance testing**: Verify execution times don't exceed thresholds
- **Legacy code testing**: Exception assertions for error handling verification
- **BDD-style testing**: Given-When-Then patterns enhanced by fluent grammar

---

*Document Version: 1.0*
*Based on FluentAssertions source code analysis, official documentation, and community resources*
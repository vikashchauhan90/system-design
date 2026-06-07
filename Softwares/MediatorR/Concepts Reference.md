# MediatR: Complete Mediator Pattern Library Reference

## Document Overview

This document provides a comprehensive analysis of MediatR's architectural patterns, message processing algorithms, pipeline behavior execution strategies, and integration mechanisms. MediatR is a simple, unambitious library for implementing the mediator pattern in .NET applications, enabling loose coupling between components by having them communicate indirectly through a mediator object . Unlike traditional direct dependencies where services reference each other explicitly, MediatR acts as a central dispatcher: one component sends a message (request/command/query/notification), and MediatR locates and invokes the appropriate handler(s). This document covers the core mediator implementation, handler resolution strategies, pipeline behavior composition, notification dispatch, request/response abstractions, and cross-cutting concern integration that powers MediatR.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Request/Response (Unicast) Messaging](#requestresponse-unicast-messaging)
3. [Notification (Multicast) Messaging](#notification-multicast-messaging)
4. [Pipeline Behaviors (Middleware)](#pipeline-behaviors-middleware)
5. [Handler Resolution & Caching](#handler-resolution--caching)
6. [Stream Request Processing](#stream-request-processing)
7. [Integration with Dependency Injection](#integration-with-dependency-injection)
8. [Pre/Post Processors](#prepost-processors)
9. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Mediator Pattern Implementation

**Purpose**: Decouple senders and receivers of messages by introducing a mediator object that handles all communications between components.

**Core Principle**: Instead of objects referencing each other directly (creating tight coupling), they communicate through a central mediator. A component sends a message to the mediator, which then routes it to the appropriate handler(s). The sender has no knowledge of which component will handle the message—only that the message will be processed .

**Traditional Direct Communication**:
```
Service A ──direct call──► Service B (tight coupling, Service A knows Service B)
```

**Mediator Pattern Communication**:
```
Service A ──Send(message)──► MediatR ──Locates Handler──► Service B
(Service A knows only the message type, not the handler)
```

**Why the Mediator Pattern Matters**:

| Aspect | Direct Dependencies | Mediator Pattern (MediatR) |
|--------|---------------------|----------------------------|
| **Coupling** | Tight (caller knows callee) | Loose (caller knows only message) |
| **Testing** | Complex mocks for each dependency | Simple mock of IMediator |
| **Cross-cutting concerns** | Duplicate code across services | Centralized in pipeline behaviors |
| **Handler changes** | Modify all callers | Modify only handler registration |
| **Notification scenarios** | Manual iteration over subscribers | Automatic multicast via Publish |

### 2. Message Types Distinction

**Purpose**: MediatR defines two fundamental message types to address different communication patterns: Request/Response (unicast) and Notification (multicast) .

**Message Type Comparison**:

| Aspect | Request/Response (IRequest) | Notification (INotification) |
|--------|----------------------------|------------------------------|
| **Handler count** | Exactly one handler | Zero to many handlers |
| **Return value** | Optional (TResponse or Unit) | None (void/Task) |
| **Exception behavior** | Exception thrown to sender | All handlers execute (collects exceptions in AggregatedException) |
| **Use case** | Command, Query, Operation with result | Event, domain event, broadcast |
| **Method** | `Send` | `Publish` |

**Request/Response Example (Command)**:
```csharp
// Define request with response type
public class CreateOrderCommand : IRequest<OrderCreatedResponse>
{
    public string CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

// Handler processes request, returns response
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderCreatedResponse>
{
    public async Task<OrderCreatedResponse> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // Business logic here
        return new OrderCreatedResponse { OrderId = Guid.NewGuid() };
    }
}
```

**Notification Example (Event)**:
```csharp
// Define notification (no return type)
public class OrderCreatedNotification : INotification
{
    public Guid OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Multiple handlers can process the same notification
public class SendConfirmationEmailHandler : INotificationHandler<OrderCreatedNotification> { ... }
public class UpdateInventoryHandler : INotificationHandler<OrderCreatedNotification> { ... }
public class NotifyWarehouseHandler : INotificationHandler<OrderCreatedNotification> { ... }
```

### 3. Marker Interface Architecture

**Purpose**: Use marker interfaces to enable compile-time type safety and runtime handler discovery without reflection-heavy configurations.

**The Interface Hierarchy** :

```
IBaseRequest (root marker)
    │
    ├── IRequest<TResponse> (request with response)
    │       │
    │       └── IRequest (void response using Unit)
    │
    └── INotification (notification marker)
```

**IBaseRequest**:
The foundational marker interface that all messages must implement. It serves no behavioral purpose other than type constraint—it enables MediatR to accept any message type while maintaining compile-time checking.

**IRequest&lt;TResponse&gt;**:
Represents a request that expects a response of type `TResponse`. Used for Command and Query patterns where the sender needs the result of the operation.

**IRequest (void equivalent)**:
```csharp
public interface IRequest : IRequest<Unit> { }
```
This convenience interface leverages the `Unit` type (MediatR's equivalent of `void`) to represent requests without return values, eliminating the need to specify `IRequest<Unit>` explicitly .

**INotification**:
Marker for notifications—messages that can be handled by multiple handlers and do not return a value to the sender.

**Unit Type Purpose**:
`Unit` represents a void return value in a type-safe manner. Unlike `void`, `Unit` can be used as a generic parameter, enabling:
- Consistent pipeline behavior handling for void requests
- Proper `Task<T>` returns without special-casing void
- Representation in asynchronous contexts

---

## Request/Response (Unicast) Messaging

### 4. IRequest & IRequestHandler Core Contracts

**Purpose**: Define the contract for one-to-one message processing where each request has exactly one handler .

**IRequest&lt;TResponse&gt; Contract**:
```csharp
/// <summary>
/// Marker interface to represent a request with a response
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IRequest<out TResponse> : IBaseRequest { }
```

**IRequestHandler&lt;TRequest, TResponse&gt; Contract**:
```csharp
/// <summary>
/// Defines a handler for a request
/// </summary>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
/// <typeparam name="TResponse">The type of response from the handler</typeparam>
public interface IRequestHandler<in TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response from the request</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
```

**Key Design Decisions**:

| Decision | Rationale |
|----------|-----------|
| **Async-first** | All handlers return `Task<TResponse>` for async support |
| **CancellationToken support** | Enables graceful shutdown and timeout handling |
| **`in TRequest` contravariance** | Allows handlers to accept base request types |
| **`out TResponse` covariance** | Allows handlers to return derived response types |
| **Generic constraint `TRequest : IRequest<TResponse>`** | Ensures type safety between request and response |

### 5. Convenience Handler Base Classes

**Purpose**: Reduce boilerplate code for common handler patterns by providing pre-built abstract base classes .

**Handler Types Matrix**:

| Base Class | Method Type | Return Type | Use Case |
|------------|-------------|-------------|----------|
| `AsyncRequestHandler<TRequest>` | Async (`Task`) | Void | Async operation without return value |
| `RequestHandler<TRequest, TResponse>` | Sync (`TResponse`) | Value | Synchronous operation with return |
| `RequestHandler<TRequest>` | Sync (`void`) | Void | Synchronous operation without return |

**AsyncRequestHandler&lt;TRequest&gt; Implementation**:
```csharp
/// <summary>
/// Wrapper class for a handler that asynchronously handles a request and does not return a response
/// </summary>
public abstract class AsyncRequestHandler<TRequest> : IRequestHandler<TRequest> 
    where TRequest : IRequest
{
    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(
        TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    /// <summary>
    /// Override in a derived class for the handler logic
    /// </summary>
    protected abstract Task Handle(TRequest request, CancellationToken cancellationToken);
}
```

**RequestHandler&lt;TRequest, TResponse&gt; Implementation**:
```csharp
/// <summary>
/// Wrapper class for a handler that synchronously handles a request and returns a response
/// </summary>
public abstract class RequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> IRequestHandler<TRequest, TResponse>.Handle(
        TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Handle(request));

    /// <summary>
    /// Override in a derived class for the handler logic
    /// </summary>
    protected abstract TResponse Handle(TRequest request);
}
```

**Design Pattern: Wrapper Pattern**:
These abstract classes use the **Wrapper Pattern**—they implement the async `IRequestHandler` interface but delegate to simpler synchronous or void methods, handling the `Task` and `Unit` conversions internally.

### 6. Send Operation Flow

**Purpose**: The core execution flow when `IMediator.Send()` is invoked .

**Execution Steps**:

1. **Client calls** `IMediator.Send(request)`
2. **Mediator checks cache** for handler wrapper type
3. **If not cached**: Mediator creates handler wrapper via reflection
   - Uses `Activator.CreateInstance` with generic type `RequestHandlerWrapperImpl<TRequest, TResponse>`
   - Caches wrapper for future requests of same type
4. **Wrapper resolves handler** from DI container
5. **Wrapper resolves pipeline behaviors** from DI container
6. **Wrapper builds pipeline** by chaining behaviors
7. **Pipeline executes**:
   - Behaviors execute before the handler (outer to inner)
   - Handler executes
   - Behaviors execute after the handler (inner to outer)
8. **Response returns** to client

**Handler Wrapper Creation** :
```csharp
var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers
    .GetOrAdd(request.GetType(), static requestType =>
    {
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>)
            .MakeGenericType(requestType, typeof(TResponse));
        var wrapper = Activator.CreateInstance(wrapperType) 
            ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
        return (RequestHandlerBase)wrapper;
    });
```

---

## Notification (Multicast) Messaging

### 7. INotification & INotificationHandler Contracts

**Purpose**: Define the contract for one-to-many message distribution where a single notification can be processed by multiple handlers .

**INotification Contract**:
```csharp
/// <summary>
/// Marker interface to represent a notification
/// </summary>
public interface INotification { }
```

**INotificationHandler&lt;TNotification&gt; Contract**:
```csharp
/// <summary>
/// Defines a handler for a notification
/// </summary>
/// <typeparam name="TNotification">The type of notification being handled</typeparam>
public interface INotificationHandler<in TNotification> 
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification
    /// </summary>
    /// <param name="notification">The notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
```

**Key Differences from Request Handlers**:

| Aspect | Request Handler | Notification Handler |
|--------|-----------------|---------------------|
| **Return value** | `Task<TResponse>` | `Task` (void) |
| **Number of handlers** | Exactly one | Zero to many |
| **Handler discovery** | Single handler lookup | Enumerate all handlers |
| **Exception handling** | Exception thrown to caller | AggregateException (all handlers invoked) |

### 8. Publish Operation Flow

**Purpose**: The execution flow when `IMediator.Publish()` is called .

**Execution Steps**:

1. **Client calls** `IMediator.Publish(notification)`
2. **Mediator queries DI container** for all `INotificationHandler<TNotification>` implementations
3. **Mediator iterates through handlers**
4. **For each handler**: Invokes `Handle(notification, cancellationToken)`
5. **If handler throws exception**: Exception captured but remaining handlers still execute
6. **After all handlers**: If any exceptions occurred, wraps them in `AggregateException`

**Exception Handling Behavior**:
Unlike requests (where the first exception terminates execution), notification publication continues executing all handlers regardless of individual failures. This ensures that one failing handler doesn't prevent other handlers from executing their logic.

**Example: Multiple Notification Handlers** :
```csharp
// Define notification
public class UserRegisteredNotification : INotification
{
    public string Username { get; set; }
    public string Email { get; set; }
}

// Handler 1: Save to database
public class DatabaseHandler : INotificationHandler<UserRegisteredNotification>
{
    public Task Handle(UserRegisteredNotification notification, CancellationToken ct)
    {
        // Save user to database
        return Task.CompletedTask;
    }
}

// Handler 2: Send welcome email
public class EmailHandler : INotificationHandler<UserRegisteredNotification>
{
    public Task Handle(UserRegisteredNotification notification, CancellationToken ct)
    {
        // Send welcome email
        return Task.CompletedTask;
    }
}

// Handler 3: Log to audit trail
public class AuditHandler : INotificationHandler<UserRegisteredNotification>
{
    public Task Handle(UserRegisteredNotification notification, CancellationToken ct)
    {
        // Write to audit log
        return Task.CompletedTask;
    }
}
```

### 9. Synchronous Notification Handler Wrapper

**Purpose**: Provide convenience base class for synchronous notification handlers .

**NotificationHandler&lt;TNotification&gt; Implementation**:
```csharp
/// <summary>
/// Wrapper class for a synchronous notification handler
/// </summary>
public abstract class NotificationHandler<TNotification> : INotificationHandler<TNotification> 
    where TNotification : INotification
{
    Task INotificationHandler<TNotification>.Handle(
        TNotification notification, CancellationToken cancellationToken)
    {
        Handle(notification);
        return Unit.Task;
    }

    /// <summary>
    /// Override in a derived class for the handler logic
    /// </summary>
    protected abstract void Handle(TNotification notification);
}
```

---

## Pipeline Behaviors (Middleware)

### 10. IPipelineBehavior Interface

**Purpose**: Enable cross-cutting concerns (logging, validation, metrics, caching, retries) to be applied to request processing without modifying handlers .

**IPipelineBehavior Contract**:
```csharp
/// <summary>
/// Pipeline behavior to surround the inner handler.
/// Implementations add additional behavior and await the next delegate.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> 
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler. Perform any additional behavior and await the <paramref name="next"/> delegate as necessary
    /// </summary>
    /// <param name="request">Incoming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="next">Awaitable delegate for the next action in the pipeline. Eventually this delegate represents the handler.</param>
    /// <returns>Awaitable task returning the <typeparamref name="TResponse"/></returns>
    Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next);
}
```

**RequestHandlerDelegate Delegate**:
```csharp
/// <summary>
/// Represents an async continuation for the next task to execute in the pipeline
/// </summary>
/// <typeparam name="TResponse">Response type</typeparam>
/// <returns>Awaitable task returning a <typeparamref name="TResponse"/></returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
```

### 11. Pipeline Behavior Chaining Algorithm

**Purpose**: Compose multiple behaviors into a nested execution chain using functional aggregation .

**The Algorithm** (using `Aggregate` LINQ method):
```csharp
return serviceProvider
    .GetServices<IPipelineBehavior<TRequest, TResponse>>()
    .Reverse()
    .Aggregate(
        (RequestHandlerDelegate<TResponse>)Handler,  // Seed = actual handler
        (next, pipeline) => (t) => pipeline.Handle(request, next, t)
    );
```

**Visualization of Pipeline Chaining**:

```
Request → Behavior 1 → Behavior 2 → Behavior 3 → Handler
                      ↑           ↑           ↑
                      │           │           │
               Await next()  Await next()  Await next()

Response ← Behavior 1 ← Behavior 2 ← Behavior 3 ← Handler
```

**Step-by-Step Explanation**:

1. **Get behaviors in registration order** (from DI container)
2. **Reverse the order** (so first registered executes first)
3. **Start with the actual handler** as the innermost delegate
4. **For each behavior**: Wrap the current delegate with a new delegate that calls the behavior's `Handle` method, passing the current delegate as `next`
5. **Result**: Nested function calls where each behavior wraps the next

**Example Ordering**:
- Behaviors registered: `LoggingBehavior`, `ValidationBehavior`, `CachingBehavior`
- Execution order (pre-handler): `Logging` → `Validation` → `Caching` → `Handler`
- Execution order (post-handler): `Handler` → `Caching` → `Validation` → `Logging`

### 12. Common Pipeline Behavior Implementations

**Logging Behavior**:
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();
        
        _logger.LogInformation("Handled {RequestType} in {ElapsedMs}ms", 
            typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
        
        return response;
    }
}
```

**Validation Behavior**:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Any())
                throw new ValidationException(failures);
        }
        
        return await next();
    }
}
```

**Exception Handling Behavior** :
```csharp
public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            // Transform exception, log, or wrap in ProblemDetails
            throw new CustomException("An error occurred processing your request", ex);
        }
    }
}
```

---

## Handler Resolution & Caching

### 13. Handler Caching Mechanism

**Purpose**: Eliminate reflection overhead by caching handler wrapper types after first use .

**Caching Architecture**:

Mediator maintains three `ConcurrentDictionary` instances for thread-safe, lock-free caching:

| Cache Dictionary | Key Type | Value Type |
|-----------------|----------|------------|
| `_requestHandlers` | Request Type (`Type`) | `RequestHandlerBase` |
| `_notificationHandlers` | Notification Type (`Type`) | `NotificationHandlerBase` |
| `_streamRequestHandlers` | Stream Request Type (`Type`) | `StreamRequestHandlerBase` |

**Why ConcurrentDictionary?** :
- Thread-safe for concurrent request handling
- Lock-free operations using `GetOrAdd`
- Minimal contention under high throughput
- O(1) lookup after warmup

**Cache Population on First Request** :
```csharp
var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers
    .GetOrAdd(request.GetType(), static requestType =>
    {
        // Build generic type: RequestHandlerWrapperImpl<TRequest, TResponse>
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>)
            .MakeGenericType(requestType, typeof(TResponse));
        
        // Create instance via Activator
        var wrapper = Activator.CreateInstance(wrapperType) 
            ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
        
        return (RequestHandlerBase)wrapper;
    });
```

**Performance Impact**:

| Phase | Overhead | Frequency |
|-------|----------|-----------|
| First request per type | Reflection + `Activator.CreateInstance` | Once per request type |
| Subsequent requests | O(1) dictionary lookup | Every request |

### 14. Handler Resolution from DI Container

**Purpose**: Delegate handler instance creation to the configured DI container for proper lifetime management (singleton, scoped, transient) .

**Handler Resolution Flow**:

1. **Wrapper receives the `IServiceProvider`** (typically from DI container)
2. **Wrapper calls `GetService<T>()`** to resolve the actual handler
3. **Container manages handler lifecycle** based on registration

**Example Handler Registration**:
```csharp
// In Startup.cs / Program.cs
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Handler is resolved per request (Scoped) if registered with .AddScoped()
// MediatR's AddMediatR registers handlers as Scoped by default
```

### 15. Request Handler Wrapper Implementation

**Purpose**: Generic wrapper that abstracts the handler resolution and pipeline execution from the mediator core .

**Wrapper Type Hierarchy**:

```
RequestHandlerBase (abstract base, non-generic for caching)
    │
    └── RequestHandlerWrapper<TResponse> (generic for response type)
            │
            └── RequestHandlerWrapperImpl<TRequest, TResponse> (concrete implementation)
```

**Why Three Levels?** :

| Level | Generics | Purpose |
|-------|----------|---------|
| `RequestHandlerBase` | None | Non-generic storage in dictionary |
| `RequestHandlerWrapper<TResponse>` | Response only | Handle Unit specialization |
| `RequestHandlerWrapperImpl<TRequest, TResponse>` | Full | Actual handler resolution |

**Wrapper Implementation** (conceptual):
```csharp
internal class RequestHandlerWrapperImpl<TRequest, TResponse> 
    : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(
        IRequest<TResponse> request, 
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Resolve actual handler
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        
        // Resolve pipeline behaviors
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        
        // Build pipeline and execute
        return BuildPipeline(handler, behaviors)
            .Handle((TRequest)request, cancellationToken);
    }
}
```

---

## Stream Request Processing

### 16. IStreamRequest & IStreamRequestHandler

**Purpose**: Support streaming responses where handlers can yield multiple results over time using `IAsyncEnumerable<T>` .

**Introduced**: MediatR 10.0

**Stream Request Interface**:
```csharp
/// <summary>
/// Marker interface to represent a stream request
/// </summary>
public interface IStreamRequest<out TResponse> : IBaseRequest { }
```

**Stream Handler Interface**:
```csharp
/// <summary>
/// Defines a handler for a stream request
/// </summary>
public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles a stream request
    /// </summary>
    IAsyncEnumerable<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken);
}
```

**Stream Pipeline Behaviors**:

Stream requests have their own behavior interface `IStreamPipelineBehavior<TRequest, TResponse>`. Unlike regular behaviors that wrap the entire request execution, stream behaviors wrap the **entire stream**—not each individual yielded item .

### 17. Stream vs. Regular Request Comparison

| Aspect | Regular Request | Stream Request |
|--------|-----------------|----------------|
| **Return type** | `Task<TResponse>` | `IAsyncEnumerable<TResponse>` |
| **Response timing** | All results at once | Yield as available |
| **Memory usage** | Stores all results | Streams results |
| **Use case** | CRUD operations | Large datasets, real-time feeds |
| **Behavior wrapper** | Wraps execution once | Wraps entire stream |

**Stream Request Example**:
```csharp
// Define stream request
public class SearchProductsQuery : IStreamRequest<ProductDto>
{
    public string SearchTerm { get; set; }
}

// Stream handler yields results as found
public class SearchProductsHandler : IStreamRequestHandler<SearchProductsQuery, ProductDto>
{
    public async IAsyncEnumerable<ProductDto> Handle(
        SearchProductsQuery request, 
        CancellationToken cancellationToken)
    {
        await foreach (var product in _dbContext.Products
            .Where(p => p.Name.Contains(request.SearchTerm))
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return new ProductDto { Id = product.Id, Name = product.Name };
        }
    }
}
```

---

## Integration with Dependency Injection

### 18. Service Registration

**Purpose**: Provide a simple, convention-based API for registering MediatR with any DI container .

**Basic Registration**:
```csharp
// In Program.cs (.NET 6+) or Startup.cs
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
```

**Registration Options**:

| Method | Purpose |
|--------|---------|
| `RegisterServicesFromAssembly(Assembly)` | Scan entire assembly for handlers |
| `RegisterServicesFromAssemblies(params Assembly[])` | Scan multiple assemblies |
| `RegisterServicesFromAssemblyContaining<T>()` | Scan assembly containing type T |
| `RegisterServicesFromType(Type)` | Register from type |
| `RegisterServicesFromTypes(params Type[])` | Register from multiple types |

**What Gets Registered**:
- `IMediator` as `Mediator` (Scoped by default)
- All `IRequestHandler<TRequest, TResponse>` implementations
- All `INotificationHandler<TNotification>` implementations
- All `IStreamRequestHandler<TRequest, TResponse>` implementations
- All `IPipelineBehavior<TRequest, TResponse>` implementations
- All `IRequestPreProcessor<TRequest>` implementations
- All `IRequestPostProcessor<TRequest, TResponse>` implementations

### 19. Lifetime Management

**Default Registration Lifetimes**:

| Component | Default Lifetime | Rationale |
|-----------|-----------------|-----------|
| `IMediator` | Scoped | Handlers typically depend on scoped DbContext |
| `IRequestHandler` | Scoped | Should match mediator lifetime |
| `INotificationHandler` | Scoped | May have database dependencies |
| `IPipelineBehavior` | Scoped | May hold request-specific state |
| `IRequestPreProcessor` | Scoped | Request-specific state |

**Custom Lifetime Registration**:
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.Lifetime = ServiceLifetime.Transient; // Override default
});
```

---

## Pre/Post Processors

### 20. IRequestPreProcessor & IRequestPostProcessor

**Purpose**: Provide simpler alternatives to full pipeline behaviors for scenarios that don't need the next delegate pattern .

**IRequestPreProcessor Interface**:
```csharp
/// <summary>
/// Defines a pre-processor for a request
/// </summary>
public interface IRequestPreProcessor<in TRequest>
{
    Task Process(TRequest request, CancellationToken cancellationToken);
}
```

**IRequestPostProcessor Interface**:
```csharp
/// <summary>
/// Defines a post-processor for a request
/// </summary>
public interface IRequestPostProcessor<in TRequest, in TResponse>
{
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
```

**Built-in Behaviors**:

MediatR includes two built-in behaviors that automatically execute pre/post processors :
- `RequestPreProcessorBehavior<TRequest, TResponse>`
- `RequestPostProcessorBehavior<TRequest, TResponse>`

**Pre/Post Processor Registration**:
```csharp
public class LoggingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
{
    private readonly ILogger _logger;
    
    public async Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing {RequestType}", typeof(TRequest).Name);
    }
}
```

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          APPLICATION LAYER                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │ Controller   │  │ Service      │  │ Background   │                       │
│  │ (API)        │  │ (Domain)     │  │ Service      │                       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                       │
│         │                 │                 │                               │
│         │ Send(request)   │ Send(command)   │ Publish(event)                │
│         └─────────────────┼─────────────────┘                               │
│                           │                                                 │
└───────────────────────────┼─────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           IMediator Interface                               │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Task<TResponse> Send<TResponse>(IRequest<TResponse> request,       │    │
│  │                                 CancellationToken ct = default)    │    │
│  │                                                                      │    │
│  │  Task Publish<TNotification>(TNotification notification,            │    │
│  │                              CancellationToken ct = default)       │    │
│  │                              where TNotification : INotification    │    │
│  │                                                                      │    │
│  │  IAsyncEnumerable<TResponse> CreateStream<TResponse>(               │    │
│  │      IStreamRequest<TResponse> request,                             │    │
│      CancellationToken ct = default)                                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Mediator Implementation                             │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Send() Flow:                                                         │    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ 1. Check cache for handler wrapper                          │    │    │
│  │  │    _requestHandlers.GetOrAdd(requestType)                   │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ 2. Wrapper resolves handler from DI                         │    │    │
│  │  │    GetRequiredService<IRequestHandler<TRequest, TResponse>>│    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ 3. Wrapper resolves pipeline behaviors from DI              │    │    │
│  │  │    GetServices<IPipelineBehavior<TRequest, TResponse>>()    │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ 4. Build pipeline using Aggregate chain                      │    │    │
│  │  │    behaviors.Reverse().Aggregate(...)                        │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Pipeline Behaviors (Middleware)                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                                                                       │    │
│  │  Request ──► LoggingBehavior ──► ValidationBehavior ──► Handler    │    │
│  │                 │                    │                │              │    │
│  │                 │ await next()       │ await next()   │              │    │
│  │                 │                    │                │              │    │
│  │                 ▼                    ▼                ▼              │    │
│  │           (log start)          (validate)       (execute)            │    │
│  │                                                                       │    │
│  │  Response ◄── LoggingBehavior ◄── ValidationBehavior ◄── Handler    │    │
│  │                 │                    │                │              │    │
│  │                 │ after next()       │ after next()   │              │    │
│  │                 ▼                    ▼                ▼              │    │
│  │            (log end)           (no post)         (return)            │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Handler Resolution (DI Container)                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ IRequestHandler<TRequest, TResponse>                         │    │    │
│  │  │     ↓                                                         │    │    │
│  │  │ YourHandler : IRequestHandler<Command, Result>              │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ INotificationHandler<TNotification>                          │    │    │
│  │  │     ↓                                                         │    │    │
│  │  │ Handler1 : INotificationHandler<Event>                       │    │    │
│  │  │ Handler2 : INotificationHandler<Event>                       │    │    │
│  │  │ Handler3 : INotificationHandler<Event>                       │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ IPipelineBehavior<TRequest, TResponse>                       │    │    │
│  │  │     ↓                                                         │    │    │
│  │  │ LoggingBehavior, ValidationBehavior, CachingBehavior        │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Response Processing (Return Path)                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                                                                       │    │
│  │  Handler returns TResponse                                           │    │
│  │         │                                                            │    │
│  │         ▼                                                            │    │
│  │  Pipeline unwinds (behaviors execute after handler)                 │    │
│  │         │                                                            │    │
│  │         ▼                                                            │    │
│  │  Wrapper returns response to mediator                               │    │
│  │         │                                                            │    │
│  │         ▼                                                            │    │
│  │  Mediator returns response to caller                                │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | MediatR Component |
|---|------------------|-----------------|-------------------|
| 1 | Mediator Pattern | Decouple message senders from handlers | `IMediator` |
| 2 | Marker Interface | Type-safe message discrimination | `IBaseRequest`, `INotification` |
| 3 | Unicast Messaging | One-to-one message processing | `IRequest`, `IRequestHandler` |
| 4 | Multicast Messaging | One-to-many message distribution | `INotification`, `INotificationHandler` |
| 5 | Pipeline Behaviors | Cross-cutting concern composition | `IPipelineBehavior` |
| 6 | Functional Aggregation | Behavior chaining using `Aggregate` | Pipeline construction |
| 7 | Wrapper Pattern | Cache generic behavior for type erasure | `RequestHandlerWrapper` |
| 8 | Lazy Initialization | Create handler wrappers on first use | `ConcurrentDictionary` caching |
| 9 | Unit Type | Type-safe representation of void | `Unit` struct |
| 10 | Async First | Non-blocking operation support | `Task<T>` returns |
| 11 | Stream Processing | Yield results progressively | `IStreamRequest`, `IAsyncEnumerable` |
| 12 | Pre/Post Processor | Simple request lifecycle hooks | `IRequestPreProcessor`, `IRequestPostProcessor` |
| 13 | Assembly Scanning | Convention-based handler discovery | `RegisterServicesFromAssembly` |
| 14 | Contravariant Input | Flexible handler parameter types | `in TRequest` |
| 15 | Covariant Output | Flexible handler return types | `out TResponse` |

---

## Configuration Reference

### Basic Service Registration

```csharp
// .NET 6+ Program.cs
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
```

### Advanced Configuration

```csharp
builder.Services.AddMediatR(cfg =>
{
    // Scan multiple assemblies
    cfg.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),
        Assembly.Load("MyApp.Application"),
        Assembly.Load("MyApp.Infrastructure")
    );
    
    // Set lifetime (default is Scoped)
    cfg.Lifetime = ServiceLifetime.Scoped;
    
    // Add open generic behaviors
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

### Request Handler Configuration

```csharp
// Command with response
public class CreateOrderCommand : IRequest<OrderIdResult>
{
    public string CustomerId { get; set; }
}

// Handler implementation
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderIdResult>
{
    public async Task<OrderIdResult> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // Business logic
        return new OrderIdResult { OrderId = Guid.NewGuid() };
    }
}
```

### Notification Handler Configuration

```csharp
// Notification definition
public class OrderCreatedNotification : INotification
{
    public Guid OrderId { get; set; }
}

// Multiple handlers
public class EmailNotificationHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        // Send email
        return Task.CompletedTask;
    }
}

public class InventoryHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken ct)
    {
        // Update inventory
        return Task.CompletedTask;
    }
}
```

### Pipeline Behavior Configuration

```csharp
// Define behavior
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Processing {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Completed {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Register (via assembly scanning or explicitly)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

---

## Performance Characteristics

| Operation | Complexity | First Request | Subsequent | Notes |
|-----------|------------|---------------|------------|-------|
| Send (Request) | O(behaviors) | Reflection + caching | Compiled delegate | Cached after first use |
| Publish (Notification) | O(handlers) | Resolve all handlers | Same as first | No per-type caching |
| Pipeline building | O(behaviors) | Built each request | Built each request | Small overhead per request |
| Handler resolution | O(1) DI | Per request | Per request | Container overhead |

**Memory Usage**:
- `ConcurrentDictionary` entries: One per unique request type
- Handler wrappers: One cached wrapper per request type
- Behavior instances: Scoped per request (released after completion)

---

## Comparison with Alternative Mediator Libraries

| Feature | MediatR | Brighter | MassTransit (In-Memory) | Wolverine |
|---------|---------|----------|------------------------|-----------|
| Primary Use | In-process mediator | Command processor | Message bus (can be in-mem) | In-process + messaging |
| Request/Response | Yes | Yes (via Command) | Limited | Yes |
| Notifications | Yes | No | Yes (via consumers) | Yes |
| Pipeline Behaviors | Yes | Yes (via handlers) | Yes (via middleware) | Yes |
| Stream Responses | Yes (10.0+) | No | Yes | Yes |
| Pre/Post Processors | Yes | No | No | No |
| DI Integration | Any container | Any container | Any container | Any container |
| Learning Curve | Low | Medium | High | Medium |
| External Dependencies | None | None | Optional (transports) | Optional |

---

## Source Code Reference

| Component | Location (GitHub: jbogard/MediatR) |
|-----------|-------------------------------------|
| Core Interfaces | `src/MediatR/Contracts/` |
| Mediator Implementation | `src/MediatR/Mediator.cs` |
| Handler Wrappers | `src/MediatR/Internal/` |
| Pipeline Behaviors | `src/MediatR/Pipeline/` |
| Unit Type | `src/MediatR/Unit.cs` |
| Service Registration | `src/MediatR/ServiceCollectionExtensions.cs` |

---

## Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `MediatR` | Core library |
| `MediatR.Extensions.Microsoft.DependencyInjection` | DI integration (deprecated; now included in core) |
| `MediatR.Contracts` | Contracts assembly (minimal dependencies for sharing) |

---

## Conclusion

MediatR's design philosophy emphasizes:

- **Simplicity**: Unambitious, focused library with minimal moving parts
- **Loose coupling**: Eliminate direct dependencies between components
- **Consistency**: Uniform message handling patterns across applications
- **Extensibility**: Pipeline behaviors for cross-cutting concerns
- **Performance**: Caching and compiled delegates for minimal overhead

Key innovations and algorithms include:

- **Mediator pattern implementation**: Single interface (`IMediator`) for all message routing, eliminating direct service dependencies
- **Two message types (Request/Notification)** : Clear separation between unicast (one handler) and multicast (many handlers) communication patterns
- **Pipeline behavior chaining via functional aggregation**: Uses `Aggregate` and `Reverse` to compose behaviors into nested execution chains 
- **Handler wrapper caching with `ConcurrentDictionary`** : Eliminates reflection overhead after first use per request type 
- **Unit type for void returns**: Type-safe representation enabling generic pipelines without special-casing void
- **Stream processing with `IAsyncEnumerable`** : Efficient streaming of large result sets without blocking 
- **Wrapper pattern for type erasure**: Generic wrappers stored in non-generic caches enabling type-safe handling
- **Pre/Post processors**: Simplified request lifecycle hooks for common scenarios 

This combination of algorithms and patterns makes MediatR suitable for:
- **Domain-Driven Design (DDD)** : Clear separation between commands, queries, and domain events
- **CQRS (Command Query Responsibility Segregation)** : Enforces separation of write and read operations
- **Clean/Onion Architecture** : Application core depends only on abstractions (`IRequest`, `IRequestHandler`)
- **API Controllers** : Thin controllers delegating to MediatR rather than calling services directly
- **Event-driven architectures**: Using `INotification` for domain events within single process boundary
- **Maintainable enterprise applications**: Reducing coupling between components, improving testability

---

*Document Version: 1.0*
*Based on MediatR source code, official documentation, and community resources*
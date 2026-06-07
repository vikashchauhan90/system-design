# AutoMapper: Complete Object-Object Mapping Library Reference

## Document Overview

This document provides a comprehensive analysis of AutoMapper's architectural patterns, configuration systems, mapping algorithms, and runtime execution strategies. AutoMapper is a convention-based object-object mapping library for .NET that eliminates the tedious boilerplate code required to map properties from one object to another . Unlike manual mapping approaches that require explicit property assignments, AutoMapper uses a **configuration-driven, convention-based algorithm** to automatically map properties between types, with support for flattening, projection, and complex object graphs . This document covers the core architecture, configuration lifecycle, convention-based matching algorithms, query projection integration, and extensibility mechanisms that power AutoMapper.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Configuration System & Lifecycle](#configuration-system--lifecycle)
3. [Convention-Based Matching Algorithm](#convention-based-matching-algorithm)
4. [Runtime Execution Engine](#runtime-execution-engine)
5. [Query Projection (LINQ Integration)](#query-projection-linq-integration)
6. [Value Resolution System](#value-resolution-system)
7. [Extensibility & Customization](#extensibility--customization)
8. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Convention Over Configuration Pattern

**Purpose**: Reduce boilerplate mapping code by deriving mapping behavior from naming and type conventions rather than explicit instructions.

**Core Principle**: AutoMapper assumes that properties with the same name and compatible types should be mapped together automatically. This convention-based approach means that for simple scenarios (e.g., `User.FirstName` → `UserDto.FirstName`), no additional configuration is required .

**Convention Examples**:

| Convention | Source Type | Destination Type | AutoMapper Behavior |
|------------|-------------|------------------|---------------------|
| Same property names | `Customer.Address` | `CustomerDto.Address` | Automatically mapped |
| PascalCase to camelCase | `Order.TotalAmount` | `orderDto.totalAmount` | Automatically handled |
| Flattening (nested access) | `User.Address.City` | `UserDto.City` | Maps via pattern matching |
| Type compatibility | `int` to `string` | Compatible via `ToString()` | Automatic if configured |

**When Configuration Is Needed**:
- Property names differ (e.g., `User.Id` → `UserDto.UserIdentifier`)
- Complex flattening with custom logic
- Value transformations (e.g., `DateTime` → custom string format)
- Conditional mapping based on runtime values

### 2. Configuration-Once, Execute-Many Pattern

**Purpose**: Separate mapping configuration from runtime execution, enabling optimized, precompiled mapping functions .

**The Two-Phase Design**:

| Phase | Component | Timing | Mutability |
|-------|-----------|--------|------------|
| **Configuration Phase** | `MapperConfiguration` | Application startup | Build once, then immutable |
| **Execution Phase** | `IMapper` | Throughout application lifecycle | Stateless, thread-safe |

**Why Two-Phase?** :
- Configuration validation occurs upfront (catch errors at startup)
- Execution can be highly optimized (precompiled expressions, no repeated reflection) 
- Thread-safe shared `IMapper` instance can be used across the entire application
- Supports Dependency Injection scenarios with singleton registration

**Configuration Immutability** (Starting with version 9.0):
Once a `MapperConfiguration` is created, it cannot be modified. This design enables:
- Safe concurrent usage across threads
- Predictable behavior (no runtime configuration changes)
- Simplified testing and validation

### 3. Separation of Configuration Profiles

**Purpose**: Organize mapping configurations into logical, reusable groups for maintainability at scale .

**Profile Architecture**:

```
MapperConfiguration
    │
    ├── Profile 1 (e.g., DomainToDtoProfile)
    │   ├── CreateMap<EntityA, DtoA>()
    │   ├── CreateMap<EntityB, DtoB>()
    │   └── ForMember(...)
    │
    ├── Profile 2 (e.g., ApiToViewModelProfile)
    │   ├── CreateMap<ApiRequest, ViewModel>()
    │   └── ...
    │
    └── Profile 3 (e.g., DataTransferProfile)
        └── ...
```

**Profile Benefits**:

| Benefit | Description |
|---------|-------------|
| **Organization** | Group related mappings together |
| **Reusability** | Profiles can be added to multiple configurations |
| **Testability** | Individual profiles can be tested in isolation |
| **Discovery** | Assembly scanning automatically finds and registers profiles |
| **Separation of concerns** | Different layers have separate mapping configurations |

**Profile Definition** (modern approach using constructor):
```csharp
public class OrganizationProfile : Profile
{
    public OrganizationProfile()
    {
        CreateMap<Foo, FooDto>();
        CreateMap<Bar, BarDto>();
    }
}
```

**Assembly Scanning**:
AutoMapper can automatically scan assemblies for classes inheriting from `Profile` and add them to the configuration using `cfg.AddMaps(myAssembly)` .

---

## Configuration System & Lifecycle

### 4. MapperConfiguration & Fluent API

**Purpose**: Central, immutable configuration container that defines all mapping strategies .

**MapperConfiguration Anatomy**:

```csharp
var config = new MapperConfiguration(cfg => 
{
    // Create direct type maps
    cfg.CreateMap<Source, Destination>();
    
    // Add profile instances
    cfg.AddProfile<MyProfile>();
    
    // Configure global settings
    cfg.AllowNullDestinationValues = true;
    cfg.AllowNullCollections = true;
    
    // Validate configuration (optional)
}, loggerFactory);
```

**Configuration Components**:

| Component | Purpose |
|-----------|---------|
| **MapperConfigurationExpression** | Fluent API entry point for configuration |
| **TypeMap** | Complete mapping definition between two types |
| **PropertyMap** | Member-level mapping rules |
| **ConstructorMap** | Constructor parameter mapping rules |
| **ProfileMap** | Groups related configurations together |

**Configuration Validation**:

```csharp
// Development-time validation
#if DEBUG
config.AssertConfigurationIsValid();
#endif
```

This validation ensures all configured mappings are complete (no unmapped properties) and prevents runtime surprises .

### 5. IMapper & Dependency Injection

**Purpose**: Runtime mapping service that executes configured transformations .

**IMapper Registration** (typical DI setup):
```csharp
// In Startup.cs or Program.cs
services.AddAutoMapper(typeof(Startup));  // Scans assembly for profiles
services.AddAutoMapper(cfg => 
{
    cfg.CreateMap<Source, Destination>();
}, typeof(MyProfile).Assembly);
```

**IMapper Characteristics**:

| Characteristic | Description |
|----------------|-------------|
| **Thread-safety** | Safe to use as singleton across concurrent requests |
| **Stateless** | No mutation of configuration after creation |
| **Disposable** | Implements `IDisposable` for cleanup |
| **Extensible** | Can be wrapped or decorated |

### 6. Configuration Lifecycle Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       APPLICATION STARTUP                                    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  1. Create MapperConfiguration                                      │    │
│  │     var config = new MapperConfiguration(cfg => { ... });           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  2. Add Profiles & Create Maps                                      │    │
│  │     cfg.AddProfile<MyProfile>();                                    │    │
│  │     cfg.CreateMap<Source, Dest>();                                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  3. Validate Configuration (optional)                               │    │
│  │     config.AssertConfigurationIsValid();                            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  4. Create IMapper Instance                                         │    │
│  │     IMapper mapper = config.CreateMapper();                         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
└────────────────────────────────────┼─────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       APPLICATION RUNTIME                                    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  5. Execute Mappings                                                │    │
│  │     var dest = mapper.Map<Destination>(source);                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  (IMapper instance is typically registered as singleton in DI container)   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Convention-Based Matching Algorithm

### 7. Default Matching Rules

**Purpose**: Automatically match source and destination properties without explicit configuration .

**Core Matching Algorithm**:

AutoMapper matches properties based on the following precedence:

1. **Exact name match** (case-insensitive by default)
2. **Flattening pattern** (nested property access)
3. **Custom naming conventions** (e.g., `GetMethod()` → `Method`)
4. **Explicit configuration** (overrides conventions)

**Property Matching Examples**:

```csharp
public class Source
{
    public string FirstName { get; set; }
    public Address HomeAddress { get; set; }
    public int Age { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class Destination
{
    public string FirstName { get; set; }      // ✓ Matches by name
    public string City { get; set; }           // ✓ Flattens HomeAddress.City
    public string Age { get; set; }            // ✓ int → string (if configured)
    public string Created { get; set; }        // ✗ No match (different name)
}
```

### 8. Flattening Algorithm

**Purpose**: Automatically map nested properties to flat DTO structures .

**Flattening Pattern Recognition**:

AutoMapper recognizes flattens by looking for properties named `TypeNamePropertyName` or accessing nested objects via dot notation.

**Example**:

```csharp
public class Order
{
    public Customer Customer { get; set; }
    public decimal Total { get; set; }
}

public class Customer
{
    public string Name { get; set; }
    public Address ShippingAddress { get; set; }
}

public class Address
{
    public string City { get; set; }
}

public class OrderDto
{
    public string CustomerName { get; set; }        // Flattened from Customer.Name
    public string CustomerShippingAddressCity { get; set; }  // Deep flattening
    public decimal Total { get; set; }              // Direct match
}
```

**How Flattening Works Internally**:
AutoMapper uses a "dot" notation algorithm to traverse object graphs. When a destination member is not found directly on the source type, AutoMapper attempts to match using patterns like `Source.Property.SubProperty` .

### 9. Type Compatibility Resolution

**Purpose**: Automatically convert between compatible but different types.

**Built-in Type Converters**:

| Source Type | Destination Type | Conversion Behavior |
|-------------|------------------|---------------------|
| `int` | `string` | `ToString()` |
| `string` | `int` | `int.Parse()` (throws on invalid) |
| `DateTime` | `string` | `ToString()` using current culture |
| `DateTime` | `DateTimeOffset` | Implicit conversion |
| `Enum` | `int` | Underlying value |
| `int` | `Enum` | Cast |
| `List<A>` | `IEnumerable<B>` | Element-wise mapping |

**Custom Type Converters**:
Developers can register custom `ITypeConverter<TSource, TDestination>` implementations for complex conversion logic.

---

## Runtime Execution Engine

### 10. Expression Tree Compilation

**Purpose**: Generate optimized mapping functions at configuration time rather than using reflection during execution .

**Architecture**:

```
Configuration Phase                          Execution Phase
┌─────────────────────┐                    ┌─────────────────────┐
│   TypeMap           │                    │   Compiled          │
│   (Mapping          │    Expression      │   Delegate          │
│    Definition)      │    Tree            │   (Func)            │
├─────────────────────┤ ───────────────►   ├─────────────────────┤
│ • Source type       │                    │   Fast property     │
│ • Destination type  │                    │   assignments       │
│ • Member mappings   │                    │   No reflection     │
└─────────────────────┘                    └─────────────────────┘
```

**Key Components**:

| Component | Responsibility |
|-----------|----------------|
| **TypeMapPlanBuilder** | Core engine that builds execution plans |
| **ExpressionBuilder** | Creates expression trees for property access |
| **ResolutionContext** | Manages state during mapping execution |
| **MapperRegistry** | Registry of available mapping strategies |

**Performance Implication**:
Because AutoMapper compiles expression trees rather than using reflection per mapping call, runtime performance approaches hand-coded mapping . The initial configuration overhead (compiling all mappings) is amortized over the lifetime of the application.

### 11. ResolutionContext

**Purpose**: Maintain state during mapping execution, including circular reference detection and context-specific options.

**Context-Managed Features**:
- **Circular reference detection**: Prevents infinite loops when objects reference each other
- **Inline mapping options**: Override configuration per mapping call
- **Items dictionary**: Pass custom data through the mapping pipeline
- **Source/destination type tracking**: For polymorphic mapping

---

## Query Projection (LINQ Integration)

### 12. ProjectTo Pattern

**Purpose**: Translate AutoMapper configurations into LINQ expressions for efficient database querying .

**The Problem**:
Without projection, mapping database entities to DTOs often requires:
1. Loading entire entities into memory
2. Then mapping to DTOs (additional memory and CPU)

**ProjectTo Solution**:

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Customer, CustomerDto>();
});

// Instead of: customers.ToList().Select(c => mapper.Map<CustomerDto>(c))
var dtos = dbContext.Customers
    .ProjectTo<CustomerDto>(config)
    .ToList();
```

**SQL Generation**:
`ProjectTo` translates mapping configurations into `SELECT` clauses that request only the columns needed for the DTO. For example, if `CustomerDto` only contains `Name` and `Email`, the generated SQL will only `SELECT` those columns.

**Projection Pipeline**:

```
Configured TypeMap
        │
        ▼
ProjectionBuilder ──► Expression Tree ──► LINQ Provider (EF Core)
                                              │
                                              ▼
                                          SQL Query
```

### 13. Expression Translation

**Purpose**: Convert AutoMapper's member mapping logic into expressions that database providers can understand.

**Supported Expression Types**:
- Property access (`entity.Property`)
- Simple transformations (`entity.Value.ToString()`)
- Collection projections (`entity.Orders.Select(o => o.Total)`)
- Flattening (`entity.Address.City`)

**Limitations**:
- Complex custom resolvers may not translate to SQL
- Value converters with arbitrary logic cannot be pushed to the database
- Only expressions that represent translatable logic work with `ProjectTo`

---

## Value Resolution System

### 14. Value Resolution Strategies

**Purpose**: Provide multiple pluggable mechanisms for obtaining source values during mapping .

**Resolution Strategy Hierarchy**:

```
┌─────────────────────────────────────────────────────────────────┐
│                     Value Resolution Strategies                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ Property        │  │ MapFrom         │  │ ResolveUsing    │  │
│  │ Access          │  │ (Expression)    │  │ (Custom)        │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
│                                                                  │
│  Direct property      Complex member        Fully custom        │
│  read                 resolution           resolution logic    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Strategy Selection**:
The system automatically selects the appropriate resolution strategy based on configuration and type compatibility .

### 15. MapFrom & Custom Resolvers

**Purpose**: Override default convention-based member mapping with explicit logic.

**MapFrom (Expression-based)** :
```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.FullName, 
        opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
    .ForMember(dest => dest.AgeGroup,
        opt => opt.MapFrom(src => src.Age >= 18 ? "Adult" : "Minor"));
```

**Custom Resolver (IValueResolver)** :
```csharp
public class MyValueResolver : IValueResolver<Source, Destination, string>
{
    public string Resolve(Source source, Destination destination, 
        string destMember, ResolutionContext context)
    {
        // Complex logic here
        return source.Value?.ToString() ?? "Unknown";
    }
}

CreateMap<Source, Dest>()
    .ForMember(dest => dest.CalculatedValue, 
        opt => opt.MapFrom<MyValueResolver>());
```

### 16. Conditional Mapping

**Purpose**: Apply mapping only when certain conditions are met.

```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Value, 
        opt => opt.Condition(src => src.HasValue));
```

---

## Extensibility & Customization

### 17. ITypeConverter

**Purpose**: Define complete custom conversion logic between types, overriding all default mapping behavior.

```csharp
public class CustomConverter : ITypeConverter<Source, Dest>
{
    public Dest Convert(Source source, Dest destination, ResolutionContext context)
    {
        return new Dest
        {
            Value = Transform(source.RawValue),
            Computed = Compute(source)
        };
    }
}

CreateMap<Source, Dest>().ConvertUsing<CustomConverter>();
```

### 18. IMemberValueResolver

**Purpose**: Provide custom resolution for individual members without implementing full type converter.

```csharp
public class CustomMemberResolver : IMemberValueResolver<Source, Dest, string, string>
{
    public string Resolve(Source source, Dest dest, string sourceMember, 
        string destMember, ResolutionContext context)
    {
        // Transform the source value
        return Transform(sourceMember);
    }
}

CreateMap<Source, Dest>()
    .ForMember(dest => dest.Transformed, 
        opt => opt.MapFrom<CustomMemberResolver, string>(src => src.Original));
```

### 19. IValueTransformer

**Purpose**: Apply consistent transformations to all mapped values of a specific type.

```csharp
// Trim all string properties
cfg.ValueTransformers.Add<string>(val => val?.Trim());
```

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      APPLICATION STARTUP                                     │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     CONFIGURATION PHASE                              │    │
│  │                                                                       │    │
│  │   Program.cs / Startup.cs                                            │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  var config = new MapperConfiguration(cfg =>                │    │    │
│  │   │  {                                                           │    │    │
│  │   │      cfg.AddProfile<MyProfile>();                           │    │    │
│  │   │      cfg.CreateMap<Source, Dest>();                         │    │    │
│  │   │  });                                                         │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  MapperConfigurationExpression                              │    │    │
│  │   │  • Creates TypeMap for each (Source,Dest) pair             │    │    │
│  │   │  • Builds PropertyMap for each member                       │    │    │
│  │   │  • Applies convention matching                              │    │    │
│  │   │  • Stores configuration (immutable after this)             │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  TypeMapPlanBuilder                                         │    │    │
│  │   │  • Builds expression tree for each mapping                  │    │    │
│  │   │  • Compiles to delegates                                    │    │    │
│  │   │  • Stores compiled mapping functions                        │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  config.AssertConfigurationIsValid()  // Optional validation │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  IMapper mapper = config.CreateMapper();                    │    │    │
│  │   │  // Registered as singleton in DI container                 │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      APPLICATION RUNTIME                                     │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                       EXECUTION PHASE                                │    │
│  │                                                                       │    │
│  │   Client Code                                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  var dest = mapper.Map<Destination>(source);                │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  IMapper.Map()                                              │    │    │
│  │   │  • Looks up compiled mapping function for (Source,Dest)    │    │    │
│  │   │  • Creates ResolutionContext                               │    │    │
│  │   │  • Invokes compiled delegate                               │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  ResolutionContext                                          │    │    │
│  │   │  • Tracks circular references                               │    │    │
│  │   │  • Manages inline options                                   │    │    │
│  │   │  • Passes context to resolvers                              │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  Value Resolution Strategy Selection                        │    │    │
│  │   │                                                              │    │    │
│  │   │  ┌────────────┐    ┌────────────┐    ┌────────────┐        │    │    │
│  │   │  │ Property   │ or │ MapFrom    │ or │ IValueRes- │        │    │    │
│  │   │  │ Access     │    │ Expression │    │ olver      │        │    │    │
│  │   │  └────────────┘    └────────────┘    └────────────┘        │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  Destination object created and populated                   │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │   ┌─────────────────────────────────────────────────────────────┐    │    │
│  │   │  Result returned to caller                                  │    │    │
│  │   └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      QUERY PROJECTION (LINQ)                                 │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  IQueryable<Source> query = dbContext.Sources;                      │    │
│  │  var dtos = query.ProjectTo<Destination>(config).ToList();         │    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │  ProjectTo Extension                                         │    │    │
│  │  │  • Builds expression tree from TypeMap                      │    │    │
│  │  │  • Returns IQueryable<Destination>                          │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │  LINQ Provider (EF Core, etc.)                              │    │    │
│  │  │  • Translates expression tree to SQL                        │    │    │
│  │  │  • SELECT only columns needed for DTO                       │    │    │
│  │  │  • Executes query at database                               │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                        │    │
│  │                              ▼                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │  SQL: SELECT s.FirstName, a.City, ...                       │    │    │
│  │  │  FROM Sources s                                             │    │    │
│  │  │  LEFT JOIN Addresses a ON s.AddressId = a.Id                │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | AutoMapper Component |
|---|------------------|-----------------|----------------------|
| 1 | Convention-over-Configuration | Reduce boilerplate mapping code | Default property matching |
| 2 | Configuration-Once, Execute-Many | Separate setup from runtime | `MapperConfiguration` + `IMapper` |
| 3 | Flattening Algorithm | Map nested objects to flat DTOs | Pattern-based property resolution |
| 4 | Expression Tree Compilation | Generate optimized mapping code | `TypeMapPlanBuilder`, `ExpressionBuilder` |
| 5 | ProjectTo (LINQ Translation) | Database query projection | `ProjectionBuilder` |
| 6 | Type Conversion Resolution | Handle compatible but different types | Built-in/registered `ITypeConverter` |
| 7 | Value Resolution Strategy | Pluggable value sourcing | `MapFrom`, `IValueResolver` |
| 8 | Conditional Mapping | Apply mapping based on runtime conditions | `Condition` method |
| 9 | Circular Reference Detection | Prevent infinite recursion | `ResolutionContext` |
| 10 | Profile Organization | Group related mappings | `Profile` class |
| 11 | Assembly Scanning | Automatic profile discovery | `AddMaps` method |
| 12 | Configuration Validation | Catch mapping errors at startup | `AssertConfigurationIsValid` |
| 13 | Null Substitution | Replace nulls with default values | `NullSubstitute` |
| 14 | Value Transformation | Apply consistent transformations | `ValueTransformers` |
| 15 | Member Visibility | Map private/internal members | `ShouldMapProperty` |
| 16 | Reverse Mapping | Bidirectional mapping configuration | `ReverseMap` |
| 17 | Max Depth | Limit recursion depth | `MaxDepth` |

---

## Configuration Reference

### Basic Configuration

```csharp
// Using static API (pre-9.0) - No longer available in 9.0+
// Mapper.Initialize(cfg => cfg.CreateMap<Source, Dest>()); // OBSOLETE

// Modern approach (9.0+)
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Source, Destination>();
    cfg.CreateMap<AnotherSource, AnotherDest>();
});

var mapper = config.CreateMapper();
```

### Profile Configuration

```csharp
public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.OrderTotal, 
                opt => opt.MapFrom(src => src.Items.Sum(i => i.Price)))
            .ForMember(dest => dest.CustomerName,
                opt => opt.MapFrom(src => src.Customer.FullName))
            .ReverseMap();  // Creates bidirectional mapping
    }
}
```

### Condition Mapping Example

```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Value, 
        opt => 
        {
            opt.Condition(src => src.HasValue);
            opt.MapFrom(src => src.ComputedValue);
        });
```

### Null Substitution

```csharp
CreateMap<Source, Dest>()
    .ForMember(dest => dest.Name, 
        opt => opt.NullSubstitute("Unknown"));
```

### Max Depth Configuration

```csharp
CreateMap<Source, Dest>()
    .MaxDepth(2);  // Limits recursion depth to prevent loops
```

### Dependency Injection Registration

```csharp
// In Program.cs (.NET 6+)
builder.Services.AddAutoMapper(typeof(Program));

// Or with configuration action
builder.Services.AddAutoMapper(cfg =>
{
    cfg.CreateMap<Source, Dest>();
    cfg.AddProfile<MyProfile>();
}, typeof(Program).Assembly);
```

---

## Performance Characteristics

| Operation | Complexity | Relative Cost | Notes |
|-----------|------------|---------------|-------|
| Configuration creation | O(number of mappings × number of properties) | High (one-time) | Expression tree compilation |
| First mapping execution | O(1) lookup + delegate invocation | Low (after warmup) | Compilation already done |
| Subsequent mapping executions | O(1) delegate invocation | Very low | Near hand-coded performance  |
| ProjectTo (LINQ) | Expression translation + provider translation | Varies | Depends on query provider |
| Validation (`AssertConfigurationIsValid`) | O(number of mappings) | Moderate | Development/testing only |

---

## Comparison with Alternative Approaches

| Approach | Performance | Type Safety | Configuration Effort | Debugging | Best For |
|----------|-------------|-------------|---------------------|-----------|----------|
| **Manual mapping (extension methods)** | Best | Compile-time | High per DTO | Excellent | Simple DTOs (<10 properties)  |
| **AutoMapper** | Very good (compiled delegates) | Runtime (validated at startup) | Low after setup | Moderate | Large numbers of mappings, complex flattening  |
| **Mapperly (Source Generator)** | Best | Compile-time | Low | Good | Performance-critical, many mappings  |
| **Explicit mapping libraries** | Good | Runtime | Moderate | Variable | Specialized scenarios |

**Decision Factors**:
- **Choose manual mapping when**: Small project, few DTOs (<10), need maximum control and debugging clarity
- **Choose AutoMapper when**: Many DTOs (50+), complex flattening needs, established convention-based mapping
- **Choose Mapperly when**: Performance-critical paths with many mappings, want compile-time safety

---

## Source Code Reference

| Component | Location (GitHub: AutoMapper/AutoMapper) |
|-----------|-------------------------------------------|
| Core Configuration | `src/AutoMapper/Configuration/` |
| Type Maps | `src/AutoMapper/TypeMap.cs` |
| Execution Engine | `src/AutoMapper/Execution/` |
| Projection | `src/AutoMapper/QueryableExtensions.cs` |
| Profiles | `src/AutoMapper/Profile.cs` |
| Value Resolvers | `src/AutoMapper/ValueResolver.cs` |

---

## Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `AutoMapper` | Core library |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | DI integration for ASP.NET Core |
| `AutoMapper.Collection` | Collection mapping with identity handling |
| `AutoMapper.EF6` | Entity Framework 6 integration |
| `AutoMapper.Extensions.ExpressionMapping` | Expression mapping support |

---

## Conclusion

AutoMapper's design philosophy emphasizes:

- **Convention over configuration**: Reduce boilerplate through intelligent defaults
- **Configuration immutability**: Once configured, mapping definitions cannot change
- **Compiled execution**: Expression trees converted to delegates for runtime performance
- **Separation of concerns**: Profiles organize mappings by logical boundary
- **Query projection**: Push mapping logic to database query layer

Key innovations and algorithms include:

- **Convention-based matching**: Automatic property mapping with flattening support, eliminating manual assignments for common cases 
- **Two-phase architecture**: Separation of configuration (one-time) and execution (many times) enabling optimized compiled delegates 
- **Expression tree compilation**: Dynamic generation of mapping functions that approach hand-coded performance 
- **ProjectTo for LINQ**: Translate mapping configurations directly to database queries, avoiding unnecessary data retrieval 
- **Pluggable value resolution**: Multiple strategies (MapFrom, IValueResolver, ITypeConverter) for flexible value sourcing
- **Profile-based organization**: Assembly-scanning for automatic profile discovery 
- **Configuration validation**: Catch unmapped properties and mismatches at application startup rather than runtime 

This combination of algorithms and patterns makes AutoMapper suitable for:

- **Layered architectures**: Mapping between domain models, DTOs, view models, and API contracts
- **Entity Framework applications**: Efficient query projection with `ProjectTo`
- **Large-scale applications**: Managing hundreds of mapping definitions through profiles
- **API development**: Mapping request/response objects to internal models
- **Maintaining separation of concerns**: Preventing domain model exposure to presentation layers

---

*Document Version: 1.0*
*Based on AutoMapper documentation, source code analysis, and community resources* 
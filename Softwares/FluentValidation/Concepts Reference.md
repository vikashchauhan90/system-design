# FluentValidation: Complete .NET Validation Library Reference

## Document Overview

This document provides a comprehensive analysis of FluentValidation's architectural patterns, rule configuration systems, execution engines, and extensibility mechanisms. FluentValidation is a popular .NET library that provides a strongly-typed fluent interface for building validation rules. Unlike Data Annotations (which rely on metadata attributes attached to the model), FluentValidation allows validation logic to be encapsulated in dedicated validator classes, making rules easier to test, reuse, and maintain . This document covers the core architecture, rule definition patterns, execution strategies, cascade control mechanisms, dependency integration, and extensibility points that power this widely-adopted validation library.

---

## Core Architectural Patterns

### 1. Specification Pattern Implementation

**Purpose**: Encapsulate validation rules as reusable, composable specifications that can be evaluated against domain objects .

**Core Principle**: FluentValidation implements the Specification pattern—a design pattern where business rules are encapsulated in dedicated objects that can be composed, tested, and reused independently of the entities they validate .

**How It Differs from Data Annotations**:

| Aspect | Data Annotations | FluentValidation |
|--------|------------------|------------------|
| Rule location | Attributes on model class | Separate validator classes  |
| Reusability | Rules tied to specific properties | Validators can be composed and reused  |
| Testability | Must instantiate model class | Validators testable in isolation  |
| Complexity handling | Basic rules only | Complex cross-property validation  |
| Async support | None (synchronous only) | Full async database checking  |

**The Specification Pattern in FluentValidation**:
```csharp
// Each validator is a specification that determines if an object is valid
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Email).EmailAddress();
    }
}

// Usage: Evaluate specification against entity
var validator = new CustomerValidator();
ValidationResult result = validator.Validate(customer);
bool isValid = result.IsValid;
```

### 2. Separation of Concerns

**Purpose**: Isolate validation logic from business entities and application services, adhering to the Single Responsibility Principle .

**The Problem with Embedded Validation**:
When validation logic is embedded in entities or services, it becomes difficult to:
- Test validation rules independently
- Reuse validation rules across different contexts
- Change validation rules without modifying core domain classes

**FluentValidation's Solution**:

| Concern | Responsible Component | Benefits |
|---------|----------------------|----------|
| Data structure | Entity/DTO class | Focus on data, not rules |
| Validation rules | Validator class | Rules are testable, reusable  |
| Rule execution | Validation pipeline | Centralized processing  |
| Error handling | ValidationResult | Uniform error reporting |

### 3. Configuration-Once, Validate-Many Pattern

**Purpose**: Configure validation rules once in a validator class; reuse the same validator across multiple contexts and execution environments.

**The Two-Phase Design**:

| Phase | Timing | Mutability |
|-------|--------|------------|
| Configuration | Validator construction (or DI resolution) | Configure rules once |
| Validation | Each call to `Validate()` | Stateless, thread-safe execution |

**Lifecycle Flow**:
```csharp
// Phase 1: Configuration (typically once at startup or via DI)
var validator = new CustomerValidator();  // Rules are built here

// Phase 2: Validation (executed many times)
var result1 = validator.Validate(customer1);  // Reuses configured rules
var result2 = validator.Validate(customer2);  // Stateless execution
```

**Thread Safety**: Validators are inherently thread-safe because:
- Rule configuration is immutable after construction
- Validation execution does not modify validator state
- The same validator instance can be used by multiple threads concurrently

---

## Validator Configuration System

### 4. AbstractValidator Base Class

**Purpose**: Provide the foundation for all validator implementations, offering rule configuration APIs.

**AbstractValidator&lt;T&gt; Structure**:

```csharp
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        // Rule configuration happens in constructor
        RuleFor(c => c.FirstName).NotEmpty();
        RuleFor(c => c.LastName).NotEmpty();
        RuleFor(c => c.Email).EmailAddress();
    }
}
```

**Key Members of AbstractValidator**:

| Member | Purpose |
|--------|---------|
| `RuleFor<TProperty>()` | Defines a rule for a specific property |
| `RuleForEach<TProperty>()` | Defines rule for each item in a collection |
| `CascadeMode` (deprecated) | Legacy cascade control  |
| `RuleLevelCascadeMode` | Controls rule execution within a validator  |
| `ClassLevelCascadeMode` | Controls inter-rule execution  |
| `Include()` | Includes rules from another validator  |

### 5. Rule Definition Architecture

**Purpose**: Provide a fluent API for defining validation rules with a grammar that reads like natural language.

**IRuleBuilder&lt;T, TProperty&gt; Interface**:

The fluent interface is built on a chain of interfaces where each method returns the appropriate next interface type:

```csharp
// The fluent chain progression
RuleFor<T>          // Returns IRuleBuilderInitial
    .<Validator>()  // Returns IRuleBuilderOptions
    .WithMessage()  // Returns IRuleBuilderOptions (fluent)
    .WithSeverity() // Returns IRuleBuilderOptions (fluent)
    .When()         // Returns IRuleBuilderOptions
```

**Rule Building Flow**:

```
RuleFor(x => x.Name)          // Select property
    .NotNull()                 // Add validator
    .WithMessage("Required")   // Configure message
    .WithSeverity(Severity.Error) // Configure severity
    .When(x => x.Type == 1)    // Add condition
    .WithState(x => new { ... }) // Add custom state
```

### 6. Built-in Validators

**Purpose**: Provide a comprehensive set of pre-built validators for common validation scenarios.

**Validator Categories**:

| Category | Validators | Use Case |
|----------|------------|----------|
| **Null/Empty** | `NotNull`, `NotEmpty`, `Null`, `Empty` | Presence checking |
| **Comparison** | `Equal`, `NotEqual`, `GreaterThan`, `LessThan`, `InclusiveBetween`, `ExclusiveBetween` | Numeric/date comparisons |
| **String** | `Length`, `MaximumLength`, `MinimumLength`, `Matches` (regex), `EmailAddress` | Text validation |
| **Collections** | `NotEmpty`, `ForEach` (inline rules), `NotNull` | Collection validation |
| **Format** | `CreditCard`, `EnumName`, `EnumValue`, `RegularExpression` | Format validation |
| **Identity** | `IdentityCard`, `IdentityNumber`, `Imei`, `Isonic` | Special identifier formats |
| **URL/URI** | Not built-in (maintainer cites maintenance burden)  | Use `Must()` for custom logic |

**Extensibility Note**: The library maintainer explicitly states that adding new built-in validators is limited due to maintenance overhead (translation support, long-term support commitment, testing, and documentation). Instead, FluentValidation prioritizes extensibility, allowing custom validators via `Must()`, `Custom()`, or `IPropertyValidator` .

---

## Cascade Control Mechanisms

### 7. Cascade Modes Overview

**Purpose**: Control execution flow when validations fail, determining whether subsequent validators should execute.

**Three-Level Cascade Control** :

| Level | Property | Scope | When to Use |
|-------|----------|-------|-------------|
| **Rule-Level** | `RuleLevelCascadeMode` | Validators within a single rule | Prevent dependent rule execution |
| **Class-Level** | `ClassLevelCascadeMode` | Multiple rules within a validator | Control inter-rule execution |
| **Global** | `ValidatorOptions.Global.DefaultRuleLevelCascadeMode` | All validators in application | Set application-wide defaults |

### 8. Rule-Level Cascade Mode

**Purpose**: Control whether subsequent validators execute after a failure within the same rule chain.

**The Two Modes** :

| Mode | Behavior | Use Case |
|------|----------|----------|
| `Continue` (default) | All validators execute regardless of failures | Collect all validation errors |
| `Stop` | Execution stops at first failure | Prevent subsequent validators that depend on success |

**Example: Why Stop Matters**:
```csharp
RuleFor(x => x.Surname)
    .NotNull()      // If this fails...
    .NotEqual("foo"); // ...this would throw NullReferenceException

// Solution: Use Stop cascade mode
RuleFor(x => x.Surname)
    .Cascade(CascadeMode.Stop)
    .NotNull()      // Stops here if null
    .NotEqual("foo"); // Not executed when null
```

**Rule-Level Cascade Visualization**:

```
Continue Mode (default):
NotNull (fails) → NotEqual (executes anyway) → NotEmpty (executes)

Stop Mode:
NotNull (fails) → STOP → NotEqual (skipped) → NotEmpty (skipped)
```

### 9. Class-Level Cascade Mode

**Purpose**: Control whether execution continues to subsequent rules after a rule chain failure.

**Class-Level Cascade Behavior** :

```csharp
public class PersonValidator : AbstractValidator<Person>
{
    public PersonValidator()
    {
        // Setting class-level cascade mode
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Continue;  // Keep rule-level separate
        
        RuleFor(x => x.Forename).NotNull();
        RuleFor(x => x.MiddleNames).NotNull();  // Not executed if Forename fails
        RuleFor(x => x.Surname).NotNull();      // Not executed if Forename fails
    }
}
```

**Cascade Mode Combinations** :

| Class Mode | Rule Mode | Behavior |
|------------|-----------|----------|
| Continue | Continue | All rules execute; all validators within each rule execute |
| Continue | Stop | All rules execute; each rule stops at first failure |
| Stop | Continue | Stop at first rule failure; all validators within that rule execute |
| Stop | Stop | Stop at first rule failure within first rule; no further rules execute |

### 10. Global Cascade Configuration

**Purpose**: Set default cascade behavior across the entire application.

```csharp
// Configure during application startup
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
```

**Important Change (Version 11+)** : The `CascadeMode` property on `AbstractValidator` is deprecated and removed in version 12. Use `RuleLevelCascadeMode` and `ClassLevelCascadeMode` instead for finer control .

---

## Execution Engine

### 11. IValidator Interface

**Purpose**: Provide the contract for all validators, enabling abstraction and dependency injection.

**Interface Definition**:
```csharp
public interface IValidator
{
    ValidationResult Validate(object instance);
    Task<ValidationResult> ValidateAsync(object instance, CancellationToken cancellation = default);
}

public interface IValidator<in T> : IValidator where T : class
{
    ValidationResult Validate(T instance);
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellation = default);
}
```

**Key Characteristics**:

| Characteristic | Description |
|----------------|-------------|
| Generic and non-generic | Non-generic version enables runtime discovery |
| Async-first | Full async support for database/API validation  |
| Interface-based | Enables DI and testing with mocks |
| Thread-safe | Implementations must be thread-safe |

### 12. ValidationResult Structure

**Purpose**: Encapsulate validation results, including success/failure status and detailed error information.

**ValidationResult Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | `bool` | True if no validation errors |
| `Errors` | `IList<ValidationFailure>` | Collection of validation errors |
| `RuleSetsExecuted` | `string[]` | Rulesets that were executed |

**ValidationFailure Properties**:

| Property | Description |
|----------|-------------|
| `PropertyName` | Name of the property that failed |
| `ErrorMessage` | Human-readable error message |
| `AttemptedValue` | The value that caused the failure |
| `CustomState` | User-defined state object |
| `Severity` | Error severity level (Error, Warning, Info) |
| `ErrorCode` | Custom error code for categorization |
| `FormattedMessageArguments` | Arguments used to format message |
| `ResourceName` | Name of localization resource |

### 13. TestValidate Extension

**Purpose**: Enable fluent, readable unit tests for validators .

**TestValidate API**:
```csharp
// Instead of: var result = validator.Validate(customer);
var result = validator.TestValidate(customer);

// Fluent assertions
result.ShouldHaveValidationErrorFor(x => x.Email)
      .WithErrorMessage("Email is required")
      .WithSeverity(Severity.Error);

result.ShouldNotHaveValidationErrorFor(x => x.Name);
```

**TestValidate Benefits**:

| Feature | Benefit |
|---------|---------|
| Expression-based selectors | Strongly-typed property selection |
| Multiple assertion methods | `ShouldHaveValidationErrorFor`, `ShouldNotHaveValidationErrorFor` |
| Chained assertions | Verify multiple aspects of a failure |
| Integration with test frameworks | Works with xUnit, NUnit, MSTest |

---

## Conditional Validation

### 14. When/Unless Conditions

**Purpose**: Conditionally apply validation rules based on runtime conditions.

**Basic When Condition** :
```csharp
RuleFor(x => x.SomeId)
    .NotEmpty()
    .When(x => x.Type == ProjectType.MovieFocused);
```

**When Scope Behavior** :

By default, a `When` condition applies to all preceding validators in the same chain. This behavior can be modified:

```csharp
// Default: Condition applies to both NotEmpty AND NotNull
RuleFor(x => x.SomeId)
    .NotEmpty()
    .NotNull()
    .When(x => x.Type == MovieFocused);

// Alternative: Apply to CurrentValidator only
RuleFor(x => x.SomeId)
    .NotEmpty()
    .When(x => x.Type == MovieFocused, ApplyConditionTo.CurrentValidator)
    .Null()
    .When(x => x.Type != MovieFocused);
```

**Unless** (negated When):
```csharp
RuleFor(x => x.SomeId).NotEmpty().Unless(x => x.IsDeleted);
```

### 15. Dependent Rules

**Purpose**: Define rules that depend on other property validations succeeding first.

```csharp
RuleFor(x => x.Surname).NotNull();
RuleFor(x => x.Surname).NotEqual("foo")
    .DependentRules(() => {
        RuleFor(x => x.FirstName).NotNull();  // Only if Surname passed
    });
```

---

## Reusability & Composition

### 16. Validator Reuse via Include

**Purpose**: Compose validators by including rules from other validators targeting the same or compatible types .

**Basic Composition**:
```csharp
public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.PostalCode).Matches(@"\d{5}");
    }
}

public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        
        // Include all AddressValidator rules
        RuleFor(x => x.Address).SetValidator(new AddressValidator());
    }
}
```

**Include for Same Type**:
```csharp
// When multiple validators target the same type
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        Include(new CustomerBasicValidator());
        Include(new CustomerExtendedValidator());
    }
}
```

### 17. Inheritance Validation

**Purpose**: Validate polymorphic hierarchies where derived types have additional or overridden rules.

```csharp
public abstract class AnimalValidator : AbstractValidator<Animal>
{
    protected AnimalValidator()
    {
        RuleFor(x => x.Species).NotEmpty();
    }
}

public class DogValidator : AnimalValidator
{
    public DogValidator()
    {
        RuleFor(x => x.Breed).NotEmpty();
    }
}
```

**Note**: Polymorphic validation requires the validator type to match the runtime type. The library's maintainer prefers inheritance-based composition, though composition via `Include` is also supported .

---

## Extensibility Architecture

### 18. Custom Validators via Must

**Purpose**: Implement custom validation logic inline using a predicate.

```csharp
RuleFor(x => x.SomeProperty)
    .Must((obj, prop, context) => {
        // Custom complex logic here
        return IsValid(prop, obj);
    })
    .WithMessage("Custom validation failed");
```

### 19. Custom Validators via IPropertyValidator

**Purpose**: Create reusable, configurable validators with full FluentValidation integration.

**Custom Validator Implementation**:
```csharp
public class EvenNumberValidator : IPropertyValidator
{
    public string Name => "EvenNumberValidator";
    
    public ValidationResult Validate(PropertyValidatorContext context)
    {
        var value = (int)context.PropertyValue;
        if (value % 2 != 0)
        {
            context.MessageFormatter.AppendArgument("Value", value);
            return ValidationResult.Fail(context.MessageFormatter.BuildMessage("Value {Value} is not even"));
        }
        return ValidationResult.Success();
    }
}

// Usage with extension method
public static class MyValidatorExtensions
{
    public static IRuleBuilderOptions<T, int> IsEven<T>(
        this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder.SetValidator(new EvenNumberValidator());
    }
}
```

### 20. Custom Validators via Predicate Validator

**Purpose**: Alternative approach for simpler custom logic.

```csharp
RuleFor(x => x.Value)
    .Custom((value, context) => {
        if (value < 0)
            context.AddFailure("Value must be positive");
    });
```

---

## Integration Patterns

### 21. ASP.NET Core Integration

**Purpose**: Automatically validate incoming request models in web applications.

**Integration Approaches** :

| Approach | Description | Recommendation |
|----------|-------------|----------------|
| **Manual Validation** | Inject validator, call `Validate()` in controller | **Recommended**: Clear, testable, debuggable |
| **Auto-validation (Pipeline)** | Plugs into MVC validation pipeline | Not recommended: synchronous only, MVC-only, harder to debug  |
| **Auto-validation (Filter)** | Action filter using 3rd-party package | Asynchronous, works with minimal APIs |

**Why Manual Validation is Preferred** :
- Asynchronous rules work correctly (auto-validation pipeline is synchronous)
- Works with Minimal APIs, Blazor, and MVC
- Easier to debug and test
- No "magic" behind the scenes

**Manual Validation Example**:
```csharp
public class UsersController : ControllerBase
{
    private readonly IValidator<UserRequest> _validator;
    
    public UsersController(IValidator<UserRequest> validator)
    {
        _validator = validator;
    }
    
    public async Task<IActionResult> Create(UserRequest request)
    {
        var result = await _validator.ValidateAsync(request);
        if (!result.IsValid)
            return BadRequest(result.Errors);
        
        // Continue processing...
    }
}
```

### 22. MediatR Pipeline Integration

**Purpose**: Automatically validate commands/queries before they reach handlers, implementing cross-cutting validation concern .

**ValidationBehavior Implementation**:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();
            
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();
        
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }
        
        if (failures.Any())
            throw new ValidationException(failures);
            
        return await next();
    }
}
```

**Result Pattern Integration** :
```csharp
public static class FluentValidationExtensions
{
    public static Result ToResult(this ValidationResult result)
    {
        if (result.IsValid)
            return Result.Success();
            
        var errors = result.Errors.Select(e => new Error(e.PropertyName, e.ErrorMessage));
        return Result.Failure(errors);
    }
}
```

### 23. Dependency Injection Registration

**Purpose**: Register validators with DI containers for automatic discovery and injection .

**Automatic Assembly Scanning** :
```csharp
// In Program.cs or Startup.cs
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// Or scan multiple assemblies
services.AddValidatorsFromAssemblies(new[] { 
    Assembly.GetExecutingAssembly(),
    typeof(OtherValidator).Assembly 
});
```

**What Assembly Scanning Does** :
The `AssemblyScanner` class identifies all types that implement `IValidator<T>` (the generic interface) and registers them automatically:
- Abstract types and generic type definitions are excluded 
- Both interface and concrete type information are captured
- Validators can be registered with different lifetimes (Scoped, Transient, Singleton)

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          APPLICATION CODE                                    │
│                                                                              │
│  var customer = new Customer { Name = "John", Email = "invalid" };         │
│  var validator = new CustomerValidator();                                   │
│  var result = validator.Validate(customer);                                 │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      VALIDATOR (AbstractValidator<T>)                       │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Rule Configuration (built in constructor)                           │    │
│  │                                                                       │    │
│  │  RuleFor(x => x.Name)                                                │    │
│  │      .NotEmpty()                                                     │    │
│  │      .WithMessage("Name is required")                                │    │
│  │      .When(x => x.Type == CustomerType.VIP);                         │    │
│  │                                                                       │    │
│  │  RuleFor(x => x.Email)                                               │    │
│  │      .EmailAddress()                                                 │    │
│  │      .When(x => !string.IsNullOrEmpty(x.Email));                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Configuration is stored as collection of PropertyRule objects             │
│                                                                              │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      │ Validate() call
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         VALIDATION EXECUTION                                 │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  For each PropertyRule:                                              │    │
│  │                                                                       │    │
│  │  1. Evaluate conditions (When/Unless)                               │    │
│  │         │                                                            │    │
│  │         ▼ (if condition passes)                                      │    │
│  │  2. For each validator in the chain:                                │    │
│  │         │                                                            │    │
│  │         ├─ Validate property value                                   │    │
│  │         ├─ If RuleLevelCascadeMode = Stop and fails: break chain   │    │
│  │         └─ Continue if CascadeMode = Continue                        │    │
│  │         │                                                            │    │
│  │         ▼                                                            │    │
│  │  3. Collect ValidationFailure(s)                                     │    │
│  │         │                                                            │    │
│  │  4. If ClassLevelCascadeMode = Stop and failures exist: stop       │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       VALIDATIONRESULT CREATION                              │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  ValidationResult                                                     │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ IsValid = false                                               │    │    │
│  │  │ Errors = [                                                    │    │    │
│  │  │     ValidationFailure { PropertyName = "Name",                │    │    │
│  │  │                        ErrorMessage = "Name is required" },  │    │    │
│  │  │     ValidationFailure { PropertyName = "Email",               │    │    │
│  │  │                        ErrorMessage = "Invalid email format"}│    │    │
│  │  │ ]                                                             │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                        DEPENDENCY INJECTION SETUP                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  // Assembly scanning                              │
│  services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());       │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     AssemblyScanner                      │    │
│  │                                                                       │    │
│  │  Finds all types implementing IValidator<T>                          │    │
│  │  Excludes abstract types and generic type definitions               │    │
│  │  Registers each with DI container                                    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  // Validator resolved where needed                                         │
│  public class MyService {                                                   │
│      public MyService(IValidator<Customer> validator) { ... }              │
│  }                                                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | FluentValidation Component |
|---|------------------|-----------------|---------------------------|
| 1 | Specification Pattern | Encapsulate business rules as objects | `AbstractValidator<T>`  |
| 2 | Fluent Interface | Natural language rule definition | `IRuleBuilder<T, TProperty>` |
| 3 | Strategy Pattern | Pluggable validation logic | `IPropertyValidator` |
| 4 | Builder Pattern | Chain of rule configuration methods | `RuleBuilder<T, TProperty>` |
| 5 | Cascade Control (Rule-Level) | Short-circuit on rule failure | `CascadeMode.Stop`  |
| 6 | Cascade Control (Class-Level) | Short-circuit on any rule failure | `ClassLevelCascadeMode`  |
| 7 | Composite Pattern | Compose multiple validators | `Include()`, `SetValidator()`  |
| 8 | Predicate Evaluation | Custom inline validation | `Must()`, `Custom()` |
| 9 | Lazy Evaluation | Deferred condition evaluation | `When()`, `Unless()` |
| 10 | Assembly Scanning | Automatic validator discovery | `AssemblyScanner`  |
| 11 | Mediator Pipeline | Cross-cutting validation | `ValidationBehavior<TRequest, TResponse>`  |
| 12 | Result Pattern Integration | Return validation outcomes as values | `ToResult()` extension  |

---

## Configuration Reference

### Basic Validator Definition

```csharp
public class PersonValidator : AbstractValidator<Person>
{
    public PersonValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(0, 120);
        RuleFor(x => x.BirthDate).LessThan(DateTime.Today);
    }
}
```

### Conditional Rules

```csharp
RuleFor(x => x.SomeId)
    .NotEmpty()
    .When(x => x.Type == ProjectType.MovieFocused);  // Condition applies to NotEmpty 

RuleFor(x => x.SomeId)
    .NotEmpty()
    .When(x => x.Type == MovieFocused, ApplyConditionTo.CurrentValidator)  // Current validator only 
    .Null()
    .When(x => x.Type != MovieFocused);
```

### Cascade Configuration

```csharp
// Per-validator (version 11+)
public class MyValidator : AbstractValidator<MyClass>
{
    public MyValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;   // Stop within rules 
        ClassLevelCascadeMode = CascadeMode.Stop;  // Stop after any rule failure 
    }
}

// Global (set once at startup)
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Stop;
```

### Dependency Injection Setup

```csharp
// .NET Core / ASP.NET Core 
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Manual registration (if assembly scanning not desired)
builder.Services.AddScoped<IValidator<Customer>, CustomerValidator>();
```

### MediatR Pipeline Behavior

```csharp
// Register pipeline behavior 
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## Performance & Complexity Reference

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Validator construction | O(number of rules) | Happens once per validator instance |
| Rule evaluation | O(validators per property × rules) | Each validator executes independently |
| Cascade Stop mode | O(rules up to first failure) | May reduce execution time |
| Cascade Continue mode | O(all rules) | All validators always execute |
| `Must` predicate | O(1) + custom logic | Delegated to user code |
| `SetValidator` (composition) | O(validators in composed chain) | Recursively validates child objects |
| Assembly scanning | O(types in assembly) | Happens once at startup  |

---

## Comparison with Validation Approaches

| Feature | FluentValidation | Data Annotations | Manual Validation |
|---------|------------------|------------------|-------------------|
| Rule location | Separate class | Model class attributes | Business logic code |
| Complex cross-property | Yes  | Limited | Yes |
| Async validation | Yes  | No | Yes |
| Reusability | High  | Low (attribute reuse limited) | Manual composition |
| Testability | Excellent (isolated validator)  | Requires model instantiation | Moderate |
| ASP.NET Core integration | Manual (recommended) or auto  | Automatic | Manual |
| Learning curve | Moderate | Low | Depends on code organization |
| Custom validators | Multiple extensibility points  | Custom attributes | N/A |

---

## Source Code Reference

| Component | Location (GitHub: FluentValidation/FluentValidation) |
|-----------|------------------------------------------------------|
| Core Validator | `src/FluentValidation/AbstractValidator.cs` |
| Rule Building | `src/FluentValidation/RuleBuilder.cs` |
| Built-in Validators | `src/FluentValidation/Validators/` |
| Assembly Scanner | `src/FluentValidation/AssemblyScanner.cs`  |
| Cascade Control | `src/FluentValidation/CascadeMode.cs`  |
| ASP.NET Integration | `src/FluentValidation.AspNetCore/`  |

---

## Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `FluentValidation` | Core validation library |
| `FluentValidation.AspNetCore` | ASP.NET Core integration (legacy auto-validation)  |
| `FluentValidation.DependencyInjectionExtensions` | DI integration and assembly scanning  |
| `SharpGrip.FluentValidation.AutoValidation` | 3rd-party async filter-based auto-validation  |

---

## Conclusion

FluentValidation's design philosophy emphasizes:

- **Separation of concerns**: Validation rules live in dedicated classes, not embedded in entities 
- **Specification pattern**: Rules are reusable, composable specifications evaluated against objects 
- **Fluent grammar**: Rules read like natural language, improving readability and maintainability
- **Extensibility first**: Priority on providing building blocks for custom logic rather than exhaustive built-in validators 
- **Configuration immutability**: Rules configured once; validation execution is stateless and thread-safe
- **Testability by design**: Validators can be unit tested in isolation without infrastructure dependencies 

Key innovations and algorithms include:

- **Specification Pattern Implementation**: Encapsulates validation logic as reusable objects with composition support (`Include`, `SetValidator`) 
- **Two-level Cascade Control**: `RuleLevelCascadeMode` for intra-rule control; `ClassLevelCascadeMode` for inter-rule control 
- **When/Unless with Scope Control**: Conditional rule application with configurable behavior (`ApplyConditionTo.CurrentValidator`) 
- **Assembly Scanner**: Automatic discovery and registration of validators via reflection 
- **TestValidate Extension**: Fluent testing API eliminating repetitive assertion code 
- **Multiple Extensibility Points**: `Must()`, `Custom()`, `IPropertyValidator`, `IValueFormatter`
- **Async Support**: Full async validation pipeline for database and API checking 

This combination of algorithms and patterns makes FluentValidation suitable for:
- **Clean/Onion Architecture**: Validators as part of application layer, separate from domain
- **CQRS and MediatR pipelines**: Cross-cutting validation via pipeline behaviors 
- **Complex domain validation**: Cross-property and database-dependent rules 
- **API development**: Request model validation with clear separation of concerns
- **DDD (Domain-Driven Design)** : Encapsulating business rules as specifications 
- **Legacy system modernization**: Incremental migration from Data Annotations

---

*Document Version: 1.0*
*Based on FluentValidation source code, official documentation, and community resources*
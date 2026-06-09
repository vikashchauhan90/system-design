# Rust Runbook

## Introduction

Rust is a systems programming language focused on safety, performance, and concurrency. It delivers memory safety without a garbage collector using ownership, borrowing, and lifetimes.

This runbook covers:
- Rust basics
- Ownership and borrowing
- Types, traits, and generics
- Error handling
- Concurrency and `Send`/`Sync`
- Advanced runtime concepts like `dyn`, `static`, and trait objects

---

## 1. Rust Basics

### 1.1 Cargo and project structure

Use Cargo to create and manage Rust projects.

```bash
cargo new hello_rust
cd hello_rust
cargo run
```

Typical structure:

- `Cargo.toml` — package metadata and dependencies
- `src/main.rs` — binary entry point
- `src/lib.rs` — library root

### 1.2 Variables and mutability

Variables are immutable by default.

```rust
let x = 5;
let mut y = 10;
y += 2;
```

### 1.3 Data types

Primitive types:
- `i32`, `i64`, `u32`, `u64`
- `f32`, `f64`
- `bool`
- `char`
- tuples `(i32, bool)`
- arrays `[i32; 3]`

Example:

```rust
let id: u32 = 42;
let name: &str = "rust";
let pair: (i32, bool) = (1, true);
```

### 1.4 Control flow

```rust
if x > 5 {
    println!("big");
} else {
    println!("small");
}

let result = if x > 5 { "yes" } else { "no" };

for i in 0..3 {
    println!("{}", i);
}

let mut n = 0;
while n < 3 {
    println!("{}", n);
    n += 1;
}
```

### 1.5 Functions

```rust
fn add(a: i32, b: i32) -> i32 {
    a + b
}

let sum = add(2, 3);
```

Expression-based return avoids `return` in simple cases.

---

## 2. Ownership and Borrowing

Ownership is Rust's core memory safety model.

### 2.1 Ownership rules

1. Each value has one owner.
2. When the owner goes out of scope, the value is dropped.
3. Values can be moved but not implicitly copied for non-`Copy` types.

Example:

```rust
let s1 = String::from("hello");
let s2 = s1; // move
// s1 is no longer valid
```

### 2.2 Borrowing with references

References let you use a value without taking ownership.

```rust
let s = String::from("hello");
let len = calculate_length(&s);

fn calculate_length(s: &String) -> usize {
    s.len()
}
```

### 2.3 Mutable borrowing

Only one mutable reference may exist at a time.

```rust
let mut s = String::from("hi");
change(&mut s);

fn change(s: &mut String) {
    s.push_str(", world");
}
```

### 2.4 Lifetimes

Lifetimes describe how long references are valid.

```rust
fn longest<'a>(x: &'a str, y: &'a str) -> &'a str {
    if x.len() > y.len() { x } else { y }
}
```

They prevent dangling references by ensuring returned references do not outlive inputs.

---

## 3. Structs, Enums, and Pattern Matching

### 3.1 Structs

```rust
struct Point {
    x: f64,
    y: f64,
}

let p = Point { x: 1.0, y: 2.0 };
```

### 3.2 Enums

```rust
enum Message {
    Quit,
    Move { x: i32, y: i32 },
    Write(String),
    ChangeColor(i32, i32, i32),
}
```

### 3.3 Pattern matching

```rust
match message {
    Message::Quit => println!("quit"),
    Message::Move { x, y } => println!("move {} {}", x, y),
    Message::Write(text) => println!("{}", text),
    _ => println!("something else"),
}
```

---

## 4. Traits and Generics

### 4.1 Traits

Traits define behavior.

```rust
trait Summary {
    fn summarize(&self) -> String;
}

struct Article {
    headline: String,
}

impl Summary for Article {
    fn summarize(&self) -> String {
        format!("{}", self.headline)
    }
}
```

### 4.2 Generics

```rust
fn largest<T: PartialOrd>(list: &[T]) -> &T {
    let mut largest = &list[0];
    for item in list.iter() {
        if item > largest {
            largest = item;
        }
    }
    largest
}
```

### 4.3 Generic structs

```rust
struct Pair<T> {
    x: T,
    y: T,
}
```

---

## 5. Error Handling

### 5.1 `panic!`

Abort the thread on unrecoverable errors.

```rust
panic!("Something went wrong");
```

### 5.2 `Result`

Return recoverable errors.

```rust
use std::fs::File;
use std::io::{self, Read};

fn read_file(path: &str) -> io::Result<String> {
    let mut file = File::open(path)?;
    let mut contents = String::new();
    file.read_to_string(&mut contents)?;
    Ok(contents)
}
```

Use `?` to propagate errors.

---

## 6. Ownership Across Types

### 6.1 `Copy` and `Clone`

Simple types like integers are `Copy`.

```rust
let x = 5;
let y = x; // copy
```

Heap-allocated types like `String` are not `Copy`, but can be `Clone`.

```rust
let s1 = String::from("hi");
let s2 = s1.clone();
```

### 6.2 `Sized` and `?Sized`

Most types are `Sized`. Dynamically sized types cannot be stored directly on the stack without indirection.

```rust
let s: &str = "hello"; // &str is unsized but reference is sized
```

---

## 7. Advanced Concepts

### 7.1 `dyn` and trait objects

`dyn Trait` is a trait object used when you want values with different concrete types to share the same behavior.

Example:

```rust
trait Draw {
    fn draw(&self);
}

struct Circle;
struct Square;

impl Draw for Circle {
    fn draw(&self) { println!("circle"); }
}

impl Draw for Square {
    fn draw(&self) { println!("square"); }
}

fn render(shape: &dyn Draw) {
    shape.draw();
}
```

#### Why `dyn`?

- `dyn` allows **dynamic dispatch** at runtime.
- It avoids needing a concrete type at compile time.
- It enables storing heterogeneous objects in a single container.

Example of avoiding duplicate copies:

```rust
let shapes: Vec<Box<dyn Draw>> = vec![Box::new(Circle), Box::new(Square)];
```

Without `dyn`, you would need separate concrete containers or an enum for every shape type.

### 7.2 Static dispatch vs dynamic dispatch

Rust normally uses **static dispatch** for generics:

```rust
fn draw_all<T: Draw>(items: &[T]) {
    for item in items {
        item.draw();
    }
}
```

This is compiled separately for each concrete `T` and is fast, but you cannot mix types.

Dynamic dispatch via `dyn Draw` resolves method calls at runtime, which is flexible and avoids code duplication when multiple types share a trait.

### 7.3 `static`

`'static` is the longest possible lifetime.

- A `&'static str` lives for the entire program.
- `static` variables have static storage duration.

```rust
static APP_NAME: &str = "RustRunbook";
```

`'static` also means a type contains no non-`'static` references.

### 7.4 `Send` and `Sync`

`Send` and `Sync` are marker traits used by the compiler for thread safety.

- `Send` means a value can be moved to another thread.
- `Sync` means a value can be shared across threads safely.

Most primitive types and thread-safe data are both `Send` and `Sync`.

Examples:

- `Arc<T>` is `Send` and `Sync` when `T: Send + Sync`.
- `Mutex<T>` is `Send` and `Sync` when `T: Send`.

#### Why they matter

Rust enforces these automatically in concurrent code.

```rust
use std::thread;
use std::sync::Arc;

let data = Arc::new(vec![1, 2, 3]);
let handle = thread::spawn({
    let data = Arc::clone(&data);
    move || {
        println!("{:?}", data);
    }
});
handle.join().unwrap();
```

If a type is not `Send`, it cannot be moved into `thread::spawn`.

If a type is not `Sync`, it cannot be referenced from multiple threads concurrently.

### 7.5 `Rc`, `Arc`, and sharing

- `Rc<T>` is a reference-counted pointer for single-threaded contexts.
- `Arc<T>` is an atomic reference-counted pointer for multi-threaded contexts.

Example:

```rust
use std::sync::Arc;
let shared = Arc::new(String::from("data"));
```

### 7.6 Interior mutability

Rust normally forbids mutable aliasing, but `RefCell<T>` and `Mutex<T>` provide controlled interior mutability.

```rust
use std::cell::RefCell;
let x = RefCell::new(10);
* x.borrow_mut() += 5;
```

Use this when you need mutation through a shared reference.

### 7.7 `unsafe`

`unsafe` blocks allow operations the compiler cannot verify.

Examples:

- dereferencing raw pointers
- calling external functions via FFI
- accessing mutable static variables

Use `unsafe` sparingly and wrap it in safe abstractions.

---

## 8. Concurrency

### 8.1 Threads

```rust
use std::thread;

let handle = thread::spawn(|| {
    println!("hello from thread");
});
handle.join().unwrap();
```

### 8.2 Channels

```rust
use std::sync::mpsc;

let (tx, rx) = mpsc::channel();
thread::spawn(move || {
    tx.send(42).unwrap();
});
println!("received {}", rx.recv().unwrap());
```

### 8.3 `async` / `await`

Rust supports async functions and futures.

```rust
async fn hello() {
    println!("hello async");
}

// requires runtime like tokio or async-std
```

---

## 9. Common patterns

### 9.1 `Result` and `Option`

- `Option<T>` for optional values.
- `Result<T, E>` for fallible operations.

Example:

```rust
let maybe_num: Option<i32> = Some(5);
let no_num: Option<i32> = None;
```

### 9.2 `impl Trait`

Simplifies function signatures.

```rust
fn make_drawable() -> impl Draw {
    Circle
}
```

### 9.3 `where` clauses

```rust
fn compare<T, U>(a: T, b: U)
where
    T: PartialOrd,
    U: PartialOrd,
{
}
```

---

## 10. Practical advice

- Prefer immutable data unless mutability is needed.
- Use `cargo fmt` for formatting and `cargo clippy` for linting.
- Favor explicit lifetimes only when needed.
- Use `dyn Trait` when you need heterogeneous collections or runtime-polymorphic behavior.
- Use generics and trait bounds for compile-time abstraction.
- Use `Arc`, `Mutex`, or `RwLock` only when sharing across threads.

---

## Quick reference: `dyn`, `static`, `Send`, `Sync`

- `dyn Trait`:
  - enables trait objects
  - used for runtime polymorphism
  - is unsized, typically behind `&`, `Box`, or `Arc`

- `static`:
  - a global variable with program lifetime
  - `'static` is the longest lifetime

- `Send`:
  - type can be transferred across threads
  - automatically derived for safe types

- `Sync`:
  - type can be referenced from multiple threads
  - `&T` is `Sync` if `T` is `Sync`

---

## Example: `dyn` vs generics

Generic static dispatch:

```rust
fn draw<T: Draw>(item: T) {
    item.draw();
}
```

Trait object dynamic dispatch:

```rust
fn draw_box(item: Box<dyn Draw>) {
    item.draw();
}
```

Use `dyn` when the concrete type is not known at compile time or when you need to store mixed implementations in one container.

---

## Closing

This runbook is designed to be a learning path from Rust basics to advanced concepts. Use it as a reference while building projects and exploring the language.

# Python Runbook

## Introduction

Python is a high-level, interpreted language known for readability and expressiveness. It supports multiple programming paradigms including procedural, object-oriented, and functional programming.

This runbook covers:
- Python basics
- data types and control flow
- functions and modules
- object-oriented programming
- advanced features like decorators, async, and metaprogramming
- magic methods / dunder methods and their uses

---

## 1. Python Basics

### 1.1 Installation and execution

Run Python from the command line:

```bash
python --version
python script.py
```

Use `python -m venv env` to create a virtual environment.

### 1.2 Basic syntax

```python
name = "Alice"
print(f"Hello, {name}")
```

Python uses indentation to define blocks.

### 1.3 Variables and types

Python is dynamically typed.

```python
x = 10
pi = 3.14
name = 'Python'
is_ready = True
```

### 1.4 Common built-in types

- `int`
- `float`
- `str`
- `bool`
- `list`
- `tuple`
- `dict`
- `set`
- `None`

---

## 2. Control Flow

### 2.1 Conditionals

```python
if x > 0:
    print('positive')
elif x == 0:
    print('zero')
else:
    print('negative')
```

### 2.2 Loops

```python
for i in range(5):
    print(i)

while condition:
    do_work()
```

### 2.3 Comprehensions

```python
squares = [x * x for x in range(5)]
unique = {x for x in items if x > 0}
mapping = {x: x*x for x in range(5)}
```

### 2.4 Truthiness

Falsy values include: `0`, `0.0`, `""`, `[]`, `()`, `{}`, `None`, `False`.

---

## 3. Functions

### 3.1 Defining functions

```python
def add(a, b):
    return a + b

result = add(2, 3)
```

### 3.2 Default and keyword args

```python
def greet(name='Guest', *, excited=False):
    greeting = f"Hello, {name}"
    if excited:
        greeting += '!'
    return greeting
```

### 3.3 Variable arguments

```python
def log(message, *args, **kwargs):
    print(message, args, kwargs)
```

### 3.4 First-class functions

Functions can be assigned, passed, and returned.

```python
def apply(func, value):
    return func(value)

apply(lambda x: x + 1, 4)
```

### 3.5 Lambda expressions

`lambda` creates a small anonymous function in a single expression.

```python
square = lambda x: x * x
print(square(5))  # 25

pairs = [(1, 2), (3, 4), (5, 6)]
sorted_pairs = sorted(pairs, key=lambda pair: pair[1])
print(sorted_pairs)
```

Use lambda expressions for short throwaway functions, especially with `map`, `filter`, and `sorted`.

---

## 4. Modules and Packages

### 4.1 Modules

Put code in `.py` files and import them:

```python
# utils.py

def add(a, b):
    return a + b

# main.py
from utils import add
print(add(1, 2))
```

### 4.2 Packages

A package is a directory with `__init__.py`.

```
myapp/
  __init__.py
  helpers.py
```

### 4.3 Standard library

Key modules:
- `os`, `sys`, `pathlib`
- `json`, `csv`, `configparser`
- `datetime`, `time`
- `collections`, `itertools`
- `re`, `math`, `functools`

---

## 5. Object-Oriented Programming

### 5.1 Classes

```python
class Person:
    def __init__(self, name, age):
        self.name = name
        self.age = age

    def greet(self):
        return f"Hello, {self.name}"
```

### 5.2 Inheritance

```python
class Employee(Person):
    def __init__(self, name, age, role):
        super().__init__(name, age)
        self.role = role
```

### 5.3 Encapsulation

Use single underscore for protected conventions and double underscore for name mangling.

```python
class Example:
    def __init__(self):
        self._hidden = 42
        self.__private = 99
```

### 5.4 Properties

```python
class Rectangle:
    def __init__(self, width, height):
        self._width = width
        self._height = height

    @property
    def area(self):
        return self._width * self._height
```

---

## 6. Error Handling

### 6.1 Exceptions

```python
try:
    value = int('abc')
except ValueError as err:
    print('bad input', err)
else:
    print('parsed', value)
finally:
    print('cleanup')
```

### 6.2 Custom exceptions

```python
class ValidationError(Exception):
    pass
```

---

## 7. Iterators and Generators

### 7.1 Iterators

An iterator implements `__iter__()` and `__next__()`.

```python
numbers = iter([1, 2, 3])
print(next(numbers))
```

### 7.2 Generators

Generators are easy iterator factories.

```python
def counter(n):
    for i in range(n):
        yield i
```

### 7.3 Generator expressions

```python
squares = (x*x for x in range(5))
```

---

## 8. Functional Programming

### 8.1 `map`, `filter`, `reduce`

```python
squares = list(map(lambda x: x*x, range(5)))
positives = list(filter(lambda x: x > 0, data))
```

### 8.2 List, set, dict comprehensions

```python
names = [user.name for user in users if user.active]
```

### 8.3 `functools`

Useful utilities:
- `lru_cache`
- `partial`
- `wraps`

---

## 9. Decorators

### 9.1 Function decorators

```python
def debug(func):
    def wrapper(*args, **kwargs):
        print('calling', func.__name__)
        return func(*args, **kwargs)
    return wrapper

@debug
def add(a, b):
    return a + b
```

### 9.2 Class decorators

```python
def singleton(cls):
    instances = {}
    def wrapper(*args, **kwargs):
        if cls not in instances:
            instances[cls] = cls(*args, **kwargs)
        return instances[cls]
    return wrapper
```

### 9.3 `functools.wraps`

Preserves metadata in decorators.

---

## 10. Context Managers

### 10.1 `with` statement

```python
with open('file.txt') as f:
    data = f.read()
```

### 10.2 Custom context manager

```python
class Timer:
    def __enter__(self):
        self.start = time.time()
        return self

    def __exit__(self, exc_type, exc, tb):
        self.elapsed = time.time() - self.start
```

---

## 11. Async Programming

### 11.1 `async` / `await`

```python
import asyncio

async def hello():
    await asyncio.sleep(1)
    return 'world'

async def main():
    print(await hello())

asyncio.run(main())
```

### 11.2 Tasks and concurrency

```python
async def fetch(url):
    ...

async def main():
    tasks = [asyncio.create_task(fetch(url)) for url in urls]
    await asyncio.gather(*tasks)
```

### 11.3 `asyncio` primitives

- `asyncio.Queue`
- `asyncio.Lock`
- `asyncio.Event`

---

## 12. Typing and Annotations

### 12.1 Type hints

```python
def add(a: int, b: int) -> int:
    return a + b
```

### 12.2 Common typing types

- `Optional[T]`
- `Union[A, B]`
- `List[T]`, `Dict[K, V]`
- `Tuple[T, ...]`
- `Callable[[T], R]`

### 12.3 `typing.Protocol`

```python
from typing import Protocol

class Greeter(Protocol):
    def greet(self) -> str:
        ...
```

---

## 13. Magic Methods / Dunder Methods

Magic methods are special methods that enable Python's data model and operator behavior. They are called implicitly by built-in syntax.

### 13.1 Construction and representation

- `__init__(self, ...)` — object initializer
- `__new__(cls, ...)` — object creation
- `__repr__(self)` — developer-friendly representation
- `__str__(self)` — user-friendly string

Example:

```python
class Point:
    def __init__(self, x, y):
        self.x = x
        self.y = y

    def __repr__(self):
        return f"Point({self.x}, {self.y})"

    def __str__(self):
        return f"({self.x}, {self.y})"
```

### 13.2 Comparison and ordering

- `__eq__(self, other)` — equality `==`
- `__ne__` — inequality `!=`
- `__lt__` — less than `<`
- `__le__` — less than or equal `<=`
- `__gt__` — greater than `>`
- `__ge__` — greater than or equal `>=`

Example:

```python
class Version:
    def __init__(self, major, minor):
        self.major = major
        self.minor = minor

    def __eq__(self, other):
        return (self.major, self.minor) == (other.major, other.minor)
```

### 13.3 Arithmetic and container behavior

- `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__floordiv__`
- `__len__` — called by `len(obj)`
- `__getitem__`, `__setitem__`, `__delitem__`
- `__contains__` — called by `in`
- `__iter__` — called by `iter(obj)`
- `__next__` — iterator next value

Example:

```python
class Fibonacci:
    def __init__(self, n):
        self.n = n

    def __iter__(self):
        self.i, self.j = 0, 1
        return self

    def __next__(self):
        if self.i >= self.n:
            raise StopIteration
        value = self.i
        self.i, self.j = self.j, self.i + self.j
        return value
```

### 13.4 Attribute access

- `__getattr__` — called when an attribute is missing
- `__getattribute__` — called for all attribute access
- `__setattr__` — called when setting attributes
- `__delattr__` — called when deleting attributes

Example:

```python
class Proxy:
    def __init__(self, target):
        self._target = target

    def __getattr__(self, name):
        return getattr(self._target, name)
```

### 13.5 Callable objects

- `__call__(self, *args, **kwargs)` makes an instance callable.

```python
class Multiplier:
    def __init__(self, factor):
        self.factor = factor

    def __call__(self, value):
        return self.factor * value
```

### 13.6 Context managers

- `__enter__(self)` — enter context
- `__exit__(self, exc_type, exc, tb)` — exit context

### 13.7 Object lifecycle

- `__del__(self)` — finalizer / destructor

Use `__del__` sparingly; it is not guaranteed to run immediately.

### 13.8 Attribute customization

- `__slots__` — restrict allowed attributes and reduce memory usage
- `__dict__` — default attribute dictionary

### 13.9 Pickle and serialization

- `__getstate__` / `__setstate__`
- `__reduce__` / `__reduce_ex__`

### 13.10 Advanced data model hooks

- `__hash__` — object hash code for sets and dict keys
- `__bool__` — truthiness of objects
- `__format__` — `format(obj, spec)` support
- `__sizeof__` — memory size for `sys.getsizeof`

---

## 14. Concurrency and Parallelism

### 14.1 Threads

```python
import threading

def worker():
    print('working')

thread = threading.Thread(target=worker)
thread.start()
thread.join()
```

### 14.2 `multiprocessing`

For CPU-bound parallelism, use separate processes.

```python
from multiprocessing import Pool

with Pool(4) as pool:
    results = pool.map(func, data)
```

### 14.3 `asyncio`

Use async for I/O-bound concurrent workflows.

---

## 15. Performance and Best Practices

### 15.1 Use built-in functions

Built-ins like `sum`, `min`, `max`, and `any` are optimized.

### 15.2 Prefer list comprehensions

List comprehensions are often faster than loops.

### 15.3 Avoid global variables

Globals hurt readability and testability.

### 15.4 Use `__slots__` for many small objects

`__slots__` can reduce memory overhead when creating many instances.

### 15.5 Use generators for streaming data

Generators are memory-efficient for large sequences.

---

## 16. Practical Tips

- Follow PEP 8 style guidelines.
- Use type hints and `mypy` for optional static checking.
- Use virtual environments for dependency isolation.
- Write tests with `unittest`, `pytest`, or `doctest`.
- Use `python -m venv`, `pip install -r requirements.txt`, and `python -m pytest`.

---

## Closing

This runbook is designed as a complete reference from Python basics through advanced data model and concurrency topics. Use it as a study guide, developer reference, or onboarding document.

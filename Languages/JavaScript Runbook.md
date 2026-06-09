# JavaScript Runbook

## Introduction

JavaScript is a dynamic language used for web browsers, server-side apps with Node.js, and many runtime environments. It blends functional and object-oriented styles, and understanding its evaluation model is essential for writing robust code.

This runbook covers:
- core syntax and execution model
- LHS/RHS value handling
- types, coercion, and equality
- functions, closures, and prototypes
- modules, symbols, and modern ES features
- error handling and async patterns

---

## 1. Core JavaScript Concepts

### 1.1 Running JavaScript

- Browser: `<script>` tags or bundlers.
- Node.js: `node script.js`.

### 1.2 Values and types

JavaScript has primitive values and objects.

Primitives:
- `undefined`
- `null`
- `boolean`
- `number`
- `bigint`
- `string`
- `symbol`

Objects include arrays, functions, dates, maps, sets, and custom objects.

### 1.3 Variable declarations

```js
let x = 5;
const name = 'Alice';
var old = 'legacy';
```

- `const` creates constants.
- `let` creates block-scoped variables.
- `var` is function-scoped and hoisted.

### 1.4 Scope

JavaScript variables and functions exist in scopes, which determine where a name is accessible.

- **Global scope**: variables declared outside any function or module are global.
- **Function scope**: `var` declarations are visible throughout the enclosing function.
- **Block scope**: `let` and `const` are scoped to the nearest block `{ ... }`.
- **Module scope**: top-level bindings in ES modules are local to the module.

Example:

```js
function example() {
  var a = 1;      // function-scoped
  let b = 2;      // block-scoped
  const c = 3;    // block-scoped

  if (true) {
    let d = 4;
    console.log(b); // visible: b is declared in the outer function block
    console.log(a); // visible: a is function-scoped
  }

  console.log(d);   // ReferenceError: d is not defined
}
```

Another key difference is hoisting behavior:

```js
function example() {
  console.log(x); // undefined, because var is hoisted
  var x = 1;

  console.log(y); // ReferenceError: Cannot access 'y' before initialization
  let y = 2;
}
```

Understanding scope and declaration timing helps avoid both reference errors and invalid assignments.

#### Assignment and LHS/RHS roles within scope

Assignment is a scope-sensitive operation.
A name on the left-hand side (LHS) of `=` must resolve to a writable location in the current scope. The right-hand side (RHS) is evaluated as a value.

Example:

```js
let x = 1;       // x is declared in the current scope
x = 2;           // LHS assignment to x
const y = x + 3; // RHS reads x's value
```

A common error occurs when the LHS is not a valid assignable reference:

```js
const foo = 1;
foo + 2 = 3; // TypeError: Invalid left-hand side in assignment
```

This is both a scope and assignment issue: the expression `foo + 2` is not an assignable reference in any scope. Valid LHS targets include:

```js
let a;
a = 10;
obj.prop = 'value';
[a, b] = [1, 2];
```

Use scope rules and LHS/RHS understanding together to write correct assignments and avoid reference errors.

Valid LHS examples:

```js
let x;
x = 10;            // identifier as LHS
obj.name = 'Bob';  // property access as LHS
[a, b] = [1, 2];   // destructuring target as LHS
```

Understanding LHS/RHS is critical for destructuring, assignment, and scope.

### 1.5 Hoisting

Hoisting is the compile-time behavior where JavaScript moves declarations to the top of their containing scope.

- `var` declarations are hoisted and initialized to `undefined`.
- `let` and `const` declarations are also hoisted, but they remain uninitialized until evaluation, causing the temporal dead zone.

Example with `var`:

```js
function example() {
  console.log(x); // undefined
  var x = 1;
}
```

Example with `let` / `const`:

```js
function example() {
  console.log(y); // ReferenceError: Cannot access 'y' before initialization
  let y = 2;
}
```

Hoisting explains why `var` can be referenced before assignment while `let` and `const` cannot.

---

## 2. Expressions and Statements

### 2.1 Expression examples

```js
const sum = a + b;
const isTrue = x > 0;
const obj = { name: 'Bob' };
```

Expressions produce values.

### 2.2 Statement examples

```js
if (x > 0) {
  console.log('positive');
}

for (let i = 0; i < 3; i++) {
  console.log(i);
}
```

Statements control flow.

---

## 3. Types and Coercion

### 3.1 Type conversion

JavaScript has implicit coercion.

```js
'5' + 2; // '52'
'5' - 2; // 3
true + 1; // 2
```

### 3.2 Equality

- `==` compares after coercion.
- `===` compares without coercion.

```js
0 == false; // true
0 === false; // false
null == undefined; // true
null === undefined; // false
```

### 3.3 `typeof`

```js
typeof 5; // 'number'
typeof null; // 'object' (historic quirk)
```

---

## 4. Functions and Closures

### 4.1 Function declarations

```js
function add(a, b) {
  return a + b;
}
```

### 4.2 Function expressions

```js
const multiply = function (a, b) {
  return a * b;
};
```

### 4.3 Arrow functions

```js
const square = x => x * x;
```

Arrow functions inherit `this` from the surrounding scope.

### 4.4 Closures

A closure captures variables from its outer scope.

```js
function counter() {
  let count = 0;
  return function () {
    count += 1;
    return count;
  };
}

const inc = counter();
console.log(inc()); // 1
console.log(inc()); // 2
```

A classic closure gotcha appears with `for` loops and `setTimeout`.

```js
for (var i = 1; i <= 10; i++) {
  setTimeout(function () {
    console.log('var loop:', i);
  }, i * 100);
}
```

Because `var` is function-scoped, the closure captures the same `i` variable, so all callbacks print `11` after the loop completes.

Using `let` fixes this because each iteration gets its own block-scoped binding:

```js
for (let i = 1; i <= 10; i++) {
  setTimeout(function () {
    console.log('let loop:', i);
  }, i * 100);
}
```

With `let`, each callback closes over the value of `i` for that iteration, printing `1` through `10`.

### 4.5 Function methods: bind, call, apply, and browser callbacks

JavaScript functions are objects and provide methods for controlling `this` and arguments.

```js
const button = {
  label: 'Submit',
  handleClick() {
    console.log(`Clicked: ${this.label}`);
  }
};

const otherButton = { label: 'Cancel' };

const boundHandler = button.handleClick.bind(otherButton);
boundHandler(); // Clicked: Cancel
```

`bind` returns a new function with `this` permanently set to the provided object.

```js
function greet(greeting) {
  console.log(`${greeting}, ${this.name}`);
}

const user = { name: 'Alice' };
greet.call(user, 'Hi');           // Hi, Alice
greet.apply(user, ['Hello']);    // Hello, Alice
```

- `call(thisArg, ...args)` invokes the function immediately with explicit `this` and arguments.
- `apply(thisArg, argsArray)` invokes immediately with `this` and an array of arguments.
- `bind(thisArg, ...args)` returns a new function with `this` bound and optional initial arguments.

Browser callbacks often rely on these methods for correct `this` handling.

```js
const btn = document.querySelector('button');

const handler = function (event) {
  console.log(this.id, event.type);
}.bind(btn);

btn.addEventListener('click', handler);
```

A common browser-style helper is `on` for event binding:

```js
function on(element, eventName, listener) {
  element.addEventListener(eventName, listener);
}

on(btn, 'click', () => console.log('clicked'));
```

These patterns are useful when working with DOM events, callbacks, and method borrowing between objects.

---

## 5. Objects and Prototypes

### 5.1 Object literals

```js
const user = {
  name: 'Jane',
  age: 28,
  greet() {
    console.log(`Hello, ${this.name}`);
  }
};
```

### 5.2 Prototype inheritance

Every object has a prototype.

```js
const animal = { speak() { console.log('sound'); } };
const dog = Object.create(animal);
dog.bark = () => console.log('woof');
dog.speak();
```

### 5.3 Constructor functions

```js
function Person(name) {
  this.name = name;
}
Person.prototype.greet = function () {
  console.log(`Hi, ${this.name}`);
};

const p = new Person('Sam');
p.greet();
```

### 5.4 ES6 classes

```js
class Person {
  constructor(name) {
    this.name = name;
  }

  greet() {
    console.log(`Hi, ${this.name}`);
  }
}

const p = new Person('Sara');
p.greet();
```

Classes are syntactic sugar over prototypes.

### 5.5 Prototype chain

Every JavaScript object can delegate property access to its prototype. This creates a chain of objects used during lookup.

```js
const animal = {
  speak() {
    console.log('sound');
  }
};

const dog = Object.create(animal);
dog.bark = () => console.log('woof');

dog.speak(); // delegates to animal.speak
console.log(Object.getPrototypeOf(dog) === animal); // true
```

With property lookup, the runtime checks the object first and then walks its prototype chain.

```js
console.log(dog.hasOwnProperty('bark'));  // true
console.log(dog.hasOwnProperty('speak')); // false
```

Use `Object.getPrototypeOf(obj)` instead of `__proto__` for portability.

### 5.6 Prototype-based sharing and inheritance

Constructor functions and classes both share methods via `prototype`.

```js
function Person(name) {
  this.name = name;
}

Person.prototype.greet = function () {
  console.log(`Hi, ${this.name}`);
};

const p = new Person('Sam');
p.greet();

// This is equivalent to:
class PersonClass {
  constructor(name) {
    this.name = name;
  }

  greet() {
    console.log(`Hi, ${this.name}`);
  }
}
```

If you need shared behavior across objects, attach methods to the prototype rather than creating them per-instance.

---

## 6. Modules

### 6.1 ES modules

```js
// utils.js
export function add(a, b) {
  return a + b;
}

// app.js
import { add } from './utils.js';
console.log(add(1, 2));
```

### 6.2 CommonJS

```js
// util.js
module.exports = function add(a, b) {
  return a + b;
};

// app.js
const add = require('./util');
```

### 6.3 Default export

```js
export default class Logger {}
import Logger from './logger.js';
```

---

## 7. Symbols

Symbols are unique primitive values.

```js
const key = Symbol('id');
const obj = { [key]: 123 };
```

Symbols are useful for private-like object keys and avoiding name collisions.

### 7.1 Well-known symbols

- `Symbol.iterator`
- `Symbol.toStringTag`
- `Symbol.asyncIterator`

Example:

```js
const iterable = {
  [Symbol.iterator]() {
    let i = 0;
    return {
      next() {
        return { value: i++, done: i > 3 };
      }
    };
  }
};
```

---

## 8. Error Handling

### 8.1 `try/catch/finally`

```js
try {
  throw new Error('failed');
} catch (err) {
  console.error(err.message);
} finally {
  console.log('cleanup');
}
```

### 8.2 Custom errors

```js
class ValidationError extends Error {
  constructor(message) {
    super(message);
    this.name = 'ValidationError';
  }
}
```

### 8.3 `throw` values

You can throw any value, but `Error` objects are best.

---

## 9. Advanced JavaScript Concepts

### 9.1 `this` binding

`this` depends on how a function is called.

```js
const obj = {
  name: 'A',
  hello() { console.log(this.name); }
};

obj.hello(); // 'A'
const fn = obj.hello;
fn(); // undefined or global
```

Arrow functions do not have their own `this`.

### 9.2 Execution context and hoisting

Function and `var` declarations are hoisted.

```js
console.log(foo); // undefined
var foo = 1;
```

`let` and `const` are not accessible before initialization.

### 9.3 Event loop and async queue

JavaScript has a single-threaded execution model with an event loop.

- Microtasks: `Promise.then`, `queueMicrotask`
- Macrotasks: `setTimeout`, I/O callbacks

Example:

```js
console.log('start');
Promise.resolve().then(() => console.log('micro')); 
setTimeout(() => console.log('macro'), 0);
console.log('end');
```

Output order:
`start`, `end`, `micro`, `macro`.

### 9.4 `async` / `await`

```js
async function fetchData() {
  const response = await fetch('/data');
  return await response.json();
}
```

Async functions return promises.

### 9.5 Generators

```js
function* gen() {
  yield 1;
  yield 2;
}
const iterator = gen();
console.log(iterator.next());
```

Generators can implement custom iterable behavior.

---

## 10. Prototypes vs Classes

### 10.1 Prototype chain

Objects delegate to their prototype.

```js
const base = { greet() { console.log('hi'); } };
const child = Object.create(base);
child.greet();
```

### 10.2 Prototype methods

```js
function Person(name) {
  this.name = name;
}
Person.prototype.say = function () {
  console.log(this.name);
};
```

### 10.3 Class syntax

Classes wrap prototype logic in modern syntax.

```js
class Person {
  constructor(name) {
    this.name = name;
  }

  say() {
    console.log(this.name);
  }
}
```

---

## 11. Modern JavaScript Features

### 11.1 Destructuring

```js
const [a, b] = [1, 2];
const { name, age } = user;
```

### 11.2 Spread and rest

```js
const arr = [1, 2];
const copy = [...arr];
function sum(...values) { return values.reduce((a, b) => a + b, 0); }
```

### 11.3 Optional chaining and nullish coalescing

```js
const value = obj?.prop?.sub ?? 'default';
```

### 11.4 Template literals

```js
const msg = `Hello ${name}`;
```

### 11.5 Default parameters

```js
function greet(name = 'Guest') {
  console.log(`Hello, ${name}`);
}
```

---

## 12. Performance and Best Practices

### 12.1 Avoid global state

Globals can create hard-to-debug behavior.

### 12.2 Immutable patterns

Prefer immutability where possible.

### 12.3 Efficient loops

Use `for` loops, `for...of`, or `Array.prototype` methods based on readability and performance.

### 12.4 Avoid frequent property lookups

Cache values if used repeatedly.

### 12.5 Minimize object allocations in hot paths

Reuse arrays and objects when possible.

---

## 13. Important Concepts Summary

- `LHS` is assignment target.
- `RHS` is evaluated value.
- Errors are managed via `try/catch/finally` and `throw`.
- `prototype` is the core inheritance mechanism behind objects and classes.
- `module` syntax is the modern standard for importing and exporting code.
- `Symbol` creates unique property keys and enables internal iteration.

---

## 14. Practical examples

### 14.1 Promises

```js
const promise = new Promise((resolve, reject) => {
  setTimeout(() => resolve('done'), 1000);
});
promise.then(console.log).catch(console.error);
```

### 14.2 Async iterator

```js
async function* asyncRange(n) {
  for (let i = 0; i < n; i++) {
    yield i;
  }
}
for await (const value of asyncRange(3)) {
  console.log(value);
}
```

### 14.3 Proxy example

```js
const target = {};
const proxy = new Proxy(target, {
  get(obj, prop) {
    console.log('get', prop);
    return obj[prop];
  }
});
proxy.foo = 1;
console.log(proxy.foo);
```

---

## Closing

This runbook provides a strong foundation from JavaScript basics through advanced runtime and language features. Use it as a learning guide and reference while building browser or Node.js applications.

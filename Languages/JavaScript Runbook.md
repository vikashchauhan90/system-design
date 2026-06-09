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

### 1.4 LHS vs RHS values

JavaScript expressions evaluate as either LHS or RHS values.

- RHS (right-hand side) means "evaluate and read the value." Example: `const x = y + 1;` uses `y` as an RHS value.
- LHS (left-hand side) means "assign to this location." Example: `x = 10;` uses `x` as an LHS reference.

A variable can appear as both: `x = y = 5;`

Understanding LHS/RHS is critical for destructuring, assignment, and scope.

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
```

Classes are syntactic sugar over prototypes.

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

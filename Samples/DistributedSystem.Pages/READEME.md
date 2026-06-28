# Page-based Storage

A **Page-based Storage Engine** is one of the oldest and most important designs used in databases. It’s not a single data structure—it’s a **way of organizing data on disk and in memory using fixed-size blocks called pages**.

This is the foundation behind systems like:

* PostgreSQL
* MySQL (InnoDB engine)
* Many B-Tree / B+Tree based databases

---

# 1. What is a Page-based Storage Engine?

A **page-based storage engine** stores everything in fixed-size chunks called **pages**.

### Core idea:

> All data (rows, index nodes, metadata) is stored in pages, not individually.

---

## Page structure

A page is usually:

```text
4KB / 8KB / 16KB block
```

Inside a page:

```text
+----------------------+
| Header              |
|----------------------|
| Slot directory      |
|----------------------|
| Record 1            |
| Record 2            |
| Record 3            |
| Free space          |
+----------------------+
```

---

# 2. Why pages are used

Pages solve 3 big problems:

### ✔ 1. Disk efficiency

Disk I/O is expensive, so reading 1 record is not efficient.

Instead:

> read 1 page (many records at once)

---

### ✔ 2. Memory caching

Databases cache pages in memory:

```text
Disk → Page → Buffer Pool (RAM)
```

---

### ✔ 3. Indexing simplicity

B-Trees and indexes operate on pages, not rows.

---

# 3. Key Components of a Page-based Engine

## 1. Page

Fixed-size container.

```text
PageID: 42
Records: [A, B, C]
```

---

## 2. Buffer Pool (Page Cache)

Memory area that stores recently used pages.

```text
Disk Page → Buffer Pool → CPU
```

---

## 3. Write-Ahead Log (WAL)

Ensures durability:

```text
1. Write log
2. Modify page
3. Flush later
```

---

## 4. Page Directory / Index

Maps keys → pages:

```text
10 → Page1
20 → Page2
30 → Page3
```

---

# 4. How Write Works

```text
Client Write
     ↓
Find Page
     ↓
Load Page into Buffer Pool
     ↓
Modify Page
     ↓
Write WAL
     ↓
Mark Page Dirty
     ↓
ACK
     ↓
Flush later
```

---

# 5. How Read Works

```text
Client Read
     ↓
Check Buffer Pool
     ↓
If found → return
Else → load page from disk
     ↓
Return record
```

---

# 6. How Flush Works

```text
Dirty Pages
     ↓
Flush Thread
     ↓
Write to Disk
     ↓
Mark Clean
```

---

# 7. Simple Page-Based Storage Engine (Python)

Now let's implement a **minimal educational version**.

---

## 1. Page

```python
class Page:
    def __init__(self, page_id, capacity=4):
        self.page_id = page_id
        self.records = []
        self.capacity = capacity

    def is_full(self):
        return len(self.records) >= self.capacity

    def insert(self, record):
        if self.is_full():
            return False
        self.records.append(record)
        self.records.sort(key=lambda x: x["key"])
        return True
```

---

## 2. Buffer Pool

```python
class BufferPool:
    def __init__(self):
        self.pages = {}

    def get(self, page_id):
        return self.pages.get(page_id)

    def put(self, page):
        self.pages[page.page_id] = page
```

---

## 3. WAL (Commit Log)

```python
class WAL:
    def __init__(self):
        self.log = []

    def write(self, record):
        self.log.append(record)

    def replay(self):
        return self.log
```

---

## 4. Storage Engine

```python
class StorageEngine:
    def __init__(self):
        self.pages = {}
        self.buffer = BufferPool()
        self.wal = WAL()
        self.page_counter = 0

    def _new_page(self):
        page = Page(self.page_counter)
        self.pages[self.page_counter] = page
        self.page_counter += 1
        return page

    def insert(self, key, value):
        record = {"key": key, "value": value}

        # 1. WAL first (durability)
        self.wal.write(record)

        # 2. Find a page
        for page in self.pages.values():
            if not page.is_full():
                page.insert(record)
                self.buffer.put(page)
                return

        # 3. Create new page if needed
        page = self._new_page()
        page.insert(record)
        self.buffer.put(page)

    def search(self, key):
        for page in self.pages.values():
            for r in page.records:
                if r["key"] == key:
                    return r
        return None

    def flush(self):
        # simulate disk flush
        print("Flushing pages to disk...")
        for p in self.pages.values():
            print(f"Page {p.page_id}: {p.records}")
```

---

## 5. Usage

```python
db = StorageEngine()

db.insert(10, "Alice")
db.insert(20, "Bob")
db.insert(15, "Charlie")

print(db.search(20))

db.flush()
```

---

# 8. What you just built

This is a **mini page-based storage engine** with:

### ✔ Pages

### ✔ Buffer pool (in-memory cache)

### ✔ WAL (commit log)

### ✔ Basic storage layer

---

# 9. Where this evolves in real databases

This simple model becomes:

## Level 1

Page-based engine (what we built)

## Level 2

B-Tree engine:

* pages become tree nodes

## Level 3

Full database engine:

* WAL + Buffer Pool + B+Tree

## Level 4 (modern systems)

LSM Tree engine:

* MemTable replaces pages
* SSTables replace disk pages

---

# 10. Key takeaway

A **Page-based Storage Engine is NOT a data structure**.

It is:

> A disk + memory architecture where all data is stored in fixed-size pages, managed through a buffer pool and protected by a write-ahead log.

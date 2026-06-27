## Log-Structured Merge-Tree (LSM Tree) Explained

An **LSM Tree** is a data structure designed for **high write throughput** in storage systems like Cassandra, RocksDB, and LevelDB. Instead of updating data in place (like B-Trees), it **batches writes** in memory and periodically flushes them to disk as immutable files.

### 🧠 Core Concept

Think of an LSM Tree like taking notes:

1. **Write quickly**: Jot down notes on a sticky note (memory)
2. **Batch organize**: When sticky notes pile up, transfer them to a notebook page (disk file)
3. **Periodically merge**: Combine notebook pages to remove duplicates and keep things organized (compaction)

### 🎯 Key Components

| Component | Description | Location |
|-----------|-------------|----------|
| **MemTable** | In-memory structure (often a balanced tree) for fast writes | RAM |
| **Commit Log** | Append-only log for durability (write-ahead log) | Disk |
| **SSTable** | Sorted String Table - immutable sorted files | Disk |
| **Compaction** | Background process merging SSTables | Disk |

### 📊 How It Works: Step by Step

```text
Write Path:
┌─────────────┐
│   Write     │
│   Request   │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│ 1. Write to     │
│    Commit Log   │ ← DURABILITY (crash recovery)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ 2. Add to       │
│    MemTable     │ ← FAST IN-MEMORY WRITE
└────────┬────────┘
         │
         ▼
    [MemTable fills up]
         │
         ▼
┌─────────────────┐
│ 3. Flush to     │
│    SSTable      │ ← IMMUTABLE SORTED FILE
└─────────────────┘

Read Path:
┌─────────────┐
│   Read      │
│   Request   │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│ 1. Check        │
│    MemTable     │ ← LATEST DATA
└────────┬────────┘
         │ (not found)
         ▼
┌─────────────────┐
│ 2. Check        │
│    SSTables     │ ← OLDER DATA (newest first)
└─────────────────┘
```


## 📊 Visual Flow of LSM Tree

### Write Process:

```text
┌─────────────┐
│ Write       │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ COMMIT LOG (append-only for durability) │
└─────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ MEMTABLE (in-memory sorted tree)        │
│ ┌─────────────────────────────────┐    │
│ │ "user_0001" → "Alice"            │    │
│ │ "user_0002" → "Bob"              │    │
│ │ "user_0003" → "Charlie"          │    │
│ └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
       │
       ▼ (when memtable is full)
┌─────────────────────────────────────────┐
│ FLUSH TO SSTABLE                        │
│ ┌─────────────────────────────────┐    │
│ │ SSTable 1 (sorted on disk)      │    │
│ │ user_0001 → "Alice"             │    │
│ │ user_0002 → "Bob"               │    │
│ │ user_0003 → "Charlie"           │    │
│ └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
```

### Read Process:

```text
┌─────────────┐
│ Read        │
│ "user_0002" │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────────┐
│ 1. CHECK MEMTABLE FIRST                 │
│    (latest data)                        │
└──────────┬──────────────────────────────┘
           │ (not found)
           ▼
┌─────────────────────────────────────────┐
│ 2. CHECK SSTABLES (newest to oldest)   │
│    ┌─────────────────────────────┐    │
│    │ SSTable 3 (newest)           │    │
│    │ SSTable 2                    │    │
│    │ SSTable 1 (oldest)           │    │
│    └─────────────────────────────┘    │
└─────────────────────────────────────────┘
```

## 🎯 Key Benefits of LSM Trees

| Benefit | Explanation |
|---------|-------------|
| **High Write Throughput** | Sequential writes to disk (append-only) |
| **Low Write Amplification** | B-Trees write to disk multiple times per update |
| **Compression Friendly** | Large sorted blocks compress well |
| **Predictable Performance** | No in-place updates, only sequential writes |
| **Great for SSDs** | Minimizes random I/O |

## 📈 Performance Comparison

```text
Operation   |  B-Tree   |  LSM Tree
------------|-----------|-------------
Write       |  O(log N) |  O(1) ⚡
Read (latest)| O(log N) |  O(1) ⚡
Read (old)  |  O(log N) |  O(log N)
Range Query |  O(log N) |  O(log N + K)
```

## 🎓 Summary

An LSM Tree is essentially:

1. **Write fast**: Buffer writes in memory (MemTable)
2. **Batch flush**: Periodically write to disk as sorted files (SSTables)
3. **Read smart**: Check memory first, then recent disk files
4. **Clean up**: Background process merges files (Compaction)

This design gives **extremely high write throughput** at the cost of slightly slower reads for old data. Perfect for write-heavy workloads like logging, time-series data, and distributed databases! 🚀
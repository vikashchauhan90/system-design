# Raft Implementation Architecture

## 1. Overview

This Raft implementation models a Raft node as an asynchronous Tokio actor. Each node:

- Maintains persistent Raft state (`current_term`, `voted_for`, log entries)
- Tracks volatile state (`role`, `current_leader`, election timer)
- Sends and receives Raft RPCs via Tokio channels
- Handles client proposals and consensus through leader election and log replication

---

## 2. Core Components

### `raft/src/node.rs`

Primary Raft state machine and event loop.

- `RaftNode<S: Storage>`
  - `config: RaftConfig`
  - `state: RaftState`
  - `storage: S`
  - `leader_state: Option<LeaderState>`
  - `command_rx: UnboundedReceiver<Command>`
  - `nodes: HashMap<String, UnboundedSender<Message>>`
  - `heartbeat_interval: Duration`

Main event sources:
- external commands (`Command`)
- election timer
- heartbeat timer

Key behaviors:
- `run()`: tokio `select!` loop
- `check_election_timeout()`: triggers elections
- `start_election()`: sends `RequestVote` RPCs
- `become_leader()`: initializes leader replication state
- `send_heartbeats()`: sends `AppendEntries` / heartbeats
- `handle_append_entries()`, `handle_request_vote()`
- `handle_propose()`: leader appends client commands to log

---

### `raft/src/state.rs`

Persistent and volatile Raft node state.

- `RaftState`
  - `current_term`
  - `voted_for`
  - `log: RaftLog`
  - `role: RaftRole`
  - `current_leader`
  - `election_timeout`
  - `last_heartbeat`

State transitions:
- `become_follower()`
- `become_candidate()`
- `become_leader()`
- `update_term()`
- `vote_for()`
- `is_log_up_to_date()`

---

### `raft/src/log.rs`

Raft log replication and commit tracking.

- `RaftLog`
  - `entries: Vec<LogEntry>`
  - `commit_index`
  - `applied_index`
  - `stable`

Functions:
- `append()`
- `append_from(prev_log_index, entries)`
- `commit(index)`
- `apply_committed()`
- `get_entries_from(start_index)`
- `term_at(index)`

---

### `raft/src/leader.rs`

Leader-side replication metadata.

- `LeaderState`
  - `next_index: Vec<u64>`
  - `match_index: Vec<u64>`

Used to:
- track what to send next to each follower
- calculate commit index from majority replication

---

### `raft/src/replication.rs`

Replication payload wrapper.

- `ReplicationState`
  - `term`
  - `entries`
  - `commit_index`
  - `leader_id`

---

### `raft/src/config.rs`

Cluster and timeout configuration.

- `RaftConfig`
  - `node_id`
  - `cluster_nodes`
  - `election_timeout_min/max`
  - `heartbeat_interval`
  - `channel_buffer_size`
  - `max_entries_per_rpc`

Supports randomized election timeout via `random_election_timeout()`.

---

### `raft/src/node_handler.rs`

External client API wrapper around command channel.

- `RaftNodeHandle`
  - `propose()`
  - `get_leader()`
  - `get_status()`

This isolates the `RaftNode` internals behind a message-based handle.

---

### `raft/src/message/*`

RPC and log entry definitions.

- `RequestVote`
- `RequestVoteResponse`
- `AppendEntries`
- `AppendEntriesResponse`
- `LogEntry`
- `Message` enum wrapping the RPCs

---

### `raft/src/storage/*`

Persistence abstraction and storage implementations.

- `storage::Storage`
  - `save_current_term()`
  - `load_current_term()`
  - `save_voted_for()`
  - `load_voted_for()`
  - `save_log_entry()`
  - `load_log_entries()`

Implementations:
- `InMemoryStorage`
- `FileStorage`

`FileStorage` persists term/vote as text files and logs with `bincode`.

---

## 3. Communication Model

This Raft implementation uses Tokio channels instead of network sockets:

- Each node has a `nodes: HashMap<String, UnboundedSender<Message>>`
- RPCs are sent by `sender.send(Message::...)`
- Response paths use `tokio::sync::oneshot::channel`

The node loop processes:
- local commands
- incoming RPC requests
- election/heartbeat timer events

---

## 4. Election & Leadership

Election flow:

1. Follower timeout expires
2. Node becomes candidate
3. Increments `current_term`, votes for self
4. Broadcasts `RequestVote` to other nodes
5. Collects votes via oneshot responses
6. If majority achieved, `become_leader()`

Leader flow:

- Initialize `LeaderState`
- Send heartbeat / log replication via `AppendEntries`
- Track follower replication progress
- Update commit index using majority `match_index`

---

## 5. Log Replication

Replication is performed by:

- `handle_propose()`: leader appends entry locally and persists it
- `send_heartbeats()`: includes log entries from `next_index`
- Followers validate `prev_log_index` / `prev_log_term`
- Append entries if consistent, or reject with conflict info
- Leader uses follower responses to advance `match_index`
- `update_commit_index()` commits once a majority replicate an index

---

## 6. Persistence & Recovery

Persistent state:
- `current_term`
- `voted_for`
- log entries

Storage is pluggable:
- `InMemoryStorage` for tests/demo
- `FileStorage` for simple durable storage

`RaftNode` saves term/vote during elections and saves each log entry on append.

---

## 7. Simplifications / Notable Design Notes

This implementation is a lean educational/demo Raft with simplifications:

- Cluster communication is channel-based, not TCP/gRPC
- `FileStorage.save_log_entry()` rewrites the full log, not append-only
- Leader progress updates appear conceptually handled, but response handling is simplified
- Commit index advancement checks current term but does not fully mirror production Raft edge cases
- No explicit snapshotting or log compaction

---

## 8. Recommended Architecture Diagram

A simple architecture diagram should show:

- `RaftNode`
  - `RaftState`
  - `RaftLog`
  - `LeaderState`
  - `Storage`
  - `RaftNodeHandle`
- `Message` flow between nodes:
  - `RequestVote` / `RequestVoteResponse`
  - `AppendEntries` / `AppendEntriesResponse`
- Timers:
  - election timeout
  - heartbeat interval

use crate::message::LogEntry;

/// Raft log implementation
#[derive(Debug, Clone)]
pub struct RaftLog {
    entries: Vec<LogEntry>,
    commit_index: u64,
    applied_index: u64,
    stable: bool,
}

impl Default for RaftLog {
    fn default() -> Self {
        Self::new()
    }
}

impl RaftLog {
    pub fn new() -> Self {
        Self {
            entries: Vec::new(),
            commit_index: 0,
            applied_index: 0,
            stable: true,
        }
    }

    /// Get entry at specific index (1-indexed)
    pub fn get(&self, index: u64) -> Option<&LogEntry> {
        if index == 0 {
            return None; // Sentinel entry
        }
        self.entries.get((index - 1) as usize)
    }

    /// Get the last log index
    pub fn last_index(&self) -> u64 {
        self.entries.len() as u64
    }

    /// Get the term of the last log entry
    pub fn last_term(&self) -> u64 {
        self.entries.last().map(|e| e.term).unwrap_or(0)
    }

    /// Get term at specific index
    pub fn term_at(&self, index: u64) -> u64 {
        self.get(index).map(|e| e.term).unwrap_or(0)
    }

    /// Append a single entry
    pub fn append(&mut self, entry: LogEntry) {
        self.entries.push(entry);
        self.stable = false;
    }

    /// Append multiple entries, optionally truncating from a given index
    pub fn append_from(&mut self, prev_log_index: u64, entries: &[LogEntry]) -> bool {
        // Check consistency at prev_log_index
        if let Some(existing) = self.get(prev_log_index) {
            if existing.term != entries.first().map(|e| e.term).unwrap_or(0) {
                return false;
            }
        } else if prev_log_index != 0 {
            return false;
        }

        // Truncate conflicting entries
        let start_idx = prev_log_index as usize;
        if start_idx < self.entries.len() {
            self.entries.truncate(start_idx);
        }

        // Append new entries
        self.entries.extend_from_slice(entries);
        self.stable = false;
        true
    }

    /// Commit up to the given index
    pub fn commit(&mut self, index: u64) {
        if index > self.commit_index && index <= self.last_index() {
            self.commit_index = index;
        }
    }

    /// Get the commit index
    pub fn commit_index(&self) -> u64 {
        self.commit_index
    }

    /// Get the applied index
    pub fn applied_index(&self) -> u64 {
        self.applied_index
    }

    /// Mark entries as applied (up to the commit index)
    pub fn apply_committed(&mut self) -> Vec<&LogEntry> {
        let start = self.applied_index as usize;
        let end = self.commit_index as usize;

        if start >= end {
            return Vec::new();
        }

        let applied: Vec<&LogEntry> = self.entries[start..end].iter().collect();
        self.applied_index = self.commit_index;
        applied
    }

    /// Get entries from start_index onwards (for replication)
    pub fn get_entries_from(&self, start_index: u64) -> Vec<LogEntry> {
        if start_index == 0 {
            return self.entries.clone();
        }
        let start = (start_index - 1) as usize;
        if start >= self.entries.len() {
            Vec::new()
        } else {
            self.entries[start..].to_vec()
        }
    }

    /// Check if the log is stable (persisted)
    pub fn is_stable(&self) -> bool {
        self.stable
    }

    /// Mark as stable after persistence
    pub fn mark_stable(&mut self) {
        self.stable = true;
    }
}

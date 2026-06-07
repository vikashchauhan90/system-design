use crate::message::LogEntry;

/// Persistent storage trait for Raft state
pub trait Storage: Send + Sync {
    /// Save current term
    fn save_current_term(&self, term: u64) -> Result<(), String>;

    /// Load current term
    fn load_current_term(&self) -> Result<u64, String>;

    /// Save voted for
    fn save_voted_for(&self, voted_for: &str) -> Result<(), String>;

    /// Load voted for
    fn load_voted_for(&self) -> Result<String, String>;

    /// Save log entry
    fn save_log_entry(&self, index: u64, entry: &LogEntry) -> Result<(), String>;

    /// Load log entries
    fn load_log_entries(&self) -> Result<Vec<LogEntry>, String>;
}
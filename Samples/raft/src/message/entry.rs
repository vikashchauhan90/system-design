use serde::{Deserialize, Serialize};

/// Log entry containing a command and its term
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct LogEntry {
    pub term: u64,
    pub command: Vec<u8>,
}

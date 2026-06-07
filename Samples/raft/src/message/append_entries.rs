use super::entry::LogEntry;
use serde::{Deserialize, Serialize};

/// AppendEntries RPC (leader to follower)
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AppendEntries {
    pub term: u64,
    pub leader_id: String,
    pub prev_log_index: u64,
    pub prev_log_term: u64,
    pub entries: Vec<LogEntry>,
    pub leader_commit: u64,
}

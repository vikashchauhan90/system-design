use crate::message::LogEntry;

/// Leader replication state
#[derive(Debug, Clone)]
pub struct ReplicationState {
    pub term: u64,
    pub entries: Vec<LogEntry>,
    pub commit_index: u64,
    pub leader_id: String,
}

impl ReplicationState {
    pub fn is_heartbeat(&self) -> bool {
        self.entries.is_empty()
    }
}

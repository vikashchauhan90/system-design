use crate::RaftRole;

/// Status information for monitoring
#[derive(Debug, Clone)]
pub struct RaftStatus {
    pub role: RaftRole,
    pub term: u64,
    pub current_leader: Option<String>,
    pub commit_index: u64,
    pub applied_index: u64,
    pub last_log_index: u64,
    pub last_log_term: u64,
}
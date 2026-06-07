use serde::{Serialize, Deserialize};

/// RequestVote RPC (candidate to follower)
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RequestVote {
    pub term: u64,
    pub candidate_id: String,
    pub last_log_index: u64,
    pub last_log_term: u64,
}
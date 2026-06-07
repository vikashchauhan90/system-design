use serde::{Deserialize, Serialize};

/// AppendEntries response (follower to leader)
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AppendEntriesResponse {
    pub term: u64,
    pub success: bool,
    pub match_index: u64,
    pub conflict_term: Option<u64>,
    pub conflict_index: Option<u64>,
}

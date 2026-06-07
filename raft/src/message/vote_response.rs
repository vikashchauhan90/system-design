use serde::{Serialize, Deserialize};

/// RequestVote response (follower to candidate)
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RequestVoteResponse {
    pub term: u64,
    pub vote_granted: bool,
}
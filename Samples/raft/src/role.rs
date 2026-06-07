/// Raft node role
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum RaftRole {
    Follower,
    Candidate,
    Leader,
}

impl RaftRole {
    pub fn as_str(&self) -> &'static str {
        match self {
            RaftRole::Follower => "FOLLOWER",
            RaftRole::Candidate => "CANDIDATE",
            RaftRole::Leader => "LEADER",
        }
    }
}

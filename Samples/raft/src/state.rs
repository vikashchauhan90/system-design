use super::log::RaftLog;
use super::role::RaftRole;
use std::time::{Duration, Instant};

/// Raft state machine
#[derive(Debug, Clone)]
pub struct RaftState {
    /// Current term (persistent)
    pub current_term: u64,

    /// Candidate voted for in current term (persistent)
    pub voted_for: String,

    /// Raft log (persistent)
    pub log: RaftLog,

    /// Current role
    pub role: RaftRole,

    /// Current leader (if known)
    pub current_leader: Option<String>,

    /// Timeout for election
    pub election_timeout: Duration,

    /// Last time we received a heartbeat from leader
    pub last_heartbeat: Instant,
}

impl RaftState {
    pub fn new(node_id: &str) -> Self {
        Self {
            current_term: 0,
            voted_for: String::new(),
            log: crate::log::RaftLog::new(),
            role: RaftRole::Follower,
            current_leader: None,
            election_timeout: Duration::from_millis(150),
            last_heartbeat: Instant::now(),
        }
    }

    /// Check if election timeout has elapsed
    pub fn election_timeout_elapsed(&self) -> bool {
        self.last_heartbeat.elapsed() > self.election_timeout
    }

    /// Update term (reset on term change)
    pub fn update_term(&mut self, new_term: u64) -> bool {
        if new_term > self.current_term {
            self.current_term = new_term;
            self.voted_for = String::new();
            self.role = RaftRole::Follower;
            self.current_leader = None;
            true
        } else {
            false
        }
    }

    /// Vote for a candidate in the current term
    pub fn vote_for(&mut self, candidate_id: &str) -> bool {
        if self.voted_for.is_empty() || self.voted_for == candidate_id {
            self.voted_for = candidate_id.to_string();
            true
        } else {
            false
        }
    }

    /// Become leader and initialize leader state
    pub fn become_leader(&mut self) {
        self.role = RaftRole::Leader;
        self.current_leader = None; // Leader doesn't track itself
        // Reset election timer
        self.last_heartbeat = Instant::now();
    }

    /// Become follower
    pub fn become_follower(&mut self, leader_id: Option<String>) {
        self.role = RaftRole::Follower;
        self.current_leader = leader_id;
        self.last_heartbeat = Instant::now();
    }

    /// Become candidate and increment term
    pub fn become_candidate(&mut self, node_id: &str) {
        self.role = RaftRole::Candidate;
        self.current_term += 1;
        self.voted_for = node_id.to_string();
        self.current_leader = None;
        // Reset heartbeat timer after becoming candidate
        self.last_heartbeat = Instant::now();
    }

    /// Check if candidate's log is up-to-date for voting
    pub fn is_log_up_to_date(&self, last_log_index: u64, last_log_term: u64) -> bool {
        let last_term = self.log.last_term();
        if last_log_term != last_term {
            last_log_term >= last_term
        } else {
            last_log_index >= self.log.last_index()
        }
    }
}

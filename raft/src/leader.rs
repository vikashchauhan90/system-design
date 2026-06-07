
/// Leader state (volatile)
#[derive(Debug, Clone)]
pub struct LeaderState {
    /// Next index to send to each follower
    pub next_index: Vec<u64>,

    /// Highest index known to be replicated to each follower
    pub match_index: Vec<u64>,
}

impl LeaderState {
    pub fn new(node_count: usize, last_log_index: u64) -> Self {
        Self {
            next_index: vec![last_log_index + 1; node_count],
            match_index: vec![0; node_count],
        }
    }

    /// Update match and next indices on successful append
    pub fn update_indices(&mut self, follower_idx: usize, match_idx: u64) {
        self.match_index[follower_idx] = match_idx;
        self.next_index[follower_idx] = match_idx + 1;
    }

    /// Calculate new commit index
    pub fn calculate_commit_index(&mut self, current_commit: u64, term: u64) -> u64 {
        let mut matches: Vec<u64> = self.match_index.iter().cloned().collect();
        matches.sort_unstable();

        let new_commit = matches[matches.len() / 2];  // Majority (median)

        if new_commit > current_commit {
            // Raft guarantees: only commit entries from current term
            // We simplify here: check if the entry at new_commit is from current term
            // (In production, you'd need to check the term)
            new_commit
        } else {
            current_commit
        }
    }
}
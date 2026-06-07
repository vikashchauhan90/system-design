use std::time::Duration;
use rand::Rng;

/// Configuration for a Raft node

#[derive(Debug, Clone)]
pub struct RaftConfig {
    /// Node ID (unique in cluster)
    pub node_id: String,

    /// List of all nodes in the cluster (node_id -> address)
    pub cluster_nodes: Vec<String>,

    /// Election timeout range (min to max for randomization)
    pub election_timeout_min: Duration,
    pub election_timeout_max: Duration,

    /// Heartbeat interval (leader sends to followers)
    pub heartbeat_interval: Duration,

    /// Maximum size of RPC channel buffers
    pub channel_buffer_size: usize,

    /// Maximum number of entries per AppendEntries RPC
    pub max_entries_per_rpc: usize,
}

impl Default for RaftConfig {
    fn default() -> Self {
        Self {
            node_id: String::new(),
            cluster_nodes: Vec::new(),
            election_timeout_min:Duration::from_millis(150),
            election_timeout_max:Duration::from_millis(300),
            heartbeat_interval:Duration::from_millis(50),
            channel_buffer_size:100,
            max_entries_per_rpc: 100
        }
    }
}

impl RaftConfig {
    pub fn new(node_id: String, cluster_nodes: Vec<String>) -> Self {
        Self {
            node_id,
            cluster_nodes,
            ..Default::default()
        }
    }

    pub fn random_election_timeout(&self) -> Duration {
        let mut rng = rand::thread_rng();
        let timeout =rng.gen_range(
            self.election_timeout_min.as_millis()..self.election_timeout_max.as_millis()
        );
        Duration::from_millis(timeout as u64)
    }

}
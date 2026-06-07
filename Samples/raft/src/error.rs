/// Error types for Raft operations
#[derive(Debug, thiserror::Error)]
pub enum RaftError {
    #[error("Not leader: current leader is {0}")]
    NotLeader(String),

    #[error("Log inconsistency: {0}")]
    LogInconsistency(String),

    #[error("Storage error: {0}")]
    StorageError(String),

    #[error("RPC error: {0}")]
    RpcError(String),
}

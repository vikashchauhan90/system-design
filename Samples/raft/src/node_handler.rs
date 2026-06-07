use crate::{Command, RaftStatus};
use tokio::sync::mpsc::UnboundedSender;

/// Raft node handle for external communication
#[derive(Clone)]
pub struct RaftNodeHandle {
    pub command_tx: UnboundedSender<Command>,
}

impl RaftNodeHandle {
    /// Propose a command to the Raft cluster
    pub fn propose(&self, command: Vec<u8>) -> Result<(), String> {
        self.command_tx
            .send(Command::Propose(command))
            .map_err(|_| "Raft node closed".to_string())
    }

    /// Get the current leader
    pub async fn get_leader(&self) -> Option<String> {
        let (tx, rx) = tokio::sync::oneshot::channel::<Option<String>>();
        if self
            .command_tx
            .send(Command::GetLeader { response_tx: tx })
            .is_err()
        {
            return None;
        }
        rx.await.ok()?
    }

    /// Get the current Raft status
    pub async fn get_status(&self) -> Option<RaftStatus> {
        let (tx, rx) = tokio::sync::oneshot::channel::<RaftStatus>();
        if self
            .command_tx
            .send(Command::GetStatus { response_tx: tx })
            .is_err()
        {
            return None;
        }
        rx.await.ok()
    }
}

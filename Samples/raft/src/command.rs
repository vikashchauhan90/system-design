use crate::message::{AppendEntries, AppendEntriesResponse, RequestVote, RequestVoteResponse};
use crate::status::RaftStatus;

/// Internal commands for the Raft node
pub enum Command {
    Propose(Vec<u8>),
    AppendEntries {
        entries: AppendEntries,
        response_tx: tokio::sync::oneshot::Sender<AppendEntriesResponse>,
    },
    RequestVote {
        request: RequestVote,
        response_tx: tokio::sync::oneshot::Sender<RequestVoteResponse>,
    },
    GetLeader {
        response_tx: tokio::sync::oneshot::Sender<Option<String>>,
    },
    GetStatus {
        response_tx: tokio::sync::oneshot::Sender<RaftStatus>,
    },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MessageType {
    AppendEntries,
    AppendEntriesResponse,
    RequestVote,
    RequestVoteResponse,
}

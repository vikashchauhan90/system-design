pub mod append_entries;
pub mod append_entries_response;
pub mod entry;
pub mod message;
pub mod message_type;
pub mod vote;
pub mod vote_response;

// Re-export commonly used types
pub use append_entries::AppendEntries;
pub use append_entries_response::AppendEntriesResponse;
pub use entry::LogEntry;
pub use message::Message;
pub use message_type::MessageType;
pub use vote::RequestVote;
pub use vote_response::RequestVoteResponse;

use super::append_entries::AppendEntries;
use super::append_entries_response::AppendEntriesResponse;
use super::vote::RequestVote;
use super::vote_response::RequestVoteResponse;
use super::message_type::MessageType;

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum Message {
    AppendEntries(AppendEntries),
    AppendEntriesResponse(AppendEntriesResponse),
    RequestVote(RequestVote),
    RequestVoteResponse(RequestVoteResponse),
}

impl Message {
    pub fn message_type(&self) -> MessageType {
        match self {
            Message::AppendEntries(_) => MessageType::AppendEntries,
            Message::AppendEntriesResponse(_) => MessageType::AppendEntriesResponse,
            Message::RequestVote(_) => MessageType::RequestVote,
            Message::RequestVoteResponse(_) => MessageType::RequestVoteResponse,
        }
    }
    pub fn term(&self) -> u64 {
        match self {
            Message::AppendEntries(msg) => msg.term,
            Message::AppendEntriesResponse(msg) => msg.term,
            Message::RequestVote(msg) => msg.term,
            Message::RequestVoteResponse(msg) => msg.term,
        }
    }
}
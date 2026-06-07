use crate::config::RaftConfig;
use crate::message::{
    AppendEntries, AppendEntriesResponse, LogEntry, Message, RequestVote, RequestVoteResponse,
};
use super::{
    LeaderState, RaftRole, RaftState, RaftStatus, Command
};
use crate::storage::{ Storage};
use std::collections::HashMap;
use std::time::Instant;
use tokio::sync::mpsc::{self, UnboundedReceiver, UnboundedSender};
use tokio::time::{self, Duration};

/// Main Raft node
pub struct RaftNode<S: Storage> {
    config: RaftConfig,
    state: RaftState,
    storage: S,
    leader_state: Option<LeaderState>,
    command_rx: UnboundedReceiver<Command>,
    nodes: HashMap<String, UnboundedSender<Message>>,
    heartbeat_interval: Duration,
}

impl<S: Storage> RaftNode<S> {
    /// Create a new Raft node
    pub fn new(
        config: RaftConfig,
        command_rx: UnboundedReceiver<Command>,
        nodes: HashMap<String, UnboundedSender<Message>>,
        storage: S,
    ) -> Self {
        let election_timeout = config.random_election_timeout();
        let heartbeat_interval = config.heartbeat_interval;
        let node_id = config.node_id.clone();

        let mut state = RaftState::new(&node_id);
        state.election_timeout = election_timeout;

        Self {
            config,
            state,
            storage,
            leader_state: None,
            command_rx,
            nodes,
            heartbeat_interval,
        }
    }

    /// Run the Raft node main loop
    pub async fn run(mut self) {
        let mut election_ticker = time::interval(Duration::from_millis(10));
        let mut heartbeat_ticker = time::interval(self.heartbeat_interval);

        loop {
            tokio::select! {
                Some(cmd) = self.command_rx.recv() => {
                    self.handle_command(cmd).await;
                }
                _ = election_ticker.tick() => {
                    self.check_election_timeout().await;
                }
                _ = heartbeat_ticker.tick() => {
                    if self.state.role == RaftRole::Leader {
                        self.send_heartbeats().await;
                    }
                }
            }
        }
    }

    /// Check if election timeout has been reached
    async fn check_election_timeout(&mut self) {
        if !self.state.election_timeout_elapsed() {
            return;
        }

        match self.state.role {
            RaftRole::Leader => {
                self.state.last_heartbeat = Instant::now();
            }
            _ => {
                self.start_election().await;
            }
        }
    }

    /// Start leader election
    async fn start_election(&mut self) {
        log::info!(
            "Node {} starting election for term {}",
            self.config.node_id,
            self.state.current_term + 1
        );

        self.state.become_candidate(&self.config.node_id);

        let _ = self.storage.save_current_term(self.state.current_term);
        let _ = self.storage.save_voted_for(&self.state.voted_for);

        let last_log_index = self.state.log.last_index();
        let last_log_term = self.state.log.last_term();

        let mut votes_received = 1;
        let total_nodes = self.config.cluster_nodes.len();
        let (vote_tx, mut vote_rx) = mpsc::unbounded_channel();

        for node_id in &self.config.cluster_nodes {
            if node_id == &self.config.node_id {
                continue;
            }

            let request = RequestVote {
                term: self.state.current_term,
                candidate_id: self.config.node_id.clone(),
                last_log_index,
                last_log_term,
            };

            if let Some(sender) = self.nodes.get(node_id) {
                let (resp_tx, resp_rx) = tokio::sync::oneshot::channel::<RequestVoteResponse>();
                let _ = sender.send(Message::RequestVote(request));

                let vote_tx_clone = vote_tx.clone();
                let node_id_clone = node_id.clone();

                tokio::spawn(async move {
                    match tokio::time::timeout(Duration::from_millis(150), resp_rx).await {
                        Ok(Ok(response)) => {
                            let _ = vote_tx_clone.send((node_id_clone, response));
                        }
                        _ => {
                            let _ = vote_tx_clone.send((node_id_clone, RequestVoteResponse {
                                term: 0,
                                vote_granted: false,
                            }));
                        }
                    }
                });
            }
        }

        let election_deadline = tokio::time::sleep(Duration::from_millis(200));
        tokio::pin!(election_deadline);

        loop {
            tokio::select! {
                Some((_node_id, response)) = vote_rx.recv() => {
                    if response.term > self.state.current_term {
                        self.state.update_term(response.term);
                        self.state.become_follower(None);
                        let _ = self.storage.save_current_term(self.state.current_term);
                        let _ = self.storage.save_voted_for("");
                        return;
                    }
                    if response.vote_granted {
                        votes_received += 1;
                    }

                    if votes_received > total_nodes / 2 {
                        self.become_leader().await;
                        return;
                    }
                }
                _ = &mut election_deadline => {
                    if votes_received > total_nodes / 2 {
                        self.become_leader().await;
                    }
                    return;
                }
            }
        }
    }

    /// Become leader
    async fn become_leader(&mut self) {
        log::info!("Node {} became leader for term {}", self.config.node_id, self.state.current_term);

        self.state.become_leader();

        let last_log_index = self.state.log.last_index();
        let node_count = self.config.cluster_nodes.len();
        self.leader_state = Some(LeaderState::new(node_count, last_log_index));

        self.send_heartbeats().await;
    }

    /// Send heartbeats to all followers
    async fn send_heartbeats(&mut self) {
        if self.state.role != RaftRole::Leader {
            return;
        }

        let commit_index = self.state.log.commit_index();
        let last_log_index = self.state.log.last_index();

        // Clone leader_state to avoid multiple mutable borrows
        let leader_state = self.leader_state.clone();

        for (idx, node_id) in self.config.cluster_nodes.iter().enumerate() {
            if node_id == &self.config.node_id {
                continue;
            }

            let next_idx = leader_state.as_ref().map(|ls| ls.next_index[idx]).unwrap_or(1);
            let entries = if next_idx <= last_log_index {
                self.state.log.get_entries_from(next_idx)
            } else {
                Vec::new()
            };

            let prev_log_index = if next_idx > 0 { next_idx - 1 } else { 0 };
            let prev_log_term = self.state.log.term_at(prev_log_index);

            let append_entries = AppendEntries {
                term: self.state.current_term,
                leader_id: self.config.node_id.clone(),
                prev_log_index,
                prev_log_term,
                entries: entries.clone(),
                leader_commit: commit_index,
            };

            if let Some(sender) = self.nodes.get(node_id) {
                let (resp_tx, resp_rx) = tokio::sync::oneshot::channel::<AppendEntriesResponse>();
                let _ = sender.send(Message::AppendEntries(append_entries));

                // Clone for the async task
                let node_id_clone = node_id.clone();
                let idx_clone = idx;
                let entries_len = entries.len();

                // Use a separate handle for leader_state update
                let leader_state_clone = self.leader_state.clone();

                tokio::spawn(async move {
                    match tokio::time::timeout(Duration::from_millis(100), resp_rx).await {
                        Ok(Ok(response)) => {
                            if let Some(mut ls) = leader_state_clone {
                                if response.success {
                                    // Need to update through the node's reference
                                    // This is handled in the update_commit_index method
                                    log::debug!("Follower {} acknowledged entries up to index {}",
                                        node_id_clone, response.match_index);
                                } else {
                                    log::debug!("Follower {} rejected AppendEntries", node_id_clone);
                                }
                            }
                        }
                        _ => {
                            log::debug!("Follower {} did not respond", node_id_clone);
                        }
                    }
                });
            }
        }

        self.update_commit_index().await;
    }

    /// Update commit index based on majority match indices
    async fn update_commit_index(&mut self) {
        if let Some(ref mut ls) = self.leader_state {
            let mut matches = ls.match_index.clone();
            matches.sort_unstable();
            let majority_idx = matches[matches.len() / 2];

            if majority_idx > self.state.log.commit_index() {
                let term_at_idx = self.state.log.term_at(majority_idx);
                if term_at_idx == self.state.current_term {
                    self.state.log.commit(majority_idx);
                    let applied = self.state.log.apply_committed();
                    for entry in applied {
                        log::debug!("Applying entry with term {}", entry.term);
                    }
                }
            }
        }
    }

    /// Handle AppendEntries RPC
    async fn handle_append_entries(
        &mut self,
        entries: AppendEntries,
    ) -> AppendEntriesResponse {
        if entries.term < self.state.current_term {
            return AppendEntriesResponse {
                term: self.state.current_term,
                success: false,
                match_index: self.state.log.last_index(),
                conflict_term: None,
                conflict_index: None,
            };
        }

        if entries.term > self.state.current_term {
            self.state.update_term(entries.term);
            self.state.become_follower(Some(entries.leader_id.clone()));
            let _ = self.storage.save_current_term(self.state.current_term);
        }

        self.state.last_heartbeat = Instant::now();
        self.state.current_leader = Some(entries.leader_id.clone());

        if entries.prev_log_index > 0 {
            let prev_term = self.state.log.term_at(entries.prev_log_index);
            if prev_term != entries.prev_log_term {
                let conflict_term = prev_term;
                let mut conflict_index = entries.prev_log_index;

                while conflict_index > 0 && self.state.log.term_at(conflict_index) == conflict_term {
                    conflict_index -= 1;
                }
                conflict_index += 1;

                return AppendEntriesResponse {
                    term: self.state.current_term,
                    success: false,
                    match_index: self.state.log.last_index(),
                    conflict_term: Some(conflict_term),
                    conflict_index: Some(conflict_index),
                };
            }
        }

        let success = if entries.entries.is_empty() {
            true
        } else {
            self.state.log.append_from(entries.prev_log_index, &entries.entries)
        };

        if !success {
            return AppendEntriesResponse {
                term: self.state.current_term,
                success: false,
                match_index: self.state.log.last_index(),
                conflict_term: None,
                conflict_index: None,
            };
        }

        if !entries.entries.is_empty() {
            let last_entry = entries.entries.last().unwrap();
            let _ = self.storage.save_log_entry(self.state.log.last_index(), last_entry);
        }

        if entries.leader_commit > self.state.log.commit_index() {
            let new_commit = std::cmp::min(entries.leader_commit, self.state.log.last_index());
            self.state.log.commit(new_commit);
            let applied = self.state.log.apply_committed();
            for entry in applied {
                log::debug!("Applying entry with term {}", entry.term);
            }
        }

        AppendEntriesResponse {
            term: self.state.current_term,
            success: true,
            match_index: self.state.log.last_index(),
            conflict_term: None,
            conflict_index: None,
        }
    }

    /// Handle RequestVote RPC
    async fn handle_request_vote(
        &mut self,
        request: RequestVote,
    ) -> RequestVoteResponse {
        if request.term < self.state.current_term {
            return RequestVoteResponse {
                term: self.state.current_term,
                vote_granted: false,
            };
        }

        if request.term > self.state.current_term {
            self.state.update_term(request.term);
            self.state.become_follower(Some(request.candidate_id.clone()));
            let _ = self.storage.save_current_term(self.state.current_term);
            let _ = self.storage.save_voted_for("");
        }

        let can_vote = self.state.voted_for.is_empty()
            || self.state.voted_for == request.candidate_id;

        let log_ok = self.state.is_log_up_to_date(request.last_log_index, request.last_log_term);

        let vote_granted = can_vote && log_ok;

        if vote_granted {
            self.state.vote_for(&request.candidate_id);
            let _ = self.storage.save_voted_for(&self.state.voted_for);
            self.state.last_heartbeat = Instant::now();
        }

        RequestVoteResponse {
            term: self.state.current_term,
            vote_granted,
        }
    }

    /// Handle propose command (client request)
    async fn handle_propose(&mut self, command: Vec<u8>) -> Result<u64, String> {
        if self.state.role != RaftRole::Leader {
            if let Some(leader) = &self.state.current_leader {
                return Err(format!("Not leader. Current leader: {}", leader));
            } else {
                return Err("No leader elected".to_string());
            }
        }

        let entry = LogEntry {
            term: self.state.current_term,
            command,
        };

        let index = self.state.log.last_index() + 1;
        self.state.log.append(entry.clone());

        let _ = self.storage.save_log_entry(index, &entry);
        self.send_heartbeats().await;

        Ok(index)
    }

    /// Handle incoming commands
    async fn handle_command(&mut self, cmd: Command) {
        match cmd {
            Command::Propose(command) => {
                let result = self.handle_propose(command).await;
                match result {
                    Ok(idx) => log::info!("Command proposed at index {}", idx),
                    Err(e) => log::error!("Failed to propose command: {}", e),
                }
            }
            Command::AppendEntries { entries, response_tx } => {
                let response = self.handle_append_entries(entries).await;
                let _ = response_tx.send(response);
            }
            Command::RequestVote { request, response_tx } => {
                let response = self.handle_request_vote(request).await;
                let _ = response_tx.send(response);
            }
            Command::GetLeader { response_tx } => {
                let _ = response_tx.send(self.state.current_leader.clone());
            }
            Command::GetStatus { response_tx } => {
                let status = RaftStatus {
                    role: self.state.role,
                    term: self.state.current_term,
                    current_leader: self.state.current_leader.clone(),
                    commit_index: self.state.log.commit_index(),
                    applied_index: self.state.log.applied_index(),
                    last_log_index: self.state.log.last_index(),
                    last_log_term: self.state.log.last_term(),
                };
                let _ = response_tx.send(status);
            }
        }
    }
}
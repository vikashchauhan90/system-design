use crate::message::LogEntry;
use super::storage::Storage;

/// In-memory storage (for testing/development)
pub struct InMemoryStorage {
    current_term: u64,
    voted_for: String,
    log: Vec<LogEntry>,
}

impl InMemoryStorage {
    pub fn new() -> Self {
        Self {
            current_term: 0,
            voted_for: String::new(),
            log: Vec::new(),
        }
    }
}

impl Default for InMemoryStorage {
    fn default() -> Self {
        Self::new()
    }
}

impl Storage for InMemoryStorage {
    fn save_current_term(&self, term: u64) -> Result<(), String> {
        // In-memory doesn't need persistence for this demo
        Ok(())
    }

    fn load_current_term(&self) -> Result<u64, String> {
        Ok(self.current_term)
    }

    fn save_voted_for(&self, voted_for: &str) -> Result<(), String> {
        Ok(())
    }

    fn load_voted_for(&self) -> Result<String, String> {
        Ok(self.voted_for.clone())
    }

    fn save_log_entry(&self, _index: u64, _entry: &LogEntry) -> Result<(), String> {
        Ok(())
    }

    fn load_log_entries(&self) -> Result<Vec<LogEntry>, String> {
        Ok(self.log.clone())
    }
}
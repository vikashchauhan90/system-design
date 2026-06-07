use crate::message::LogEntry;
use super::storage::Storage;
use std::fs;
use std::io;
use std::path::{Path, PathBuf};

/// File-based persistent storage
pub struct FileStorage {
    path: PathBuf,
    current_term: u64,
    voted_for: String,
    log: Vec<LogEntry>,
}

impl FileStorage {
    pub fn new<P: AsRef<Path>>(path: P) -> Result<Self, io::Error> {
        let path = path.as_ref().to_path_buf();
        fs::create_dir_all(&path)?;

        let  storage = Self {
            path,
            current_term: 0,
            voted_for: String::new(),
            log: Vec::new(),
        };

        // Load existing data
        let _ = storage.load_current_term();
        let _ = storage.load_voted_for();
        let _ = storage.load_log_entries();

        Ok(storage)
    }

    fn term_file(&self) -> PathBuf {
        self.path.join("term")
    }

    fn vote_file(&self) -> PathBuf {
        self.path.join("vote")
    }

    fn log_file(&self) -> PathBuf {
        self.path.join("log.bin")
    }
}

impl Storage for FileStorage {
    fn save_current_term(&self, term: u64) -> Result<(), String> {
        fs::write(self.term_file(), term.to_string().as_bytes())
            .map_err(|e| format!("Failed to save term: {}", e))
    }

    fn load_current_term(&self) -> Result<u64, String> {
        match fs::read_to_string(self.term_file()) {
            Ok(content) => content.parse().map_err(|e| format!("Parse error: {}", e)),
            Err(_) => Ok(0),
        }
    }

    fn save_voted_for(&self, voted_for: &str) -> Result<(), String> {
        fs::write(self.vote_file(), voted_for.as_bytes())
            .map_err(|e| format!("Failed to save vote: {}", e))
    }

    fn load_voted_for(&self) -> Result<String, String> {
        match fs::read_to_string(self.vote_file()) {
            Ok(content) => Ok(content),
            Err(_) => Ok(String::new()),
        }
    }

    fn save_log_entry(&self, index: u64, entry: &LogEntry) -> Result<(), String> {
        // For simplicity, we'll rewrite the entire log each time
        // In production, use an append-only approach
        let entries = self.load_log_entries()?;
        let mut updated = entries;

        if index as usize <= updated.len() {
            updated[index as usize - 1] = entry.clone();
        } else {
            updated.push(entry.clone());
        }

        let data = bincode::serialize(&updated).map_err(|e| format!("Serialization error: {}", e))?;
        fs::write(self.log_file(), data).map_err(|e| format!("Write error: {}", e))
    }

    fn load_log_entries(&self) -> Result<Vec<LogEntry>, String> {
        match fs::read(self.log_file()) {
            Ok(data) => bincode::deserialize(&data).map_err(|e| format!("Deserialization error: {}", e)),
            Err(_) => Ok(Vec::new()),
        }
    }
}
# New Relic Logs: Complete Log Management & Intelligence Reference

## Document Overview

This document provides a comprehensive analysis of New Relic's log management architecture, processing algorithms, and intelligent log analysis concepts. New Relic is a cloud-based observability platform that treats logs as part of a unified telemetry data model alongside metrics, traces, and events. Unlike traditional log management tools that focus on basic collection and search, New Relic's approach emphasizes intelligence, automation, and context-rich analysis. This document covers the core processing pipeline, data storage strategies, parsing mechanisms, and governance patterns that power New Relic's logging solution.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Log Ingestion & Processing Pipeline](#log-ingestion--processing-pipeline)
3. [Data Parsing & Structure Extraction](#data-parsing--structure-extraction)
4. [Storage Architecture & Blob Management](#storage-architecture--blob-management)
5. [Query & Analysis Engine (NRQL)](#query--analysis-engine-nrql)
6. [Pipeline Control Gateway (PCG)](#pipeline-control-gateway-pcg)
7. [Log Governance & Management as Code](#log-governance--management-as-code)
8. [Federated Logs & Federated Queries](#federated-logs--federated-queries)
9. [Alerting & Aggregation Methods](#alerting--aggregation-methods)
10. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Unified Telemetry Data Model

**Purpose**: Treat logs as a first-class citizen alongside metrics, traces, and events in a single data model

**Core Telemetry Types**:

| Type | Description | Use Case |
|------|-------------|----------|
| **Logs** | Text-based records of application activity | Debugging, error tracking, security auditing |
| **Metrics** | Numerical measurements (CPU, memory, request rate) | Performance monitoring, capacity planning |
| **Traces** | Distributed request flows across microservices | Latency analysis, dependency mapping |
| **Events** | Discrete occurrences (deployments, user signups) | Business analytics, change tracking |

**The Unified Data Platform Advantage**:
- **Single query language (NRQL)** across all telemetry types
- **Context preservation**: View logs in context with metrics and traces from the same time window
- **No silos**: Query log data alongside performance data without tool switching

**Query Example Across Telemetry Types**:
```sql
-- Find slow requests AND their error logs
SELECT timestamp, duration, errorLogs 
FROM Transaction 
WHERE appName = 'OrderService' 
  AND duration > 5000 
SINCE 10 minutes ago
```

### 2. Pipeline-Based Processing Architecture

**Purpose**: Process telemetry data through configurable stages before storage

**Pipeline Stages**:

```
Telemetry Source (Agent/API)
         │
         ▼
┌─────────────────┐
│    Receiver     │ ← OTLP/HTTP/HTTPS ingress
│   (Ingestion)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Processor     │ ← Transform, Filter, Sample
│   (Logic Layer) │   (OTTL-based processing)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Exporter     │ ← Export to New Relic Cloud
│   (Delivery)    │
└─────────────────┘
```

**Processing Characteristics**:
- **Declarative YAML configuration** defines processing rules
- **Processors are telemetry-type specific** (logs, metrics, traces flow separately)
- **Supports OpenTelemetry Transformation Language (OTTL)** for complex transformations
- **Can run locally** via Pipeline Control Gateway for data sovereignty

---

## Log Ingestion & Processing Pipeline

### 3. Agent-Based Collection

**Purpose**: Collect logs from application environments with minimal configuration

**Collection Methods**:

| Method | Description | Use Case |
|--------|-------------|----------|
| **New Relic Infrastructure Agent** | Installed on hosts, monitors system logs | System and application logs on VMs/containers |
| **APM Agent** | Collects logs from application frameworks | Application-level logs (Java, .NET, Node.js, Python, Ruby)  |
| **OpenTelemetry Integration** | Vendor-neutral instrumentation | Standardized collection across polyglot environments  |
| **Fluentd/Fluent Bit** | Log forwarder integration | Existing log collection pipelines |
| **API Direct Ingestion** | HTTP/HTTPS endpoints | Custom or legacy applications |

**Context Enrichment**:
- Automatic addition of `service.name`, `host.name`, `environment` attributes
- Correlation with trace IDs for distributed tracing integration
- Linking to infrastructure metrics from same source

### 4. Pipeline Control Gateway (PCG)

**Purpose**: Local data processing before transmission to New Relic cloud

**What PCG Provides**:
- **Data sovereignty**: Process and filter data locally; send only what's needed
- **Cost control**: Sample or drop low-value data before ingestion
- **Signal improvement**: Remove noise (debug logs, health checks) at the source
- **Schema normalization**: Transform disparate logging formats into standard schemas

**PCG Architecture**:

```
Your Infrastructure                     New Relic Cloud
┌─────────────────────┐                ┌─────────────────┐
│  Pipeline Control   │                │                 │
│     Gateway         │                │   NRDB (Cloud    │
│  ┌───────────────┐  │  Processed     │    Storage)      │
│  │ Transform     │──┼───────────────►│                 │
│  │ Filter        │  │  Telemetry     │                 │
│  │ Sample        │  │                │                 │
│  └───────────────┘  │                │                 │
└─────────────────────┘                └─────────────────┘
```

**Real-World Use Cases**:

| Problem | PCG Solution | Result |
|---------|--------------|--------|
| Debug logs flooding system | Filter out all DEBUG logs from production environments | 85% data volume reduction |
| High observability bill ($80k/month) | Sample 95% of INFO logs, keep 100% of ERROR/WARN | Bill reduced to $12k/month |
| Inconsistent log formats across services | Normalize attribute names (`severity=ERROR`, `level=error` → `severity.text=ERROR`) | Unified queries across all services |

**Processor Types**:

| Processor | Function | Example |
|-----------|----------|---------|
| **Transform** | Modify data using OTTL | `set(resource.attributes["source.type"],"otlp")` |
| **Filter** | Drop records matching conditions | Drop `severity_text == "INFO"` logs |
| **Sample** | Reduce volume intelligently | Keep 100% of errors, 5% of successes |

---

## Data Parsing & Structure Extraction

### 5. No-Code Parsing

**Purpose**: Transform raw unstructured logs into queryable structured data without writing code

**The Parsing Problem**:
- Raw logs are noisy and unstructured
- Traditional parsing requires complex regex patterns
- "Expert Tax": Only senior engineers could parse logs effectively
- Fragile parsers break when log formats change

**Visual Attribute Extraction**:
Instead of writing regex patterns, engineers can highlight log data directly in the UI:

**Traditional Regex Approach**:
```regex
(?<=latency=)\d+
```

**No-Code Approach**:
1. Open the log stream
2. Highlight the latency value in the log message
3. Name the attribute "service_latency"
4. Click save → parsing rule generated automatically

**Democratization Impact**:
- Junior engineers can create parsing rules without DevOps intervention
- Self-service observability culture
- Faster iteration on log configurations

### 6. Automated Parsing Rules

**Purpose**: Automatically detect and index structured data formats

**Supported Formats**:
| Format | Detection | Behavior |
|--------|-----------|----------|
| JSON | Automatic | All fields become queryable attributes |
| CSV | Automatic | Column headers become attributes |
| Key-Value pairs | Automatic | Keys become attribute names |
| Custom formats | Via Grok patterns | Structured extraction |

**Schema-on-Read Model**:
- No need to pre-define schema before ingestion
- New fields in JSON logs are instantly queryable
- No re-indexing or configuration updates required

**Example**:
```json
// Raw log sent to New Relic
{"timestamp":"2024-01-01T10:00:00Z","user_id":"123","action":"login"}

// Fields automatically become queryable:
SELECT count(*) FROM Log WHERE user_id = '123'
```

### 7. Grok Pattern Parsing

**Purpose**: Extract structured fields from unstructured logs using pattern matching

**Grok Pattern Example**:
```
# Raw log line
2024-01-01 10:00:00 INFO [session_abc123] User: 123 - Login successful

# Grok pattern
%{TIMESTAMP_ISO8601:timestamp} %{WORD:level} \[%{DATA:session_id}\] User: %{DATA:user_id} - %{GREEDYDATA:message}

# Extracted attributes:
timestamp: "2024-01-01 10:00:00"
level: "INFO"
session_id: "session_abc123"
user_id: "123"
message: "Login successful"
```

**Common Grok Patterns**:

| Pattern | Matches | Example |
|---------|---------|---------|
| `%{TIMESTAMP_ISO8601}` | ISO 8601 timestamps | `2024-01-01T10:00:00Z` |
| `%{WORD}` | Alphanumeric word | `INFO`, `ERROR` |
| `%{DATA}` | Any characters | `session_abc123` |
| `%{NUMBER}` | Numeric values | `123`, `45.6` |
| `%{IP}` | IPv4 or IPv6 address | `192.168.1.1` |
| `%{GREEDYDATA}` | Everything remaining | `Login successful` |

### 8. Real-Time Rule Validation

**Purpose**: Test parsing rules against live data before saving

**Validation Process**:
1. Create or modify parsing rule in UI
2. System shows real-time preview using current log stream
3. Engineer sees extracted attributes immediately
4. Save rule only when verification passes

**Safety Benefits**:
- No "deploy and pray" - test before production
- Catch edge cases before they break data ingestion
- Immediate feedback on rule correctness

---

## Storage Architecture & Blob Management

### 9. Long Log (BLOB) Storage

**Purpose**: Store log messages longer than 4,094 characters

**The 4,094 Character Limit**:
New Relic's NRDB database has a field limit of 4,094 characters. Longer values require special handling.

**Three-Part Storage Structure**:

| Section | Storage Location | Maximum Size | Searchable? |
|---------|-----------------|--------------|-------------|
| First 4,094 chars | `message` field | 4,094 chars | Yes |
| Next 128,000 UTF-8 bytes | `newrelic.ext.message` (BLOB) | 128,000 bytes (~32k-128k chars) | No |
| Remaining characters | Dropped | N/A | N/A |

**Storage Example**:
```
Original message: 150,000 character log
├── message: "First 4,094 characters as string" ← Searchable
├── newrelic.ext.message: "Next 128,000 bytes as base64 blob" ← Not searchable
└── Remaining: Dropped (not saved)
```

**UTF-8 Variable Length Impact**:
- UTF-8 encodes characters in 1-4 bytes
- 128,000 bytes = 32,000 to 128,000 actual characters (depending on character set)
- ASCII characters: ~128,000 chars
- CJK characters (3 bytes): ~42,666 chars

### 10. BLOB Querying & Retrieval

**Purpose**: Access long log data that exceeds field limits

**Querying BLOB Data**:
```sql
-- Basic log query
SELECT * FROM Log

-- Expand BLOB data in results (note backticks)
SELECT message, 
       blob(`newrelic.ext.message`),
       blob(`newrelic.ext.another-attribute`)
FROM Log
```

**Result Structure**:
```json
{
  "message": "<first 4,094 characters>",
  "newrelic.ext.message": "<next 128,000 bytes as Base64>",
  "another-attribute": "<first 4,094 characters>",
  "newrelic.ext.another-attribute": "<next 128,000 bytes as Base64>"
}
```

**BLOB Viewing in UI**:
- Log detail view automatically stitches original value together
- No manual decoding needed for visualization

**Manual BLOB Processing** (when using NRQL directly):
1. Decode Base64 of `newrelic.ext.` attribute
2. Convert UTF-8 bytes to string
3. Append to main attribute's first 4,094 characters

**Query Limitations**:
- BLOB query results limited to 20 records 
- Cannot search or alert on BLOB content 
- BLOB records retained for 1 month 

### 11. Data Partitioning for Performance

**Purpose**: Route logs to specific partitions for faster querying

**Log Partition Concept**:
- Physical separation of log data by partition
- Queries can target specific partitions
- Enables faster scans for frequent query patterns

**Partition Rule Configuration** (Terraform):
```terraform
resource "newrelic_data_partition_rule" "production_logs" {
  description = "Route all production app logs to high-performance partition"
  nrql = "environment = 'production'"
  enabled = true
  target_data_partition = "Log_Production"
  retention_policy = "STANDARD"
}
```

**Use Cases**:
| Partition Type | Data | Benefits |
|----------------|------|----------|
| Production partition | High-importance app logs | Reserved capacity, faster queries |
| Audit partition | Compliance logs | Extended retention, separate access controls |
| Debug partition | Low-value development logs | Cheaper storage, slower query performance |

---

## Query & Analysis Engine (NRQL)

### 12. NRQL (New Relic Query Language)

**Purpose**: Query log data alongside all telemetry types

**NRQL Structure**:
```sql
SELECT function(attribute) [AS 'label']
FROM dataType [SINCE | UNTIL | COMPARE WITH]
[WHERE condition]
[FACET attribute]
[SINCE time period]
```

**Common Log Queries**:

| Query | Purpose |
|-------|---------|
| `SELECT * FROM Log WHERE level = 'ERROR' SINCE 1 hour ago` | Get recent errors |
| `SELECT count(*) FROM Log FACET service.name` | Error count by service |
| `SELECT uniqueCount(user_id) FROM Log WHERE action = 'login'` | Unique user logins |
| `SELECT message, timestamp, service.name FROM Log WHERE status_code >= 500` | Server error details |

### 13. IDE-Integrated Querying (CodeStream)

**Purpose**: Query logs directly from development environment

**CodeStream Capabilities**:
- Execute NRQL queries from within VS Code or JetBrains IDE
- Log search directly from any line of code
- Automatic string extraction from cursor position
- Export results as CSV or JSON

**Workflow**:
1. Developer identifies suspect line of code
2. Right-click → Search logs
3. IDE automatically extracts service name, method, or string
4. Results appear without leaving IDE
5. Export findings or create dashboards

**Faster Root Cause Analysis**:
- No context switching between IDE and observability tool
- Direct correlation between code and production behavior

---

## Pipeline Control Gateway (PCG)

### 14. OpenTelemetry-Based Processing

**Purpose**: Vendor-neutral data pipeline for telemetry processing

**What is OTTL?**:
OpenTelemetry Transformation Language (OTTL) is a powerful, vendor-neutral language for writing transformation statements and boolean conditions.

**OTTL Functions in PCG**:

| Category | Functions | Purpose |
|----------|-----------|---------|
| **Attribute manipulation** | `set()`, `delete_key()`, `rename_key()` | Modify telemetry fields |
| **String operations** | `replace_pattern()` | Regex-based string replacement |
| **Value hashing** | `hash()` | Anonymize high-cardinality fields |
| **Conditional logic** | `IsMatch()`, `startsWith()` | Filtering conditions |

**Transform Example**:
```yaml
transform/Logs:
  config:
    log_statements:
      - context: log
        conditions:
          - resource.attributes["service.name"] == "otlp-java-test-service"
        statements:
          - set(resource.attributes["source.type"],"otlp")
```

### 15. YAML-Based Pipeline Configuration

**Purpose**: Declarative configuration for telemetry processing

**Pipeline Structure**:
```yaml
version: 2.0.0
autoscaling:
  minReplicas: 6
  maxReplicas: 10
  targetCPUUtilizationPercentage: 60

configuration:
  simplified/v1:
    steps:
      receive_logs:
        description: Receive logs from OTLP and New Relic agents
        output:
          - probabilistic_sampler/Logs
      
      probabilistic_sampler/Logs:
        output:
          - filter/Logs
        config:
          global_sampling_percentage: 100
          conditionalSamplingRules:
            - name: sample ruby test service
              sampling_percentage: 70
              condition: resource.attributes["service.name"] == "ruby-test-service"
      
      filter/Logs:
        output:
          - transform/Logs
        config:
          logs:
            rules:
              - name: drop INFO logs
                value: log.severity_text == "INFO"
      
      transform/Logs:
        output:
          - nrexporter/newrelic
        config:
          log_statements:
            - context: log
              statements:
                - set(resource.attributes["team"], "platform")
```

### 16. Telemetry-Specific Processing Isolation

**Purpose**: Ensure high-volume data doesn't interfere with critical telemetry

**Separate Pipelines**:
- **Metrics pipeline**: High-volume numeric data
- **Logs and Events pipeline**: Text-based data
- **Traces pipeline**: Distributed request spans

**Why Isolation Matters**:
| Scenario | Without Isolation | With Isolation |
|----------|------------------|----------------|
| Log flood (10k logs/sec) | Traces delayed, metrics dropped | Traces unaffected, logs processed separately |
| High trace volume | Log queries slow | Logs still responsive |
| Metric burst | All telemetry impacted | Metrics handled independently |

### 17. Signal Quality Improvement

**Purpose**: Reduce noise and improve the signal-to-noise ratio in telemetry data

**The Signal-to-Noise Problem**:
- Debug logs, health checks, and test environment data flood observability systems
- Engineers waste time searching through irrelevant data
- Critical anomalies hidden in noise

**PCG Noise Reduction Strategies**:

| Strategy | Implementation | Impact |
|----------|----------------|--------|
| Drop debug logs | Filter `severity_text == "DEBUG"` | 40-60% volume reduction |
| Sample low-priority data | Keep 5% of INFO logs | 95% reduction for routine logs |
| Drop test environments | Filter `environment == "test"` | Remove non-production noise |
| Remove health checks | Filter `http.url matches ".*health.*"` | Eliminate systematic noise |

**Business Value Prioritization**:
- Keep 100% of ERROR and WARN logs
- Keep 100% of business-critical transaction logs
- Aggressively sample high-volume, low-value telemetry

---

## Log Governance & Management as Code

### 18. Terraform-Based Log Management

**Purpose**: Automate log configuration across hundreds of services

**The Manual Configuration Problem**:
- Maintaining log parsing rules for 100+ services manually is error-prone
- Configuration drift between environments
- Operational toil for platform teams

**Terraform Resources for Log Management**:

| Resource | Purpose |
|----------|---------|
| `newrelic_log_parsing_rule` | Extract structured attributes with Grok |
| `newrelic_data_partition_rule` | Route logs to specific partitions |
| `newrelic_obfuscation_expression` | Define pattern for sensitive data |
| `newrelic_obfuscation_rule` | Apply masking/hashing to sensitive fields |
| `newrelic_pipeline_cloud_rule` | Filter logs at ingest |

### 19. Declarative Log Parsing

**Purpose**: Define parsing rules as code with Terraform

**Example Grok Parsing Rule**:
```terraform
resource "newrelic_log_parsing_rule" "app_parsing" {
  name = "Enrich App Logs"
  description = "Extracts session and user IDs using Grok"
  nrql = "SELECT * FROM Log WHERE service_name = 'checkout-api'"
  enabled = true
  lucene = "logtype:app_messages"
  
  grok = "%{TIMESTAMP_ISO8601:timestamp} %{WORD:level} \\[%{DATA:session_id}\\] User: %{DATA:user_id} - %{GREEDYDATA:message}"
}
```

**Deployment Pattern**:
- Commit parsing rules to Git repository
- CI/CD pipeline applies to staging environment for testing
- After validation, promote to production
- Version control provides audit trail and rollback capability

### 20. Obfuscation & Data Privacy

**Purpose**: Automatically mask or hash sensitive data before storage

**The Privacy Problem**:
- Developers may accidentally log PII (credit cards, SSNs, emails)
- Manual scrubbing is error-prone and incomplete
- Compliance regulations require data protection by default

**Obfuscation Architecture**:

```
Step 1: Define pattern (Obfuscation Expression)
Step 2: Apply to logs (Obfuscation Rule)
Step 3: Choose action (MASK or HASH)
```

**Credit Card Obfuscation Example**:
```terraform
# Define pattern once
resource "newrelic_obfuscation_expression" "credit_card" {
  name = "creditCardPattern"
  regex = "(?:4[0-9]{12}(?:[0-9]{3})?|[25][1-7][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\\d{3})\\d{11})"
}

# Apply globally as safety net
resource "newrelic_obfuscation_rule" "global_masking" {
  name = "Global PII Masking"
  filter = "message LIKE '%card%'"
  enabled = true
  
  actions {
    attributes = ["message"]
    expression_id = newrelic_obfuscation_expression.credit_card.id
    method = "MASK"  # Replace with ****, or "HASH" for consistent pseudonym
  }
}
```

**Obfuscation Methods**:
| Method | Behavior | Use Case |
|--------|----------|----------|
| `MASK` | Replace with `****` | Hide actual values, no analysis needed |
| `HASH` | Replace with consistent hash | Detect repeat occurrences without exposing data |

### 21. Automated Pipeline Rules

**Purpose**: Apply ingest filtering across entire fleet using infrastructure as code

**Multi-Service Pattern**:
```terraform
# Define services once
locals {
  target_services = ["checkout-api", "inventory-service", "payment-gateway"]
}

# Create filter rules for each service using for_each
resource "newrelic_pipeline_cloud_rule" "service_gatekeeper" {
  for_each = toset(local.target_services)
  
  name = "Filter-High-Volume-${each.key}"
  description = "Drop non-production logs for ${each.key}"
  nrql = "DELETE FROM Log WHERE serviceName = '${each.key}' AND environment != 'production'"
}
```

**Benefits of Management as Code**:
- **Consistency**: Same rules applied to all services
- **Auditability**: Git history shows who changed what and when
- **Testing**: Rules validated in CI before production
- **Self-service**: Developers can add services via pull request

---

## Federated Logs & Federated Queries

### 22. Federated Logs Architecture

**Purpose**: Query logs stored in customer-owned storage without data movement

**The Compliance Dilemma**:
- Data sovereignty laws require logs to stay in specific jurisdictions
- Traditional observability requires moving data to vendor cloud
- Teams forced to choose between compliance and visibility

**Federated Logs Solution**:
- Logs remain in customer's S3 buckets within their VPC
- Query engine runs locally via Pipeline Control Gateway
- Only query results (not raw logs) cross network boundaries

**Architecture Diagram**:
```
Customer VPC                         New Relic Cloud
┌─────────────────────┐             ┌─────────────────┐
│  S3 Bucket (Logs)   │             │                 │
│         │           │             │    Unified UI   │
│         ▼           │  Query      │                 │
│  Pipeline Control   │◄───────────►│   Query Results │
│     Gateway         │  Results    │   (not raw logs)│
└─────────────────────┘             └─────────────────┘
```

**Key Features**:
| Feature | Description |
|---------|-------------|
| **Local Data, Global Observability** | Process logs locally, view in unified UI |
| **Residency-by-Design** | Raw data never leaves customer environment |
| **No Data Egress** | Query results only, not bulk log export |
| **Unified Query Syntax** | Same NRQL across federated and cloud logs |

### 23. Federated Query Processing

**Purpose**: Execute queries across distributed data sources

**Query Flow**:
1. User issues NRQL query in New Relic UI
2. Query routed to appropriate PCG in customer VPC
3. PCG executes query against local S3 storage
4. Results (aggregated, filtered) returned to New Relic cloud
5. Results displayed alongside other telemetry

**Supported Data Sources**:
- Amazon S3 buckets within customer domain or VPC
- Any OpenTelemetry-compatible storage
- Future: Additional cloud providers

**Compliance Benefits**:
- Logs never leave jurisdiction
- No bulk data replication to third-party clouds
- Meets finance, healthcare, government requirements

---

## Alerting & Aggregation Methods

### 24. Event Flow Aggregation

**Purpose**: Real-time alerting based on data timestamp progression

**How Event Flow Works**:
- Data aggregated based on point timestamps (not ingestion time)
- Window closes when timestamp 2 minutes later than last data point arrives
- Default aggregation method for most use cases

**Event Flow Example**:
```
Timeline:
12:00:00 - Data point with timestamp 12:00:00 → Window opens
12:00:30 - Data point with timestamp 12:00:30 → Window stays open
12:01:00 - Data point with timestamp 12:01:00 → Window stays open
12:02:05 - Data point with timestamp 12:02:05 → Window closes, data evaluated
```

**Best For**:
- Agent data (frequent, consistent)
- Infrastructure monitoring
- Streaming data from third-party services
- AWS Metric Streams (polling NOT polling)

**When NOT to Use**:
- Points arrive >65 minutes apart (use Event Timer)
- Highly irregular, sporadic data
- Polled cloud integrations

### 25. Event Timer Aggregation

**Purpose**: Alert on sporadic, infrequent events

**How Event Timer Works**:
- Timer counts down when data point arrives
- Timer resets whenever new data point arrives
- When timer reaches zero, window closes and data aggregated

**Event Timer Example**:
```
Scenario: Error events arriving sporadically
Error 1 arrives at 12:00:00 → Timer starts (e.g., 5 minutes)
Error 2 arrives at 12:00:30 → Timer resets to 5 minutes
Error 3 arrives at 12:01:00 → Timer resets to 5 minutes
(No more errors for 5 minutes)
12:06:00 - Timer reaches zero → Window closes, evaluate 3 errors
```

**Best For**:
- Error events (sporadic by nature)
- Cloud integration polling (GCP, Azure, AWS polling methods)
- Usage data (New Relic usage events)
- Queries returning sparse data

**Key Difference from Event Flow**:
| Aspect | Event Flow | Event Timer |
|--------|------------|-------------|
| Window trigger | Timestamp progression | Timer countdown |
| Data pattern | Consistent, frequent | Sporadic, unpredictable |
| Reset behavior | N/A | Reset on each data point |
| Risk | Premature closure for late data | Infinite window for continuous data |

### 26. Cadence Aggregation

**Purpose**: Legacy method for time-shifted data

**How Cadence Works**:
- Fixed time intervals based on internal wall clock
- Independent of data timestamps
- Closes windows at predictable times (e.g., every hour at :00)

**When to Use Cadence**:
ONLY when data susceptible to clock skew and producer can't be corrected.

**Example Use Case**: Browser-based `PageAction` events
- Timestamp comes from user's device clock
- User's clock may be hours off
- Cadence prevents skewed timestamp from breaking alert windows

**Recommendation**: Use Event Flow or Event Timer instead for most cases 

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DATA SOURCES                                         │
│                                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐                     │
│  │  Host    │  │  APM     │  │  K8s     │  │  Cloud   │                     │
│  │  Agent   │  │  Agent   │  │  Cluster │  │  Storage │                     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘                     │
│       │             │             │             │                           │
└───────┼─────────────┼─────────────┼─────────────┼───────────────────────────┘
        │             │             │             │
        ▼             ▼             ▼             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         INGESTION LAYER                                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    INGEST API / OTLP ENDPOINTS                        │    │
│  │  • HTTP/HTTPS ingress                                                │    │
│  │  • Authentication & rate limiting                                    │    │
│  │  • Protocol conversion (Fluentd, OpenTelemetry, NR agents)         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    PIPELINE CONTROL GATEWAY (Optional Local)                 │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Processors (OTTL-based):                                           │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │    │
│  │  │ Transform   │→ │   Filter    │→ │   Sample    │                 │    │
│  │  │ • Normalize │  │ • Drop INFO │  │ • 5% keep   │                 │    │
│  │  │ • Add team  │  │ • Drop test │  │ • 100% err  │                 │    │
│  │  │ • Rename    │  │ • Drop hc   │  │             │                 │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PARSING & STRUCTURING                                │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Parsing Pipeline:                                                   │    │
│  │                                                                       │    │
│  │  Raw Log → ┌────────────────────────────────────────────────────┐   │    │
│  │            │ Auto-detect format (JSON/CSV/Grok)                 │   │    │
│  │            │ Apply no-code parsing rules (visual extraction)    │   │    │
│  │            │ Execute Grok patterns for complex extraction       │   │    │
│  │            │ Add/enrich attributes (team, environment, region)  │   │    │
│  │            │ Apply obfuscation rules (mask credit cards)        │   │    │
│  │            └────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           STORAGE LAYER (NRDB)                               │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Data Partitioning                                 │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │    │
│  │  │ Production  │  │  Audit      │  │  Debug      │                  │    │
│  │  │ Partition   │  │ Partition   │  │ Partition   │                  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    BLOB Storage (Long Logs)                          │    │
│  │                                                                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ message (first 4,094 chars) ← Searchable                     │    │    │
│  │  │ newrelic.ext.message (next 128KB as base64) ← Not searchable│    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         QUERY & ANALYTICS ENGINE (NRQL)                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Query Processing:                                                   │    │
│  │                                                                       │    │
│  │  NRQL Query → Parse → Plan → Execute → Aggregate → Return           │    │
│  │                                                                       │    │
│  │  Supported Operations:                                               │    │
│  │  • SELECT, FROM, WHERE, FACET, SINCE, UNTIL, COMPARE WITH          │    │
│  │  • Aggregations: count(), uniqueCount(), average(), min(), max()   │    │
│  │  • Functions: blob() for long logs, timestamps, string ops         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ALERTING & NOTIFICATION                              │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Aggregation Methods                               │    │
│  │                                                                       │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │    │
│  │  │Event Flow   │  │Event Timer  │  │ Cadence     │                  │    │
│  │  │(timestamp-  │  │(timer-based)│  │(fixed time) │                  │    │
│  │  │ based)      │  │             │  │             │                  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │    │
│  │                                                                       │    │
│  │  NRQL condition → Aggregate window → Evaluate threshold → Notify    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      FEDERATED LOGS (Customer VPC)                           │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Customer S3 Bucket (Logs remain local)                              │    │
│  │         │                                                            │    │
│  │         ▼                                                            │    │
│  │  Pipeline Control Gateway (executes query locally)                  │    │
│  │         │                                                            │    │
│  │         ▼ (query results only)                                       │    │
│  │  New Relic Cloud (unified UI)                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm Summary Table

| # | Algorithm/Concept | Primary Purpose | New Relic Component |
|---|------------------|-----------------|---------------------|
| 1 | Unified Telemetry Data Model | Single data model for logs, metrics, traces, events | NRDB |
| 2 | Pipeline-Based Processing | Configurable data transformation stages | PCG |
| 3 | Agent-Based Collection | Local data capture from applications | Infrastructure Agent, APM Agent |
| 4 | No-Code Parsing | Visual log structuring without regex | Log UI, Visual Attribute Extraction |
| 5 | Grok Pattern Parsing | Structured extraction from unstructured logs | Parsing Rules |
| 6 | Real-Time Rule Validation | Test parsing against live data | Log Management UI |
| 7 | BLOB Storage (4,094+128KB) | Long log message storage | NRDB message fields |
| 8 | Base64 BLOB Encoding | Storage format for long logs | `newrelic.ext.message` |
| 9 | Data Partitioning | Physical log separation for performance | Data Partition Rules |
| 10 | NRQL Query Engine | Unified query language across telemetry | Query API |
| 11 | OTTL Transformations | Vendor-neutral telemetry processing | PCG Transform Processors |
| 12 | Conditional Sampling | Percentage-based data reduction | PCG Sampling Processors |
| 13 | Declarative YAML Config | Pipeline configuration as code | PCG Configuration |
| 14 | Terraform Log Management | Log governance as infrastructure | Terraform Provider |
| 15 | Obfuscation Expressions | Pattern definition for sensitive data | Obfuscation Expression |
| 16 | Obfuscation Rules | Apply masking/hashing to sensitive fields | Obfuscation Rule |
| 17 | Federated Logs | Query logs in customer storage | PCG, Federated Queries |
| 18 | Event Flow Aggregation | Timestamp-based alert aggregation | Alert Conditions |
| 19 | Event Timer Aggregation | Timer-based alert for sporadic data | Alert Conditions |
| 20 | Cadence Aggregation | Fixed-interval aggregation (legacy) | Alert Conditions |
| 21 | CodeStream IDE Integration | IDE-based log querying | CodeStream |
| 22 | Pipeline Cloud Rules | Ingest filtering via Terraform | Pipeline Cloud Rule |

---

## Configuration Reference

### Pipeline Control Gateway YAML Structure

```yaml
version: 2.0.0
autoscaling:
  minReplicas: 6
  maxReplicas: 10
  targetCPUUtilizationPercentage: 60

configuration:
  simplified/v1:
    steps:
      receive_logs:
        description: Receive logs from OTLP and New Relic sources
        output:
          - probabilistic_sampler/Logs
      
      probabilistic_sampler/Logs:
        output:
          - filter/Logs
        config:
          global_sampling_percentage: 100
          conditionalSamplingRules:
            - name: sample ruby test service
              sampling_percentage: 70
              condition: resource.attributes["service.name"] == "ruby-test-service"
      
      filter/Logs:
        output:
          - transform/Logs
        config:
          logs:
            rules:
              - name: drop INFO logs
                value: log.severity_text == "INFO"
      
      transform/Logs:
        output:
          - nrexporter/newrelic
        config:
          log_statements:
            - context: log
              statements:
                - set(resource.attributes["team"], "platform")

      nrexporter/newrelic:
        description: Export to New Relic
```

### Terraform Log Management Examples

**Parsing Rule**:
```terraform
resource "newrelic_log_parsing_rule" "app_parsing" {
  name = "Enrich App Logs"
  description = "Extracts session and user IDs"
  nrql = "SELECT * FROM Log WHERE service_name = 'checkout-api'"
  enabled = true
  grok = "%{TIMESTAMP_ISO8601:timestamp} %{WORD:level} \\[%{DATA:session_id}\\] User: %{DATA:user_id} - %{GREEDYDATA:message}"
}
```

**Data Partition Rule**:
```terraform
resource "newrelic_data_partition_rule" "production_logs" {
  description = "Route production logs to high-performance partition"
  nrql = "environment = 'production'"
  enabled = true
  target_data_partition = "Log_Production"
  retention_policy = "STANDARD"
}
```

**Obfuscation Expression**:
```terraform
resource "newrelic_obfuscation_expression" "credit_card" {
  name = "creditCardPattern"
  regex = "(?:4[0-9]{12}(?:[0-9]{3})?|[25][1-7][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13})"
}
```

**Obfuscation Rule**:
```terraform
resource "newrelic_obfuscation_rule" "global_masking" {
  name = "Global PII Masking"
  filter = "message LIKE '%card%'"
  enabled = true
  
  actions {
    attributes = ["message"]
    expression_id = newrelic_obfuscation_expression.credit_card.id
    method = "MASK"
  }
}
```

**Pipeline Cloud Rule**:
```terraform
resource "newrelic_pipeline_cloud_rule" "filter_non_prod" {
  name = "Filter-Non-Production"
  description = "Drop non-production logs"
  nrql = "DELETE FROM Log WHERE environment != 'production'"
}
```

### NRQL Query Examples

**Basic Queries**:
```sql
-- Get recent errors with user context
SELECT timestamp, message, user_id 
FROM Log 
WHERE level = 'ERROR' 
SINCE 1 hour ago

-- Error count by service
SELECT count(*) 
FROM Log 
WHERE level = 'ERROR' 
FACET service.name 
SINCE 24 hours ago

-- Long log content extraction
SELECT message, blob(`newrelic.ext.message`) 
FROM Log 
WHERE length(message) > 4000
```

**Alerting Queries**:
```sql
-- Alert on high error rate
SELECT count(*) 
FROM Log 
WHERE level = 'ERROR' 
FACET service.name 
SINCE 5 minutes ago
```

---

## Performance & Scale Characteristics

| Metric | Typical Value | Notes |
|--------|---------------|-------|
| Log ingestion rate | Unlimited (auto-scales) | Pay per GB ingested |
| Field limit | 4,094 chars | String fields (before BLOB) |
| BLOB limit | 128,000 UTF-8 bytes | Not searchable |
| Query result limit | 20 for BLOB queries | BLOB expansions limited |
| Data retention | Configurable (default 30 days) | Extended available |
| Partition query speed | 10-100x faster | Targeted partitions |
| PCG sampling granularity | 0-100% | Per-rule configuration |

---

## Comparison to Other Log Management Tools

| Feature | New Relic Logs | Splunk | Datadog | ELK Stack |
|---------|---------------|--------|---------|-----------|
| Unified data model | Yes (logs+metrics+traces) | No (separate indexes) | Yes | No (separate systems) |
| No-code parsing | Yes (visual extraction) | No (regex required) | Yes (limited) | No (Grok required) |
| Federated queries | Yes (S3) | No (must ingest) | No | No |
| Data sovereignty | Yes (PCG) | Yes (on-prem) | No | Yes (self-managed) |
| Terraform support | Extensive | Limited | Growing | Limited |
| BLOB storage | 4KB + 128KB | Configurable | 10KB limit | Configurable |
| OTLP native | Yes | No | Partial | Yes |
| Management as code | Yes (PCG YAML) | No | Partial | Yes (config files) |

---

## Conclusion

New Relic's log management philosophy emphasizes:

- **Intelligence over collection**: Focus on extracting value, not just storing data
- **Democratization over expertise**: No-code parsing makes logs accessible to all engineers
- **Governance as code**: Terraform and YAML configurations codify best practices
- **Data sovereignty by design**: Federated queries keep sensitive data local
- **Unified telemetry**: Logs in context with metrics and traces for faster root cause

Key innovations include:
- **No-code visual parsing**: Democratize log structuring without regex expertise
- **Pipeline Control Gateway**: Local data processing for sovereignty and cost control
- **Federated Logs**: Query logs in customer S3 without data movement
- **Terraform log management**: Treat logging configuration as infrastructure
- **Real-time rule validation**: Test parsing against live data before saving
- **BLOB architecture**: Efficient storage for long log messages (4KB + 128KB model)
- **Multiple aggregation methods**: Event Flow, Event Timer, and Cadence for varied data patterns

This combination of algorithms and patterns makes New Relic Logs suitable for:
- **Modern cloud-native applications**: Unified observability with logs, metrics, and traces
- **Compliance-sensitive industries**: Federated queries keep data in jurisdiction
- **High-velocity engineering teams**: Self-service log structuring via no-code parsing
- **Large-scale microservices**: Automated governance with Terraform
- **Multi-cloud environments**: Consistent collection across AWS, Azure, GCP

---

*Document Version: 1.0*
*Based on New Relic official documentation, engineering blogs, and Terraform provider documentation*
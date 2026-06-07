# Kubernetes: Complete Container Orchestration Platform Reference

## Document Overview

This document provides a comprehensive analysis of Kubernetes' architectural patterns, scheduling algorithms, controller mechanisms, and distributed systems concepts. Kubernetes is an open-source container orchestration platform that automates the deployment, scaling, and management of containerized applications. Unlike Docker (which manages individual containers), Kubernetes manages clusters of machines and the workloads running on them, implementing a **declarative, controller-based architecture** that continuously reconciles actual state with desired state . This document covers the core architecture, scheduling algorithms (predicates, priorities, custom schedulers), controller patterns (informer, workqueue, reconciliation), etcd consensus, networking models (CNI, Service, kube-proxy), and other control plane components that power Kubernetes.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Control Plane Components](#control-plane-components)
3. [Node Components & Runtime](#node-components--runtime)
4. [Scheduling Framework & Algorithms](#scheduling-framework--algorithms)
5. [Controller Manager & Reconciliation Pattern](#controller-manager--reconciliation-pattern)
6. [etcd & Raft Consensus](#etcd--raft-consensus)
7. [Networking Architecture](#networking-architecture)
8. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Declarative API & Control Loop Pattern

**Purpose**: Allow users to declare desired state (what should exist) rather than imperative commands (what to do). Kubernetes continuously reconciles actual state to desired state.

**Core Pattern**: The fundamental control loop is:

```yaml
Desired State (YAML/API) → Kubernetes → Actual State (running resources)
                                    ↓
                            ┌───────────────────────┐
                            │  Observe → Diff → Act │
                            └───────────────────────┘
```

**Key Insight**: Users don't tell Kubernetes *how* to achieve a state; they declare *what* that state should be (e.g., "run 3 replicas"). Controllers continuously watch the cluster and make changes to ensure actual matches desired .

**Examples of Declarative Resources**:

| Resource | Desired State | Controller Action |
|----------|---------------|-------------------|
| Deployment | `replicas: 3`, `image: nginx:1.21` | Ensure 3 pods running with correct image |
| Service | `type: LoadBalancer`, `port: 80` | Provision cloud LB, configure endpoints |
| Ingress | `host: api.example.com` | Configure reverse proxy routing |

### 2. Control Plane vs. Data Plane Separation

**Purpose**: Separate cluster management logic (control plane) from workload execution (data plane/nodes) .

**The Two Planes**:

| Plane | Components | Responsibility | Scaling |
|-------|------------|----------------|---------|
| **Control Plane** | API Server, etcd, Scheduler, Controllers | Cluster state management, scheduling, coordination | 3-5 replicas (HA) |
| **Data Plane (Nodes)** | kubelet, kube-proxy, container runtime | Run pods, execute workloads | Horizontally (10s to 5000+ nodes) |

**Communication Direction**: Control plane manages nodes; nodes do not manage control plane. All modifications flow through API server.

### 3. Controller Pattern & Reconciliation Loop

**Purpose**: Watch API objects and take action to make reality match desired state.

**The Generic Controller Pattern** :

```
┌─────────────────────────────────────────────────────────────────┐
│                        Controller Process                        │
│                                                                  │
│   ┌──────────────┐     ┌──────────────┐     ┌──────────────┐    │
│   │   Informer   │────▶│  Workqueue   │────▶│ Reconciliation│    │
│   │   (Watch)    │     │  (Buffer)    │     │    Loop       │    │
│   └──────────────┘     └──────────────┘     └──────────────┘    │
│          │                                         │             │
│          │ (events: add/update/delete)            │             │
│          ▼                                         ▼             │
│   ┌──────────────┐                          ┌──────────────┐    │
│   │   API Server │                          │   API Server │    │
│   │   (List/     │                          │   (Update    │    │
│   │    Watch)    │                          │    Status)   │    │
│   └──────────────┘                          └──────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

**Controller Responsibilities** :
- **Node Controller**: Detects and responds when nodes go down
- **Job Controller**: Creates pods for one-off tasks
- **EndpointSlice Controller**: Maintains Service-to-Pod endpoint mappings
- **ServiceAccount Controller**: Creates default accounts for namespaces
- **Deployment Controller**: Manages ReplicaSets, rolling updates, rollbacks
- **StatefulSet Controller**: Manages stable storage and network identity

---

## Control Plane Components

### 4. kube-apiserver

**Purpose**: The front-end for Kubernetes control plane; exposes Kubernetes API; scales horizontally .

**Key Characteristics**:

| Aspect | Description |
|--------|-------------|
| **Primary API** | RESTful HTTP API (JSON/Protobuf) |
| **Authentication** | Client certificates, bearer tokens, OIDC, webhook |
| **Authorization** | RBAC (Role-Based Access Control), ABAC, Webhook |
| **Admission Control** | Mutating/Validating webhooks intercept requests |
| **Horizontal Scaling** | Stateless; can run multiple instances with load balancer  |
| **Default Port** | 6443 (secure), 8080 (insecure - deprecated) |

**API Request Flow**:

```
Client Request → Authentication → Authorization → Admission Control → Validation → etcd
                                                         │
                                                    (Mutating/Validating webhooks)
```

**Important**: The API server is the **only** component that talks to etcd. All other components (scheduler, controllers, kubelet) read/write cluster state through the API server, never directly to etcd.

### 5. etcd

**Purpose**: Consistent, distributed key-value store backing store for all cluster data .

**Role in Kubernetes**: etcd stores the **entire** cluster state: nodes, pods, configmaps, secrets, deployments, etc. Every API object is persisted here.

**etcd Architecture** :

| Feature | Implementation |
|---------|----------------|
| **Consensus Algorithm** | Raft  |
| **High Availability** | Cluster of odd number of nodes (3, 5, or 7)  |
| **Write Durability** | Quorum required (majority) |
| **Leader Election** | Automatic on failure |
| **Data Replication** | Full replication across cluster  |
| **Failure Tolerance** | With N nodes: can tolerate floor((N-1)/2) failures  |

**Why Raft?** :
- Strong consistency (no split-brain)
- Simple to understand (compared to Paxos)
- Leader-based for efficient writes
- Log replication for durability

**etcd in HA Control Plane**:
- Each control plane node runs an etcd member
- Raft ensures consistent state across all API servers
- API server reads/writes to local etcd (proxied to leader)
- 3-node etcd cluster: tolerates 1 failure; needs 2 for quorum

### 6. kube-scheduler

**Purpose**: Watches for newly created pods with no assigned node, selects a node for them to run on .

**Scheduling Factors** :
- Individual/collective resource requirements
- Hardware/software/policy constraints
- Affinity/anti-affinity specifications
- Data locality
- Inter-workload interference
- Deadlines
- Node taints/tolerations

**Scheduler Plugins Architecture** (Scheduling Framework) :

```
                    ┌─────────────────────────────────────┐
                    │            Scheduling Queue         │
                    │  (activeQ, backoffQ, unschedulableQ)│
                    └──────────────────┬──────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Schedule Cycle                                  │
│                                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │  Pre-    │─▶│ Filter   │─▶│ Post-    │─▶│  Score   │─▶│ Reserve  │      │
│  │  Filter  │  │          │  │  Filter  │  │          │  │          │      │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │
│                                                                              │
│  (Queue sort)                                                               │
│                                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                                   │
│  │  Permit  │─▶│   Bind   │─▶│  Post-   │                                   │
│  │          │  │          │  │  Bind    │                                   │
│  └──────────┘  └──────────┘  └──────────┘                                   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Two-Phase Process (Predicates + Priorities)** :

| Phase | Purpose | Output | Failure Handling |
|-------|---------|--------|------------------|
| **Filtering (Predicates)** | Eliminate nodes that cannot run the pod | Set of feasible nodes | Pod goes to unschedulable queue |
| **Scoring (Priorities)** | Rank feasible nodes | Node with highest score | Not applicable |

### 7. Controller Manager

**Purpose**: Runs controller processes that regulate cluster state .

**Key Controllers in kube-controller-manager** :

| Controller | Responsibility |
|------------|----------------|
| **Node Controller** | Detects node failures; manages node lifecycle  |
| **Deployment Controller** | Manages rollout of new ReplicaSets, rollbacks |
| **ReplicaSet Controller** | Ensures correct number of pods running |
| **StatefulSet Controller** | Manages stable network identity and storage |
| **DaemonSet Controller** | Ensures pods run on all (or specific) nodes |
| **Job Controller** | Creates pods for batch jobs  |
| **CronJob Controller** | Schedules Jobs on time-based schedule |
| **EndpointSlice Controller** | Populates endpoints for Services  |
| **ServiceAccount Controller** | Creates default ServiceAccounts per namespace  |
| **Namespace Controller** | Cleans up resources when namespace deleted |
| **PersistentVolume Controller** | Binds PVCs to PVs |
| **TTL Controller** | Cleans up terminated resources |

**Controller Pattern** :
- Logically independent but compiled into single binary
- Each controller watches specific API objects
- Reconciliation happens via Informer + Workqueue
- Controllers are idempotent (safe to run multiple instances)
- Uses leader election for singleton controllers

### 8. Cloud Controller Manager

**Purpose**: Embeds cloud-specific control logic; separates cloud provider dependencies .

**Why Separate?**:
- Allows Kubernetes to run without cloud providers (bare metal)
- Cloud providers can implement their own CCM
- API boundaries between Kubernetes core and cloud-specific code

**Controllers in CCM** :

| Controller | Cloud Interaction |
|------------|-------------------|
| **Node Controller** | Checks cloud API to determine if node still exists  |
| **Route Controller** | Sets up routes for pod networks in cloud infrastructure  |
| **Service Controller** | Creates/updates/deletes cloud load balancers  |

**Watch-Based Reconciliation** (v1.35+ alpha) :
- Traditionally: route controller reconciled on fixed interval (10s) 
- New model: reconciles on node events (add/update/delete)
- Reduces API calls to infrastructure providers 
- Faster route convergence (from up to 10s to near-immediate) 

---

## Node Components & Runtime

### 9. kubelet

**Purpose**: Primary node agent ensuring containers are running in pods; acts as node's "kube-apiserver" .

**Responsibilities**:

| Responsibility | Description |
|----------------|-------------|
| **Pod Lifecycle** | Reads PodSpec (file, HTTP, API), ensures containers running |
| **Container Health** | Liveness/Readiness/Startup probes |
| **Volume Management** | Mounts volumes (hostPath, PVCs, ConfigMaps, Secrets) |
| **Container Runtime Interface (CRI)** | Talks to container runtime (containerd, CRI-O) |
| **Node Status Reporting** | Reports node capacity, conditions, OS info, kubelet version |
| **Static Pods** | Runs pods from local manifest directory (for control plane self-hosting) |

**kubelet's API Endpoints** (default port 10250):
- `/pods`: List pods on node
- `/metrics`: Prometheus metrics
- `/healthz`: Health check
- `/logs`: Log access (with RBAC)

**Important**: kubelet does **not** manage containers not created by Kubernetes . It is not a generic container manager.

### 10. Container Runtime Interface (CRI)

**Purpose**: Plugable interface enabling Kubernetes to use different container runtimes without recompilation .

**CRI Operations**:
- `RunPodSandbox`: Create network namespace
- `CreateContainer`: Create container in sandbox
- `StartContainer`: Start container
- `StopContainer`: Gracefully stop
- `RemoveContainer`: Delete container
- `ListContainers`: List containers (for status)

**Supported Runtimes**:
- **containerd** (graduated CNCF)
- **CRI-O** (lightweight Kube-native, Fedora/RHEL default)
- **Docker Engine** (via cri-dockerd adapter)
- **gVisor** (security sandbox)

### 11. kube-proxy

**Purpose**: Maintains network rules on nodes for Service load balancing; implements the Service abstraction .

**What kube-proxy Does NOT Do** :
- ❌ Assigns IP addresses to pods (CNI plugin does this)
- ❌ Sets up pod-to-pod routing (CNI plugin does this)
- ❌ Creates virtual network interfaces (CNI plugin does this)

**What kube-proxy Does** :
- Manages **Service** ClusterIP and NodePort routing
- Load balances traffic across pod endpoints
- Maintains iptables/IPVS rules
- Handles session affinity (`sessionAffinity: ClientIP`)

**Proxy Modes**:

| Mode | Implementation | Pros | Cons |
|------|----------------|------|------|
| **iptables** (default) | Netfilter rules | Mature, stable | O(n) rules, high latency for large clusters |
| **IPVS** (recommended for large clusters) | Linux IP Virtual Server | O(1) lookup, more load balancing algorithms | Requires IPVS kernel module |
| **userspace** (deprecated) | Userspace proxy | Simpler | Slower, legacy only |

**Important**: Some CNI plugins (e.g., Cilium, Antrea) can implement Service handling themselves, making kube-proxy optional .

---

## Scheduling Framework & Algorithms

### 12. Scheduling Queue Management

**Purpose**: Prioritize and queue pods waiting to be scheduled .

**Three Queues**:

| Queue | Purpose | Pods Enter When |
|-------|---------|-----------------|
| **activeQ** | Pods ready for immediate scheduling | New pod, after backoff timer expires |
| **backoffQ** | Pods that failed scheduling with possible resolution | Failure due to cache inconsistency |
| **unschedulableQ** | Pods that cannot be scheduled (permanently) | No node satisfies all predicates |

**Queue Sorting**: Pods sorted by priority (higher priority processed first) .

### 13. Filtering (Predicate) Phase

**Purpose**: Eliminate nodes that cannot run the pod .

**Filter Types & Examples**:

| Filter Category | Example Filters | Purpose |
|----------------|-----------------|---------|
| **Storage** | NoVolumeZoneConflict, MaxCSIVolumeCountPred, CheckVolumeBindingPred | Zone/Limit/Volume compatibility  |
| **Pod-Node Matching** | CheckNodeCondition, PodToleratesNodeTaints, PodFitsHostPorts, MatchNodeSelector | Node readiness, taints, port conflicts  |
| **Pod-Affinity** | MatchInterPodAffinity | Pod topology affinity/anti-affinity  |
| **Pod Distribution** | EvenPodsSpread, CheckServiceAffinity | Balanced pod distribution |

**PodToleratesNodeTaints** :
Checks if pod tolerates node taints. Taints repel pods; tolerations allow exceptions.

**MatchNodeSelector** :
Validates `Pod.Spec.NodeSelector` and `Pod.Spec.Affinity.NodeAffinity` against node labels.

### 14. Even Pods Spread (Topology Spread Constraints)

**Purpose**: Ensure pods are evenly distributed across failure domains (nodes, zones, regions) .

**Topology Spread Constraints**:

```yaml
spec:
  topologySpreadConstraints:
  - maxSkew: 1
    whenUnsatisfiable: DoNotSchedule
    topologyKey: topology.kubernetes.io/zone
    labelSelector:
      matchLabels:
        app: my-app
```

**Fields** :

| Field | Description |
|-------|-------------|
| `topologyKey` | Label key for topology domain (e.g., `kubernetes.io/hostname`, `topology.kubernetes.io/zone`) |
| `maxSkew` | Maximum allowed difference in pod counts between domains |
| `whenUnsatisfiable` | `DoNotSchedule` (filter) or `ScheduleAnyway` (score penalty) |
| `labelSelector` | Which pods are part of this spread group |

**Skew Calculation** :
```
count[topo] = number of matched pods in that topology
min(count) = smallest count across all topologies
actualSkew = count[topo] - min(count)
```
Pod scheduled if `actualSkew <= maxSkew` for all constraints.

### 15. Scoring (Priority) Phase

**Purpose**: Rank feasible nodes; best node selected .

**Scoring Categories**:

| Category | Scoring Functions | Strategy |
|----------|------------------|----------|
| **Resource Usage** | LeastRequestedPriority, BalancedResourceAllocation, MostRequestedPriority | Spread or pack based on strategy |
| **Pod Distribution** | SelectorSpreadPriority, EvenPodsSpreadPriority | Spread pods across topology |
| **Node Affinity** | NodeAffinityPriority, NodePreferAvoidPodsPriority | Preference for label matching, anti-preferences |
| **Taint/Toleration** | TaintTolerationPriority | Preference for fewer untolerated taints |
| **Image Locality** | ImageLocalityPriority | Prefer nodes with image already pulled |

**Resource Usage Formulas** :

| Strategy | Formula | Use Case |
|----------|---------|----------|
| **Preferential Distribution (Spread)** | `(Allocatable - Request) / Allocatable × Score` | Balance load across nodes |
| **Preferential Stacking (Pack)** | `Request / Allocatable × Score` | Maximize node utilization |
| **Fragmentation Rate** | `{1 - Abs[CPU_usage - Mem_usage]} × Score` | Avoid imbalanced resource allocation |

### 16. Score Calculation & Node Selection

**Process** :
1. Each priority function returns a score (typically 0-10, may be scaled by weight)
2. Weighted sum: `totalScore = Σ (score_i × weight_i)`
3. Node with highest total score selected
4. Tie-breaking (if needed): random or deterministic (by node name)

---

## Controller Manager & Reconciliation Pattern

### 17. Informer Pattern

**Purpose**: Efficiently watch API resources with local cache to reduce API server load and latency.

**Informer Components**:

| Component | Purpose |
|-----------|---------|
| **Reflector** | Lists and watches resources via API server |
| **Delta FIFO Queue** | Stores pending change events |
| **Indexer (Local Cache)** | Thread-safe store of resource state |
| **Event Handlers** | `OnAdd`, `OnUpdate`, `OnDelete` callbacks |

**How Informers Work**:

```
API Server ──(Watch)──▶ Reflector ──(Events)──▶ Delta FIFO
                                                      │
                                                      ▼
                                                  Pop Event
                                                      │
                                                      ▼
                                               Update Indexer
                                                      │
                                                      ▼
                                               Call Handlers
                                                      │
                                                      ▼
                                                Workqueue Add
```

**Resource Version (RV)**:
- Each object has `metadata.resourceVersion` (string, monotonic)
- Watch uses `?resourceVersion=xxx` parameter for continuation
- Informer keeps local cache consistent via RV

### 18. Workqueue Pattern

**Purpose**: Decouple event receipt from processing; buffer and retry with backoff.

**Workqueue Features**:

| Feature | Implementation |
|---------|----------------|
| **Rate Limiting** | Item-based queue with backoff |
| **Delayed Processing** | AddAfter(duration) |
| **Ordering** | FIFO with item deduplication |
| **Garbage Collection** | Handled by controller |

**Rate Limiting Strategies**:
- `ItemExponentialFailureRateLimiter`: Exponential backoff per key
- `BucketRateLimiter`: Token bucket (overall QPS)
- `MaxOfRateLimiter`: Combine multiple limiters

### 19. Reconciliation Loop (Level Triggering vs. Edge Triggering)

**Kubernetes Uses Level Triggering (Desired vs. Actual State)** :

| Approach | Description | Kubernetes Behavior |
|----------|-------------|---------------------|
| **Edge Triggering** | React to every change event | Not used (too noisy) |
| **Level Triggering** | Compare desired vs. actual state; act on difference | **Used by controllers** |

**Why Level Triggering?** :
- Resilient to missed events (controllers resync periodically)
- Simpler logic (doesn't track what changed, just what *should* be)
- Handles cases where changes happen during controller downtime

**Controller Pattern Code** (simplified):

```go
func (c *Controller) Run() {
    for {
        key, _ := c.queue.Get() // Get next item
        namespace, name := splitKey(key)
        
        // Get desired state from informer cache
        desired := c.informer.Get(namespace, name)
        
        // Get actual state
        actual := c.getActual(desired)
        
        // Reconcile difference
        if !reflect.DeepEqual(desired, actual) {
            c.applyChanges(desired, actual)
        }
        
        c.queue.Done(key)
    }
}
```

---

## etcd & Raft Consensus

### 20. Raft Consensus Algorithm in etcd

**Purpose**: Strongly consistent, distributed consensus for cluster coordination .

**Why etcd Uses Raft** :
- Strong consistency guarantees (linearizable writes)
- Automatic leader election
- Simple to implement compared to Paxos
- Proven in production (CoreOS, Kubernetes)

**Raft Node States** :

| State | Description | Role |
|-------|-------------|------|
| **Leader** | Handles all client writes; logs replication | Proposes log entries |
| **Follower** | Passive; replicates logs; redirects writes | Responds to leader |
| **Candidate** | Intermediate state during election | Requests votes |

**Quorum Requirement** :

| Cluster Size | Failures Tolerated | Quorum Required |
|--------------|--------------------|-----------------|
| 1 (non-HA) | 0 | 1 |
| 3 | 1 | 2 |
| 5 | 2 | 3 |
| 7 | 3 | 4 |

**Formula**: `Quorum = floor(N/2) + 1` 

### 21. Raft Log Replication

**Consistency Guarantees** :
- Append-only log structure
- Majority commit for durability
- Replicated logs across all nodes
- Automatic catch-up for lagging nodes

**Write Flow** :

```
Client Write Request → Leader
                           │
                           ▼
                      Append to Leader's Log
                           │
                           ▼
                      Send AppendEntries RPC
                           │
                           ▼
                      Wait for Quorum ACK
                           │
                           ▼
                      Commit (apply to state machine)
                           │
                           ▼
                      Response to Client
```

**Leader Election Flow** :

```
Follower Timeout (no heartbeat) → Candidate
                                      │
                                      ▼
                                 Increment Term
                                      │
                                      ▼
                                 RequestVotes RPC
                                      │
                                      ▼
                            Majority Votes? ──No──▶ Timeout → Retry
                                      │
                                     Yes
                                      │
                                      ▼
                                    Leader
```

### 22. etcd Cluster Configuration

**Recommended Deployments** :

| Environment | Nodes | Setup |
|-------------|-------|-------|
| **Development** | 1 | Single node (no HA) |
| **Production (Small)** | 3 | Control plane nodes co-located |
| **Production (Large)** | 5 | Dedicated etcd nodes (separate from control plane) |
| **Very Large** | 7 | At most 7 for performance |

**Capacity Planning**:
- Default storage quota: 2GB (configurable) 
- etcd compacts history periodically (defragmentation needed)
- MVCC: Keeps multiple revisions (compact removes old)

---

## Networking Architecture

### 23. Pod Networking Fundamentals

**The Pod Network Problem**: Each pod needs a unique IP address reachable from all other pods across nodes without NAT.

**Solution**: Container Network Interface (CNI) .

**CNI Responsibilities** :
- Allocate IP address to pod
- Create virtual Ethernet pair (veth)
- Attach to node's bridge or routing table
- Set up routes for cross-node communication

**Important Distinction** :
- **kube-proxy**: Handles **Service** IPs (ClusterIP, NodePort, LoadBalancer)
- **CNI plugin** (Flannel, Calico, Cilium): Handles **Pod** IP assignment and routing

### 24. VXLAN Overlay Networking

**Purpose**: Encapsulate pod-to-pod traffic over physical network using IP/UDP .

**VXLAN Encapsulation** :

```
Original Pod-to-Pod Packet (inside container):
┌────────────────────────────────────────────────┐
│ MAC (src pod, dst pod) │ IP (src, dst) │ TCP/UDP │ Payload │
└────────────────────────────────────────────────┘
                          │
                          ▼ (encapsulation at node)
┌─────────────────────────────────────────────────────────────┐
│ Outer Ethernet │ Outer IP (node→node) │ Outer UDP (port 4789) │ VXLAN │ Original Packet │
└─────────────────────────────────────────────────────────────┘
```

**VXLAN Components** :

| Component | Purpose |
|-----------|---------|
| **VTEP (VXLAN Tunnel Endpoint)** | Encapsulate/decapsulate on each node |
| **VNI (VXLAN Network Identifier)** | Isolates different overlay networks |
| **Destination Port** | UDP 4789 (IANA assigned) |

**Cross-Node Communication** :
1. Pod A (Node A) sends packet to Pod B (Node B)
2. Node A's CNI (VTEP) encapsulates original packet in VXLAN
3. Outer packet: source = Node A IP, destination = Node B IP
4. Physical network routes based on node IPs
5. Node B's CNI decapsulates, forwards to Pod B

**Same-Node Communication** :
- No encapsulation
- Node's bridge or routing table forwards directly via veth pair
- Higher performance

### 25. Service Networking & kube-proxy

**Purpose**: Provide stable ClusterIP and load balancing for dynamic pod endpoints.

**Service Types** :

| Type | External Access | Implementation | Use Case |
|------|-----------------|----------------|----------|
| **ClusterIP** | Internal only (cluster DNS) | Virtual IP assigned by API server | Internal microservices |
| **NodePort** | Node IP:port (30000-32767) | Each node opens port | Basic external access |
| **LoadBalancer** | Cloud load balancer | CCM provisions LB; routes to NodePort | Production external access |
| **ExternalName** | CNAME record | DNS only | External service aliases |

**Service Endpoints**:
- **Endpoints** (legacy): `Endpoints` API object
- **EndpointSlices** (modern): Sharded for scalability (>1000 endpoints) 

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              USER / OPERATOR                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                           │
│  │   kubectl    │  │  Dashboard   │  │  API Client  │                           │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                           │
│         │                 │                 │                                   │
│         └─────────────────┼─────────────────┘                                   │
│                           │ (HTTPS, port 6443)                                  │
│                           │ Authentication + Authorization                     │
└───────────────────────────┼─────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              CONTROL PLANE                                       │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                           kube-apiserver                                 │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │    │
│  │  │ AuthN/AuthZ │  │Admission Ctrl│  │ API Routing │  │   Aggregator│     │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘     │    │
│  └────────────────────────────────────┬────────────────────────────────────┘    │
│                                       │                                         │
│         ┌─────────────────────────────┼─────────────────────────────┐           │
│         │                             │                             │           │
│         ▼                             ▼                             ▼           │
│  ┌─────────────┐              ┌─────────────┐              ┌─────────────┐      │
│  │    etcd     │              │kube-scheduler│             │kube-controller│     │
│  │             │              │             │              │   manager    │     │
│  │  ┌───────┐  │              │ ┌─────────┐  │              │ ┌─────────┐  │      │
│  │  │ Raft  │  │              │ │Filter   │  │              │ │Node     │  │      │
│  │  │ Leader│  │              │ │(Pred-   │  │              │ │Deploy-  │  │      │
│  │  │       │  │              │ │icates)  │  │              │ │ment     │  │      │
│  │  ├───────┤  │              │ ├─────────┤  │              │ ├─────────┤  │      │
│  │  │Follow-│  │              │ │Score    │  │              │ │Service  │  │      │
│  │  │er     │  │              │ │(Prior-  │  │              │ │Endpoint │  │      │
│  │  ├───────┤  │              │ │ities)   │  │              │ ├─────────┤  │      │
│  │  │Follow-│  │              │ └─────────┘  │              │ │Job, Cron│  │      │
│  │  │er     │  │              └─────────────┘              │ │ ...     │  │      │
│  │  └───────┘  │                                           │ └─────────┘  │      │
│  └─────────────┘                                           └─────────────┘      │
│         │                             │                             │           │
│         └─────────────────────────────┼─────────────────────────────┘           │
│                                       │                                         │
│                              (Watch + Informers)                                │
│                                       │                                         │
└───────────────────────────────────────┼─────────────────────────────────────────┘
                                        │
                                        │ (kubelet API)
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              WORKER NODE                                         │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                              kubelet                                     │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │    │
│  │  │ Pod Life-   │  │ Volume      │  │ Probe       │  │ Node Status │     │    │
│  │  │ cycle       │  │ Management  │  │ Manager     │  │ Reporter    │     │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘     │    │
│  └────────────────────────────────────┬────────────────────────────────────┘    │
│                                       │                                         │
│                                       │ (CRI - gRPC)                           │
│                                       ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                      Container Runtime (containerd/CRI-O)                │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                      │    │
│  │  │ Image       │  │ Container   │  │ Snapshotter │                      │    │
│  │  │ Pull/Store  │  │ Lifecycle   │  │ (OverlayFS) │                      │    │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                      │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                       │                                         │
│                                       │ (runC/container process)               │
│                                       ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                              Pods                                        │    │
│  │  ┌─────────────────────┐    ┌─────────────────────┐                     │    │
│  │  │ Container A         │    │ Container B         │                     │    │
│  │  │ (app)               │    │ (sidecar)           │                     │    │
│  │  └─────────────────────┘    └─────────────────────┘                     │    │
│  │                                                                          │    │
│  │  Shared: Network namespace (Pod IP), IPC, UTS, Volume mounts            │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                      CNI (Pod Networking)                                │    │
│  │                                                                          │    │
│  │  Pod IP assignment (from configured CIDR)                               │    │
│  │  Veth pair creation (pod ↔ node bridge)                                 │    │
│  │  VXLAN tunnels for cross-node traffic                                   │    │
│  │                                                                          │    │
│  │  (Calico, Flannel, Cilium, etc.)                                        │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                      kube-proxy (optional)                               │    │
│  │                                                                          │    │
│  │  iptables/IPVS rules for Service routing                                │    │
│  │  ClusterIP → Pod IP translation                                         │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | Kubernetes Component |
|---|------------------|-----------------|----------------------|
| 1 | Declarative API + Control Loop | Desired vs. actual state reconciliation | All controllers |
| 2 | Control Plane/Data Plane Separation | Central management vs. distributed execution | API Server + kubelet |
| 3 | Controller Pattern (Informer+Workqueue) | Event handling with local cache | kube-controller-manager |
| 4 | Raft Consensus Algorithm | Strongly consistent cluster state | etcd |
| 5 | Leader Election (Raft) | Fault-tolerant control plane | etcd, leader-elect controllers |
| 6 | Quorum-based Decision | Majority agreement for writes | etcd (N/2+1) |
| 7 | Predicate (Filtering) | Eliminate unsuitable nodes | kube-scheduler |
| 8 | Priority (Scoring) | Rank feasible nodes | kube-scheduler |
| 9 | Even Pods Spread (Topology Spread) | Balanced distribution across domains | kube-scheduler |
| 10 | Resource Allocation Strategies | Spread vs. Pack (fragmentation-aware) | kube-scheduler |
| 11 | Exponential Backoff Queue | Pod retry with delay | Scheduling Queue |
| 12 | Informer (List/Watch + Cache) | Efficient API watching | Client-go, all controllers |
| 13 | Workqueue (Rate Limiting) | Decouple event processing | Client-go, controllers |
| 14 | Level Triggering (vs. Edge Triggering) | Resilient reconciliation | All controllers |
| 15 | Leader Election (Controller) | Singleton controller execution | LeaderElector |
| 16 | VXLAN Overlay Networking | Cross-node pod communication | CNI plugins (Flannel, Calico) |
| 17 | iptables/IPVS Service Proxy | Service load balancing | kube-proxy |
| 18 | Pod IP Assignment & Routing | Pod network isolation | CNI plugins |
| 19 | Watch-based Reconciliation | Event-driven (vs. interval) | Cloud Controller Manager (v1.35+) |
| 20 | Resource Version + Watch Continuation | Incremental API updates | API server, Informers |

---

## Configuration Reference

### API Server Flags
```bash
# Authentication/Authorization
--authorization-mode=RBAC,Node
--authentication-mode=Webhook

# Admission Control
--enable-admission-plugins=NodeRestriction,PodSecurity,Priority

# etcd
--etcd-servers=https://etcd-1:2379,https://etcd-2:2379,https://etcd-3:2379

# TLS
--tls-cert-file=/etc/kubernetes/pki/apiserver.crt
--tls-private-key-file=/etc/kubernetes/pki/apiserver.key
```

### Scheduler Configuration
```yaml
apiVersion: kubescheduler.config.k8s.io/v1
kind: KubeSchedulerConfiguration
profiles:
- schedulerName: custom-scheduler
  plugins:
    filter:
      enabled:
      - name: NodeResourcesFit
    score:
      enabled:
      - name: NodeResourcesBalancedAllocation
        weight: 2
      - name: ImageLocality
        weight: 1
  pluginConfig:
  - name: NodeResourcesFit
    args:
      scoringStrategy:
        type: LeastAllocated
```

### kubelet Configuration
```yaml
apiVersion: kubelet.config.k8s.io/v1beta1
kind: KubeletConfiguration
cgroupDriver: systemd
containerRuntimeEndpoint: unix:///run/containerd/containerd.sock
maxPods: 110
staticPodPath: /etc/kubernetes/manifests
evictionHard:
  memory.available: "500Mi"
  nodefs.available: "10%"
```

### kube-proxy ConfigMap
```yaml
apiVersion: kubeproxy.config.k8s.io/v1alpha1
kind: KubeProxyConfiguration
mode: "ipvs"
ipvs:
  scheduler: "rr"
  excludeCIDRs:
  - "10.0.0.0/8"
```

---

## Performance & Scale Limits

| Component | Scale Limit | Recommendation |
|-----------|-------------|----------------|
| Cluster size | 5000 nodes | Bottleneck: etcd watch events |
| Pods per node | 110 (default) | kubelet maxPods |
| Total pods | 150,000 | etcd object count limit |
| etcd storage | 2GB (default) | Compact regularly; increase with quota-backend-bytes |
| Service endpoints | 5000 per service | EndpointSlices (sharded) |
| API QPS | Configurable | Node limit: kubelet --kube-api-qps=20 |
| Controller resync | 10s - 30m | Default 10-60 minutes (for reliability) |

---

## Comparison with Other Orchestrators

| Feature | Kubernetes | Docker Swarm | Apache Mesos |
|---------|------------|--------------|--------------|
| Architecture | Control plane + nodes | Manager + workers | Master + agents + frameworks |
| API | Declarative REST | Declarative REST | Framework-specific |
| Scheduler | Predicate+Priority | Spread (round-robin) | Framework-defined |
| Networking model | CNI plugin | Overlay (built-in) | Framework-specific |
| Workload types | Pods, Deployments, Jobs, etc. | Services | Long-running, batch |
| Storage | CSI + FlexVolume | Volume plugins (limited) | Framework-specific |
| HA etcd | Raft (3+ nodes) | Raft (managers) | ZooKeeper (if needed) |
| Learning curve | High | Medium | Very high |

---

## Source Code Reference

| Component | Repository (GitHub) |
|-----------|---------------------|
| API Server | `kubernetes/kubernetes/cmd/kube-apiserver/` |
| Scheduler | `kubernetes/kubernetes/cmd/kube-scheduler/` |
| Controller Manager | `kubernetes/kubernetes/cmd/kube-controller-manager/` |
| kubelet | `kubernetes/kubernetes/cmd/kubelet/` |
| kube-proxy | `kubernetes/kubernetes/cmd/kube-proxy/` |
| etcd | `etcd-io/etcd` |
| Client-go (Informers) | `kubernetes/client-go` |
| Scheduler Framework | `kubernetes/kubernetes/pkg/scheduler/framework` |

---

## Conclusion

Kubernetes' design philosophy emphasizes:

- **Declarative over imperative**: Users specify desired state; system reconciles
- **Control loops over scripts**: Controllers continuously act; resilient to failures
- **Extensibility**: CRDs, admission webhooks, scheduler plugins, CNI, CSI, CRI
- **Self-healing**: Automatic replacement of failed pods, nodes, services
- **Portability**: Same YAML works on GKE, EKS, AKS, on-prem, Minikube

Key innovations and algorithms include:

- **Declarative API + Control Loop Pattern**: Fundamental architecture for all Kubernetes controllers
- **Controller Pattern (Informer+Workqueue)** : Efficient, scalable event processing with local cache
- **Raft Consensus Algorithm** (etcd): Strongly consistent cluster state with automatic failover
- **Predicate + Priority Scheduling**: Two-phase scheduling (filtering → scoring)
- **Even Pods Spread (Topology Spread Constraints)** : Balanced distribution across failure domains
- **Resource Allocation Scoring**: Spread, pack, and fragmentation-aware strategies
- **Level Triggered Reconciliation**: Resilience to missed events; simpler than edge-triggered systems
- **VXLAN Overlay Networking**: Encapsulation-based pod networking (decoupled from physical network)
- **CNI (Container Network Interface)** : Pluggable pod IP assignment and routing
- **Leader Election (etcd + Controllers)** : High availability for all control plane components

This combination of algorithms and patterns makes Kubernetes suitable for:
- **Cloud-native microservices**: Declarative deployments, scaling, rollouts
- **Batch processing**: Jobs, CronJobs, Workflow controllers
- **Stateful applications**: StatefulSets, persistent volumes, ordered pod management
- **Multi-cloud/hybrid-cloud**: Consistent APIs across all environments
- **Edge computing**: Lightweight distributions (K3s, KubeEdge)
- **DevOps pipelines**: CI/CD integration, GitOps with ArgoCD/Flux

---

*Document Version: 1.0*
*Based on Kubernetes official documentation (v1.32), KEPs, and source code analysis*
# Docker: Complete Containerization Platform Reference

## Document Overview

This document provides a comprehensive analysis of Docker's architectural patterns, containerization algorithms, and runtime technologies. Docker is a platform for developing, shipping, and running applications in containers — lightweight, portable, isolated environments that package software with all its dependencies . Unlike traditional virtual machines that virtualize hardware and run a full guest OS, Docker containers virtualize the operating system kernel, sharing it among containers while isolating them via Linux kernel features. This document covers the core architecture, storage algorithms (UnionFS/OverlayFS), process isolation mechanisms (namespaces, cgroups), networking models, and image layering techniques that power Docker.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Container-Namespace Isolation](#container-namespace-isolation)
3. [Resource Control (cgroups)](#resource-control-cgroups)
4. [Union Filesystem & Image Layering](#union-filesystem--image-layering)
5. [Storage Drivers (OverlayFS Architecture)](#storage-drivers-overlayfs-architecture)
6. [Networking Architecture](#networking-architecture)
7. [Registry & Image Distribution](#registry--image-distribution)
8. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Client-Server Architecture

**Purpose**: Separate the user-facing command interface from the background process managing containers.

**Core Components**:

| Component | Description | Process Name |
|-----------|-------------|--------------|
| **Docker Daemon** | Background service that manages containers, images, networks, and volumes | `dockerd` |
| **Docker Client** | Command-line interface for users to interact with the daemon | `docker` |
| **REST API** | Programmatic interface for the daemon; can be local or remote | HTTP/gRPC |

**Communication Flow**:
```
User → docker command → REST API (Unix socket/network) → dockerd
```

The Docker client and daemon can run on the same host (using Unix sockets for local communication) or on different machines (using a network interface with TLS authentication) . By default, Docker uses an HTTP API over Unix sockets, but remote APIs require TLS configuration.

**Why Client-Server?** :
- **Separation of concerns**: Client handles user interaction, daemon handles container lifecycle
- **Remote management**: One client can manage multiple daemons across different hosts
- **API extensibility**: Tools (e.g., Portainer, CI/CD systems) can integrate directly with the daemon API
- **Process isolation**: Daemon runs with elevated privileges; client runs with user privileges

### 2. Daemon (dockerd) Architecture

**Purpose**: Manage all Docker objects and orchestrate container operations.

**Responsibilities**:

| Responsibility | Description |
|----------------|-------------|
| **Image management** | Pull, build, store, and distribute images |
| **Container lifecycle** | Create, start, stop, delete containers |
| **Network management** | Create and manage network drivers (bridge, overlay, macvlan) |
| **Volume management** | Create and attach persistent storage volumes |
| **Secret management** | Store and inject sensitive data into containers |
| **Swarm orchestration** | Manage cluster of Docker daemons (optional mode) |

**Subcomponents**:
- **containerd**: Industry-standard container runtime (spun off from Docker in 2016) that actually spawns containers
- **runC**: Low-level container runtime that implements the OCI specification (creates namespaces, cgroups)
- **containerd-shim**: Process that manages a single container, allowing the daemon to restart without affecting running containers

---

## Container-Namespace Isolation

### 3. Linux Namespace Fundamentals

**Purpose**: Provide processes with a private, isolated view of system resources.

**Core Principle**: Namespaces wrap global system resources so that processes within a namespace see their own isolated instance of that resource . Unlike virtual machines (which emulate hardware and run a full OS kernel), containers share the host's kernel while using namespaces to make each container think it has its own isolated environment.

**Visual Comparison**:
```
Virtual Machines:                     Containers:
┌─────────────────────────┐          ┌─────────────────────────┐
│ App A │ App B │ App C   │          │ App A │ App B │ App C   │
├───────┼───────┼─────────┤          ├───────┼───────┼─────────┤
│Guest OS│Guest OS│Guest OS│          │  Libraries & Runtime   │
├───────┼───────┼─────────┤          ├─────────────────────────┤
│ Hypervisor                        │      Host OS Kernel      │
├─────────────────────────┤          └─────────────────────────┘
│      Host Hardware                 └─────────────────────────┘
```

The Docker Engine (containerd/runC) invokes the `unshare` system call to create new namespaces when spawning a container.

### 4. Namespace Types

Docker utilizes seven Linux namespaces for isolation :

| Namespace | Flag | Isolated Resource | Docker Behavior |
|-----------|------|-------------------|-----------------|
| **mount (mnt)** | `CLONE_NEWNS` | Filesystem mount points | Container sees only its image layers and any mounted volumes |
| **process ID (pid)** | `CLONE_NEWPID` | Process ID tree | Container processes start at PID 1; cannot see host processes |
| **network (net)** | `CLONE_NEWNET` | Network interfaces, routing tables | Container gets its own virtual network stack (except host mode) |
| **interprocess communication (ipc)** | `CLONE_NEWIPC` | IPC resources (semaphores, message queues) | Prevents cross-container IPC communication |
| **UTS (hostname)** | `CLONE_NEWUTS` | Hostname and domain name | Container can have its own hostname independent of host |
| **user** | `CLONE_NEWUSER` | User and group IDs | Maps container UIDs to different host UIDs for security |
| **cgroup** | `CLONE_NEWCGROUP` | cgroup root directory | Isolates cgroup hierarchy view (used by runC, not Docker by default)  |

**Creating a new pid namespace (example using `unshare`)**:
When a new pid namespace is created, the bash process inside it sees its own PID as 1, isolated from the host's process tree .

### 5. Network Namespace Isolation

**Purpose**: Give each container its own isolated network stack.

**Resources Isolated**:
- Network interfaces (physical and virtual)
- IP routing tables
- Firewall rules (iptables)
- Socket namespace (port allocations)

**Implementation**:
When a container is created (unless using `--net=host`), Docker creates a **network namespace** for that container . The container's first virtual Ethernet device (eth0) is a veth pair: one end inside the container's namespace, the other end bridged to the host's network (typically `docker0` bridge).

**Port Mapping**:
Because each network namespace has its own port space, multiple containers can listen on port 80 internally. Docker uses iptables NAT to map host ports to container ports (e.g., `-p 8080:80` means host port 8080 → container port 80).

### 6. User Namespace

**Purpose**: Enhance security by isolating user IDs between host and container.

**How It Works**:
The user namespace maps UIDs inside the container (e.g., root = 0) to a non-privileged range on the host (e.g., UID 100000-165535) . This means:
- Container's root user (UID 0) maps to an unprivileged host UID
- If a container escapes, the attacker would have minimal privileges on the host
- Prevents privilege escalation via container breakout

**Enabling**:
User namespaces are not enabled by default but can be configured in `/etc/docker/daemon.json`:
```json
{
  "userns-remap": "default"
}
```

### 7. Creating a Namespace

**System Call Flow**:
At the lowest level (runC), namespaces are created using the `clone()` system call with namespace flags :

```c
// Simplified: Creating a new process in new namespaces
int pid = clone(child_func, stack, 
                CLONE_NEWNS | CLONE_NEWPID | CLONE_NEWNET | CLONE_NEWIPC | CLONE_NEWUTS,
                arg);
```

For an existing process to enter a new namespace, the `unshare()` system call is used. Both operations require `CAP_SYS_ADMIN` capability.

---

## Resource Control (cgroups)

### 8. Control Groups (cgroups) Fundamentals

**Purpose**: Limit, account for, and isolate resource usage (CPU, memory, disk I/O, etc.) of process groups .

**Core Principle**: While namespaces provide **isolation** (making resources invisible), cgroups provide **limits** (making resources constrained). A container without cgroups could potentially consume all host resources, starving other containers.

**Comparison**:

| Feature | Namespaces (Isolation) | Cgroups (Limits) |
|---------|----------------------|------------------|
| Purpose | Hide resources | Restrict resource usage |
| Example | Container sees its own PID 1 | Container limited to 1 CPU core |
| Enforcement | Visibility-based | Kernel-enforced throttling |
| Violation | Impossible to see | Process throttled or OOM-killed |

### 9. Cgroup Versions

**Cgroup v1 vs. v2** :

| Aspect | Cgroup v1 | Cgroup v2 |
|--------|-----------|-----------|
| **Hierarchy** | Separate hierarchy per controller (e.g., `/sys/fs/cgroup/cpu/`, `/sys/fs/cgroup/memory/`) | Unified hierarchy (single `/sys/fs/cgroup/`) |
| **Controller coordination** | Each controller operates independently | Controllers unified; kernel correlates limits |
| **Complexity** | High (debugging requires checking multiple directories) | Lower (single view) |
| **Thread-level limits** | CPU per thread possible; memory cannot be per-thread | CPU and I/O per thread possible via special flags |
| **Default in Docker** | Legacy (still used on older systems) | Modern Linux distributions |

**Check current version**:
```bash
stat -f -c %T /sys/fs/cgroup    # Shows "cgroup2fs" for v2
mount | grep cgroup
```

### 10. Common Cgroup Controllers in Docker

| Controller | Purpose | Docker Flag | Example |
|------------|---------|-------------|---------|
| **cpu** | Limit CPU usage via shares or quota | `--cpu-shares`, `--cpus`, `--cpuset-cpus` | `--cpus=2.5` (limit to 2.5 cores) |
| **memory** | Limit memory usage | `--memory`, `--memory-swap` | `--memory=512m` |
| **blkio** | Limit disk I/O (IOPS, BPS) | `--device-read-bps`, `--device-write-iops` | `--device-write-iops=/dev/sda:100` |
| **device** | Control device access | `--device` | `--device=/dev/usb:/dev/usb:rwm` |
| **freezer** | Suspend/resume container processes | `docker pause`/`docker unpause` | Freezes all processes before stopping |
| **pid** | Limit max number of processes | `--pids-limit` | `--pids-limit=100` |
| **net_cls** | Tag network packets for traffic control | Not directly; used by network plugins | Classification for QoS |

### 11. How Cgroups Work Internally

**Cgroup Directory Structure (v1 example)** :
When a container starts, Docker creates a cgroup directory for it:
```
/sys/fs/cgroup/system.slice/docker-<container-id>.scope/
```

**CPU Limits (cgroup v2)** :
CPU limits are enforced via `cpu.max` file:
```bash
# Format: <max_quota> <period>
# Example: 50000 100000 = 50% of one CPU core
echo "50000 100000" > /sys/fs/cgroup/my-container/cpu.max
```
- **Period**: Scheduling period in microseconds (default 100 ms)
- **Max quota**: Maximum microseconds of CPU time allowed per period
- **Unlimited**: `max 100000`

**Memory Limits** :
```bash
# Soft limit (throttling begins, not immediate kill)
echo "80M" > /sys/fs/cgroup/my-container/memory.high

# Hard limit (exceeding triggers OOM killer)
echo "100M" > /sys/fs/cgroup/my-container/memory.max
```

**Memory Control Flow**:
1. Container reaches `memory.high` → process stalls (throttled), kernel attempts memory reclamation
2. Container exceeds `memory.max` → OOM killer terminates process(es) immediately

### 12. Cgroup Drivers

**Two driver options** :

| Driver | Description | Preferred |
|--------|-------------|-----------|
| **cgroupfs** | Direct manipulation of cgroup filesystem (write PIDs and limits to `/sys/fs/cgroup/*/` files) | Legacy |
| **systemd** | All cgroup operations through systemd interfaces (manages cgroups as systemd slices) | **Recommended** (on systemd-based distros) |

**Why systemd is preferred**:
- Integrates cgroup management with system's service manager
- Prevents conflicts between Docker and systemd services
- Provides unified view of all cgroup limits
- Automatically cleans up when container exits

**Configuration**:
```json
{
  "exec-opts": ["native.cgroupdriver=systemd"]
}
```

---

## Union Filesystem & Image Layering

### 13. Union Filesystem (UnionFS) Concept

**Purpose**: Overlay multiple directories (layers) to present a single, unified view .

**Why UnionFS is Essential for Docker**:
- **Image layering**: Each Docker instruction (`FROM`, `RUN`, `COPY`) creates a layer
- **Layer reuse**: Multiple images share common base layers (saves disk space, speeds up pulls)
- **Copy-on-write (CoW)**: Containers write changes to new layer without modifying image layers
- **Immutable images**: Images never change; containers get a writable layer on top

**Problem Without UnionFS**:
- Every image would need a full copy of the filesystem
- Running a container would require copying all image files
- Image pulls would download duplicate data for common dependencies

**UnionFS Alternatives Historically**:

| Filesystem | Status in Docker | Notes |
|------------|------------------|-------|
| **AUFS** (Another UnionFS) | Legacy | First Docker storage driver; not merged into mainline Linux kernel |
| **OverlayFS** | **Current default** | In mainline kernel since 3.18; simpler and faster than AUFS  |
| **Overlay2** | **Recommended** | Supports up to 128 lower layers; improved inode efficiency  |
| **devicemapper** | Deprecated | Used for non-overlay-supporting kernels |
| **btrfs/zfs** | Available | For specific filesystem needs |

### 14. Image Layering Model

**Purpose**: Break down container images into reusable, immutable layers.

**Layer Example** (Python application) :

| Layer | Content | Command | Reusable? |
|-------|---------|---------|-----------|
| Layer 1 | Base OS (Ubuntu) + apt package manager | `FROM ubuntu:22.04` | ✓ (many images share Ubuntu base) |
| Layer 2 | Python runtime + pip | `RUN apt install python3` | ✓ (all Python apps on Ubuntu share) |
| Layer 3 | requirements.txt file | `COPY requirements.txt` | ✓ (until requirements change) |
| Layer 4 | Python dependencies (pip install) | `RUN pip install -r requirements.txt` | ✓ (same deps across versions) |
| Layer 5 | Application source code | `COPY . /app` | ✗ (unique to this app version) |

**Layer Characteristics**:
- **Immutable**: Once created, a layer never changes
- **Content-addressable**: Layers identified by cryptographic hash (SHA256)
- **Reusable**: Multiple images can reference the same layer
- **Stored in `/var/lib/docker/overlay2/`** 

**Benefits of Layering**:

| Benefit | Explanation |
|---------|-------------|
| **Build caching** | If layer unchanged, Docker reuses cached version (fast rebuilds) |
| **Storage efficiency** | Common base layer stored once, referenced by many images |
| **Pull efficiency** | Only missing layers downloaded; local layers reused |
| **Container start speed** | Only new writable layer created; no files copied |

---

## Storage Drivers (OverlayFS Architecture)

### 15. OverlayFS/Overlay2 Implementation

**Purpose**: Implement the union filesystem using OverlayFS kernel module.

**OverlayFS Terminology** :

| Term | Purpose | Docker Mapping |
|------|---------|----------------|
| **lowerdir** | Read-only bottom layer(s) | Image layers (one or more directories) |
| **upperdir** | Writable top layer | Container's changes |
| **merged** | Unified view of lower + upper | Container's filesystem root |
| **workdir** | Internal OverlayFS scratch space | Copy-up operations, file renaming |

**Directory Structure (`/var/lib/docker/overlay2/`)** :
```
/var/lib/docker/overlay2/
├── l/                              # Short name symlinks (avoid mount arg length limits)
│   ├── 6Y5IM2XC7TSNIJZZFLJCS6I4I4 -> ../3a36935c9d.../diff
│   └── B3WWEFKBG3PLLV737KZFIASSW7 -> ../4e9fa83caff.../diff
├── <layer-hash>/                   # Each image layer or container
│   ├── diff/                       # Layer's file contents
│   ├── link                        # Short name reference
│   ├── lower                       # Parent layer reference (for non-bottom layers)
│   ├── merged/                     # Unified view (for containers)
│   └── work/                       # Internal OverlayFS use (for containers)
```

**Layer Types** :

| Type | Contains | Has lower file? |
|------|----------|-----------------|
| **Bottom image layer** | Full filesystem (`/bin`, `/usr`, etc.) | No (`lower` absent) |
| **Upper image layer** | Only changes from parent | Yes (references parent) |
| **Container layer** | Writable layer + full stack | Yes (references top image layer) |

### 16. Copy-on-Write (CoW) Operation

**Purpose**: Defer copying of data until it is written; containers read from image layers and only write to their own layer.

**Read Scenario** :

| Case | Scenario | Performance |
|------|----------|-------------|
| **File not in container (upperdir)** | Container reads file, it exists only in image (lowerdir) | Low overhead (direct read from lowerdir) |
| **File only in container (upperdir)** | File created/written by container previously | Direct read from upperdir |
| **File in both layers** | File exists in image AND container (modified) | Container version in upperdir obscures lowerdir version |

**Write Scenario (First Write)** :

When a container writes to a file that exists only in the image layer (lowerdir), OverlayFS performs a **copy_up** operation:
1. Copy entire file from lowerdir to upperdir
2. Container writes changes to the new copy in upperdir
3. Subsequent writes only touch upperdir (no copy_up)

**Copy-up Characteristics**:
- **File-level, not block-level**: Even if modifying 1 byte of a 1GB file, entire file is copied
- **First write only**: Copy_up happens once per file (not per write)
- **Two-layer only**: OverlayFS only has two active layers (lowerdir + upperdir), unlike AUFS's multi-layer 
- **Performance impact**: Copy_up of large files can cause noticeable latency on first write

**Whiteout Files** :
When a file is deleted in the container, OverlayFS creates a **whiteout file** (character device with 0/0 device number) in upperdir. This whiteout file obscures the same file in lowerdir without actually deleting the original in the image.

**Opaque Directories**:
When a directory is deleted in the container, OverlayFS creates an **opaque directory** marker in upperdir, which masks the entire lowerdir directory from view.

### 17. Overlay2 Optimization

**Purpose**: Improve upon original OverlayFS driver with better multi-layer support.

**Key Improvements** :

| Feature | Overlay (old) | Overlay2 (current) |
|---------|---------------|---------------------|
| **Max lower layers** | 2 (effectively) | Up to 128 |
| **Inode usage** | High (one per layer per file via hardlinks) | Lower (uses multiple lowerdirs) |
| **Build performance** | Slower | Better for `docker build` and `docker commit` |
| **Diff operation** | Requires hardlinks | Native OverlayFS diff support |

**How Overlay2 Achieves 128 Layers**:
Instead of stacking OverlayFS mounts on top of each other (which can't exceed 2 layers), Overlay2 passes multiple `lowerdir` parameters to the OverlayFS mount:
```
mount -t overlay overlay -o lowerdir=layer5:layer4:layer3:layer2:layer1,upperdir=container,workdir=work /merged
```

### 18. Configuration

**Enabling Overlay2** :
```json
// /etc/docker/daemon.json
{
  "storage-driver": "overlay2",
  "storage-opts": [
    "overlay2.override_kernel_check=true"   // Only for testing
  ]
}
```

**Prerequisites** :
- Linux kernel ≥ 4.0 (or RHEL/CentOS 3.10.0-514+)
- Backing filesystem: ext4 or xfs (with `d_type=true` enabled)
- For xfs: `xfs_info /var/lib/docker` should show `ftype=1`

**Verify**:
```bash
docker info | grep -i "storage"
# Output: Storage Driver: overlay2
```

**Caution**: Changing storage driver makes existing containers and images inaccessible. Back up with `docker save` or push to registry first .

---

## Networking Architecture

### 19. Container Network Model (CNM)

**Purpose**: Abstract networking for containers with pluggable drivers.

**CNM Components** :

| Component | Purpose | Implementation |
|-----------|---------|----------------|
| **Sandbox** | Container's network stack (isolated configuration) | Network namespace |
| **Endpoint** | Connection point connecting sandbox to network | veth pair, OVS internal port |
| **Network** | Group of endpoints that can communicate | Linux bridge, VLAN, overlay |

**libnetwork**:
Docker's native implementation of CNM, providing drivers:
- `bridge` (default single-host)
- `overlay` (multi-host with VXLAN)
- `host` (container uses host's network stack)
- `macvlan` (container gets its own MAC address)
- `none` (no networking)
- `ipvlan` (layer 2/3 VLAN tagging)

### 20. Network Drivers

**Bridge (default)** :
- Creates an internal Linux bridge (`docker0`) on the host
- Each container gets a virtual Ethernet interface (veth) connected to bridge
- Containers communicate via NAT for external access
- IP allocation from private subnet (typically 172.17.0.0/16)

**Overlay (multi-host)** :
- Enables containers on different hosts to communicate as if on same network
- Uses VXLAN (Virtual Extensible LAN) tunneling (UDP port 4789)
- Encapsulates container-originated packets in UDP for transport
- Requires key-value store (libkv) for state coordination (or Docker Swarm's built-in store)

**Overlay Creation** :
```bash
docker network create --driver overlay --subnet=10.0.0.0/24 my-overlay-net
```

**Macvlan** :
- Assigns a physical MAC address to each container
- Container appears as physical device on the network
- Direct layer 2 communication without NAT
- Requires promiscuous mode on the parent interface

**Host** :
- Container shares host's network namespace (no isolation)
- No IP assignment; container uses host's IP and port space
- Highest performance, but container cannot have its own network stack
- Use case: performance-critical, network monitoring tools

### 21. libnetwork Initialization

**Three-step process for CNM networking** :

1. **Initialize NetworkController** (during dockerd startup)
2. **Initialize networks** (bridge, null, host)
3. **Create sandbox and endpoints** (when container starts)

**Sandbox Creation**:
When a container starts, Docker's `NetworkManager`:
- Creates (or reuses) a network namespace (sandbox)
- For bridge network: creates veth pair, attaches one end to bridge
- For host network: uses host's existing namespace
- No sandbox is created until container networking is configured

**Endpoint Join**:
The `Endpoint.Join()` operation :
1. Calls driver-specific join (e.g., bridge driver creates veth)
2. Updates `/etc/hosts` and DNS configuration
3. Populates network resources (routes, interfaces) into sandbox via `populateNetworkResources()`

---

## Registry & Image Distribution

### 22. Container Registry Concept

**Purpose**: Store and distribute Docker images across hosts.

**Registry Types**:

| Type | Example | Characteristics |
|------|---------|-----------------|
| **Public registry** | Docker Hub | Free for public images; rate-limited for anonymous pulls |
| **Private registry** | Self-hosted, AWS ECR, GCR, ACR | Full control, integrated with cloud IAM |
| **Official images** | `ubuntu`, `nginx`, `postgres` | Maintained by Docker or trusted partners; security best practices |

**Registry API** (HTTP/HTTPS):
- `docker pull`: GET `/v2/<name>/manifests/<reference>`
- `docker push`: PUT/POST `/v2/<name>/blobs/uploads/`
- Registry implements OCI Distribution Specification

### 23. Image Push/Pull Flow

**Pull Operation**:
1. Client requests image manifest (JSON describing layers)
2. Registry returns layer digests (SHA256 hashes)
3. Client checks local storage for existing layers
4. Missing layers downloaded from registry
5. Layers extracted to `/var/lib/docker/overlay2/`

**Push Operation**:
1. Client calculates digests for local layers
2. Uploads manifest to registry
3. Uploads missing blob layers
4. Registry stores layers and updates tags

### 24. Content Addressable Storage

**Purpose**: Identify layers by cryptographic hash rather than arbitrary names.

**How It Works**:
- Each layer is identified by SHA256 hash of its contents
- Image manifest references layers by digest (e.g., `sha256:a3ed95caeb02...`)
- Tags (e.g., `ubuntu:22.04`) are human-friendly pointers to digests

**Benefits**:
- **Deduplication**: Same layer stored once, referenced by many images
- **Integrity**: Client verifies downloaded layer hash matches manifest
- **Immutability**: Layer with same hash always has same content
- **Cache efficiency**: Registry can cache by digest permanently

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          USER / OPERATOR                                     │
│                                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │ docker CLI   │  │ docker-compose│ │ Portainer/    │                       │
│  │ (docker run) │  │ (docker-compose)│ │ UI           │                       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                       │
│         │                 │                 │                               │
└─────────┼─────────────────┼─────────────────┼───────────────────────────────┘
          │                 │                 │
          │ (Unix socket or TCP over TLS)
          │                 │                 │
          ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DOCKER CLIENT                                        │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  docker command parser → API request formatter → send to daemon      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │ REST API (HTTP/gRPC)
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         DOCKER DAEMON (dockerd)                             │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Core Components:                                                     │    │
│  │  • Image Manager    • Container Manager   • Network Manager          │    │
│  │  • Volume Manager   • Secret Manager      • Swarm (optional)         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    │ (gRPC to containerd)                   │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    containerd (High-level Runtime)                    │    │
│  │  • Manages container lifecycle (start, stop, pause)                  │    │
│  │  • Pulls images, manages snapshotting                                 │    │
│  │  • Handles container metadata and state                               │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    │ (OCI runtime API)                       │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     containerd-shim                                   │    │
│  │  • Per-container process manager                                      │    │
│  │  • Allows daemon restart without affecting containers                 │    │
│  │  • Forwards signals and I/O between daemon and container             │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                                    │ (exec into container)                   │
│                                    ▼                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CONTAINER RUNTIME                                  │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         runC (Low-level Runtime)                      │    │
│  │                                                                       │    │
│  │  1. Creates OCI bundle (config.json + rootfs)                       │    │
│  │  2. Spawns container process                                         │    │
│  │  3. Configures namespaces and cgroups via `clone()` and `unshare()`  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│         ┌──────────────────────────┼──────────────────────────┐             │
│         │                          │                          │             │
│         ▼                          ▼                          ▼             │
│  ┌─────────────┐            ┌─────────────┐            ┌─────────────┐       │
│  │ Namespaces  │            │  cgroups    │            │ UnionFS     │       │
│  │             │            │             │            │ (OverlayFS) │       │
│  │ ┌─────────┐ │            │ ┌─────────┐ │            │ ┌─────────┐ │       │
│  │ │pid      │ │            │ │cpu      │ │            │ │lowerdir │ │       │
│  │ │net      │ │            │ │memory   │ │            │ │upperdir │ │       │
│  │ │mnt      │ │            │ │blkio    │ │            │ │merged   │ │       │
│  │ │ipc      │ │            │ │device   │ │            │ │workdir  │ │       │
│  │ │uts      │ │            │ │freezer  │ │            │ └─────────┘ │       │
│  │ │user     │ │            │ │pid      │ │            │             │       │
│  │ │cgroup   │ │            │ └─────────┘ │            │             │       │
│  │ └─────────┘ │            └─────────────┘            └─────────────┘       │
│  └─────────────┘            └─────────────┘            └─────────────┘       │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    CONTAINER RUNNING PROCESS                                 │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  • Has PID 1 inside its namespace (may be PID 12345 on host)        │    │
│  │  • Sees private /dev, /proc, /sys                                   │    │
│  │  • Has virtual eth0 with isolated IP                                 │    │
│  │  • Writes go to upperdir (container layer)                          │    │
│  │  • Reads from merged view (image layers + container layer)          │    │
│  │  • Subject to cgroup limits (can't exceed --memory, --cpus)         │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
          ┌───────────────────────────┼───────────────────────────┐
          │                           │                           │
          ▼                           ▼                           ▼
┌─────────────────┐     ┌─────────────────────────┐     ┌─────────────────────┐
│  IMAGE REGISTRY │     │   VOLUME (Persistent)    │     │   NETWORK           │
│  (Docker Hub)   │     │                           │     │                     │
│                 │     │  • Stored on host         │     │  • Bridge (docker0) │
│  ┌───────────┐  │     │  • Survives container    │     │  • Overlay (VXLAN)  │
│  │ Images    │  │     │  • Bind mounts / named   │     │  • Macvlan          │
│  │(layers)   │◄─┼─────┤                           │     │  • Host (no netns)  │
│  └───────────┘  │     └─────────────────────────┘     └─────────────────────┘
└─────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | Docker Component |
|---|------------------|-----------------|------------------|
| 1 | Client-Server Architecture | Separate CLI from background daemon | `docker` CLI + `dockerd` |
| 2 | Namespace Isolation (7 types) | Process isolation, resource visibility | Linux kernel, runC |
| 3 | Cgroup Resource Control | CPU, memory, I/O limits | Linux kernel, runC |
| 4 | Union Filesystem (OverlayFS) | Image layering, copy-on-write | Storage driver |
| 5 | Copy-on-Write (CoW) | Defer file copying until modification | OverlayFS copy_up |
| 6 | Content-Addressable Storage | Identify layers by hash | Registry, image storage |
| 7 | Container Network Model (CNM) | Abstracted, pluggable networking | libnetwork |
| 8 | VXLAN Overlay Networking | Multi-host container communication | Overlay network driver |
| 9 | Copy-up Operation | First write file copy from lower to upper | OverlayFS |
| 10 | Whiteout Files | Represent file deletion in overlay | OverlayFS |
| 11 | OCI Image Specification | Standardized image format | Image distribution |
| 12 | Registry Manifest | Layer listing and metadata | Registry API |
| 13 | containerd-shim | Daemon-independent container management | containerd |
| 14 | Network Namespace Sandbox | Container network isolation | CNM Sandbox |
| 15 | veth Pair Bridging | Connect container to host network | Bridge network driver |
| 16 | cgroup v2 Unified Hierarchy | Single filesystem tree for all controllers | cgroup driver |

---

## Configuration Reference

### Daemon Configuration (`/etc/docker/daemon.json`)

```json
{
  "storage-driver": "overlay2",
  "storage-opts": ["overlay2.size=20G"],
  "exec-opts": ["native.cgroupdriver=systemd"],
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  },
  "insecure-registries": ["myregistry.local:5000"],
  "registry-mirrors": ["https://mirror.gcr.io"],
  "live-restore": true,
  "default-address-pools": [
    {
      "base": "172.17.0.0/16",
      "size": 24
    }
  ]
}
```

### Resource Limits (per container)

```bash
# CPU limits
docker run --cpus=2.5                     # Use 2.5 CPU cores
docker run --cpuset-cpus=0-3              # Only use cores 0,1,2,3
docker run --cpu-shares=512               # Weighted share (default 1024)

# Memory limits
docker run --memory=512m                  # Hard limit 512 MB
docker run --memory-swap=1g               # Memory + swap limit
docker run --memory-reservation=256m      # Soft limit

# I/O limits
docker run --device-read-bps=/dev/sda:1mb
docker run --device-write-iops=/dev/sda:100

# Process limits
docker run --pids-limit=100
```

### Network Configuration

```bash
# Create bridge network
docker network create --driver bridge --subnet=172.20.0.0/16 my-net

# Create overlay network (requires Swarm)
docker network create --driver overlay --attachable my-overlay

# Run container with specific network
docker run --network my-net nginx
```

### Storage Driver Verification

```bash
# Check current storage driver
docker info | grep -A5 "Storage Driver"

# Expected output for overlay2:
# Storage Driver: overlay2
#  Backing Filesystem: extfs
#  Supports d_type: true
#  Native Overlay Diff: true
```

---

## Performance & Complexity Reference

| Operation | Complexity | Typical Performance | Notes |
|-----------|------------|---------------------|-------|
| Container start (cold) | O(layers) + copy_up | ~0.5-2 seconds | Includes filesystem setup |
| Container start (warm) | O(1) namespaces | ~100-300 ms | Already has filesystem |
| Image pull (new) | O(layers) network | Depends on layer size | Layers downloaded in parallel |
| Image pull (cached) | O(1) hash check | ~0.1-0.5 seconds | Only manifest download |
| First write to file | O(file size) copy_up | Large file penalty | Entire file copied |
| Subsequent writes | O(block size) | ~10-50 µs | Already in upperdir |
| Docker build (cache hit) | O(unchanged layers) | ~0.1 seconds per layer | Skips re-execution |
| Container pause | O(process count) freezing | ~10-100 ms | Freezer cgroup |
| Overlay network (VXLAN) | O(encapsulation overhead) | ~10-30% throughput loss | Plus latency increase |

---

## Comparison to Virtual Machines

| Feature | Containers (Docker) | Virtual Machines |
|---------|---------------------|------------------|
| **Kernel** | Shared host kernel | Each VM has own kernel |
| **Isolation** | Namespaces (process-level) | Hardware virtualization |
| **Startup time** | ~100-500 ms | ~30-90 seconds |
| **Disk usage** | MB per container (shared base) | GB per VM (full OS) |
| **Memory overhead** | Low (process only) | High (guest OS + apps) |
| **Performance** | Near-native | Moderate virtualization overhead |
| **Portability** | Requires same OS kernel family | Any OS can run on any hypervisor |
| **Security** | Moderate (escape possible) | Strong (hardware isolation) |
| **Use cases** | Microservices, dev/test, CI/CD | Full OS isolation, Windows/Linux mixed |

---

## Source Code Reference

| Component | Repository |
|-----------|------------|
| Docker CLI | `docker/cli` |
| Docker Engine (daemon) | `moby/moby` |
| containerd | `containerd/containerd` |
| runC | `opencontainers/runc` |
| libnetwork | `moby/libnetwork` |
| OverlayFS driver | `moby/moby/daemon/graphdriver/overlay2/` |

---

## Conclusion

Docker's design philosophy emphasizes:

- **Portability**: Build once, run anywhere (Linux, Windows, cloud, on-prem)
- **Layered efficiency**: Union filesystem for storage optimization and reuse
- **Isolation through kernel features**: Namespaces for visibility, cgroups for resource limits
- **Developer experience**: Simple CLI, Dockerfile abstraction, Compose for multi-container apps
- **Extensibility**: CNM for networking, storage drivers, registry API for distribution

Key innovations and patterns include:
- **Union filesystem with copy-on-write**: Immutable images with container-specific writable layers
- **OverlayFS multi-layer support (overlay2)**: Up to 128 lower layers, efficient inode usage
- **Copy-up operation**: On-first-write file copying, enabling read-mostly workloads
- **Container Network Model (CNM)**: Pluggable networking with bridge, overlay, macvlan drivers
- **Namespace isolation**: Seven namespaces providing comprehensive process isolation
- **Cgroup resource control**: Fine-grained CPU, memory, I/O, device, and process limits
- **Content-addressable storage**: Cryptographic hash-based layer deduplication and integrity
- **containerd architecture**: Shim processes for daemon-restart tolerant container management

This combination of Linux kernel features and container-specific abstractions makes Docker suitable for:
- **Microservices deployment**: Isolated services with minimal overhead
- **Development environments**: Consistent runtime across team members
- **CI/CD pipelines**: Ephemeral, reproducible build environments
- **Application packaging**: "Build once, run anywhere" portability
- **Edge and IoT**: Lightweight runtime on resource-constrained devices
- **Multi-cloud deployments**: Consistent operations across AWS, Azure, GCP

---

*Document Version: 1.0*
*Based on Docker documentation, OCI specifications, and kernel namespace/cgroup analysis*
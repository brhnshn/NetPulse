# NetPulse Architecture Guide

NetPulse is built using clean, decoupled design patterns to separate core network logic, infrastructure operations, and the user interface. It focuses heavily on non-blocking asynchronous execution, event-driven updates, and strict thread safety.

---

## Architectural Layers

NetPulse is split into three main logical layers:

```
┌─────────────────────────────────────────────────────────┐
│                        UI Layer                         │
│   (MainForm, SettingsForm, Custom Controls - KpiCard)   │
└───────────────────────────┬─────────────────────────────┘
                            │ (Subscribes to events)
                            ▼
┌─────────────────────────────────────────────────────────┐
│                       Core Layer                        │
│ (PingEngine, IPingProvider, CircuitBreaker, Fallback)   │
└───────────────────────────┬─────────────────────────────┘
                            │ (Performs IO / Utilities)
                            ▼
┌─────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                   │
│   (LogWriter, ConfigManager, Export, Notifications)    │
└─────────────────────────────────────────────────────────┘
```

### 1. Core Layer (The Engine)
- **`IPingProvider` & `PingProvider`**: Abstraction around System.Net pings to allow testing. Resolves hosts asynchronously and returns wrapped replies.
- **`PingEngine`**: Manages the main monitoring thread loop. Calculates interval execution times and corrects for delay drift. Fires events (`OnPingCompleted`, `OnConnectionDropped`, `OnConnectionRestored`, etc.) in a fire-and-forget or background task manner.
- **`CircuitBreaker`**: A thread-safe state machine tracking consecutive failures per target IP. If errors reach 10, it trips open to skip pings for 60s.
- **`FallbackChecker`**: Initiates parallel TCP connect (port 53) and HTTP GET requests when a target fails 3 times in a row, generating detailed diagnostics.

### 2. Infrastructure Layer
- **`ConfigManager`**: Reads and writes settings to `config.json`. Employs a `FileSystemWatcher` to trigger configuration reloads dynamically without restarting.
- **`LogWriter`**: Writes JSON-formatted events. Signs lines with HMAC-SHA256. Utilizes a shared `SemaphoreSlim` to allow safe, multi-threaded appending. Uses `FileOptions.WriteThrough` to guarantee physical disk writes on critical drops.
- **`ExportService`**: Reads json files and reformats them into CSV or human-readable TXT reports, locking the log writer's `SemaphoreSlim` to prevent read/write conflicts.
- **`NotificationService`**: Fires UWP Toast Notifications, automatically falling back to system tray balloons if the operating system lacks permissions.

### 3. UI Layer
- **`MainForm`**: The central dark-mode dashboard. Captures engine events and dispatches updates to the grid, chart, and KPI cards.
- **`KpiCard`**: A custom dashboard element containing layout labels, colored state values, and a built-in flashing timer for critical alarms.
- **`SettingsForm`**: A modal configuration editor with validation.

---

## Threading & Concurrency Model

A primary design goal of NetPulse is that **the UI thread must never lock or lag**. To achieve this, the following concurrency rules are enforced:

1. **Non-Blocking I/O**: All network pings, TCP connections, HTTP GETs, config file writes, log appends, and exports are executed asynchronously (`async/await` and task-based background loops).
2. **BeginInvoke Marshalling**: WinForms controls are not thread-safe. When `PingEngine` dispatches events from its background monitoring thread, `MainForm` intercepts them and uses `Control.BeginInvoke` to marshal the UI updates back to the UI thread safely, avoiding cross-thread exceptions.
3. **Shared Log Access Protection**: To prevent file locks and conflicts when exporting logs while the engine is actively writing, both `LogWriter` and `ExportService` coordinate using a shared `SemaphoreSlim` instance.
4. **Fire-and-Forget Diagnostics**: Fallback tests are run in detached background tasks, ensuring that diagnostics latency does not slow down the sub-second ping loop.

---

## Event Execution Flow Diagram

The diagram below details the sequence of operations for a single ping interval:

```
[PingEngine Loop] ────► Ping targets in parallel (IPingProvider)
                            │
                            ├─► [Success]
                            │     │
                            │     ├─► RecordSuccess (CircuitBreaker)
                            │     ├─► Dispatch OnPingCompleted
                            │     └─► [If previously dropped] ──► Dispatch OnConnectionRestored
                            │
                            └─► [Failure]
                                  │
                                  ├─► RecordFailure (CircuitBreaker)
                                  ├─► Dispatch OnPingCompleted
                                  ├─► [If first failure] ────► Dispatch OnConnectionDropped
                                  ├─► [If failure count == 3] ─► Launch FallbackChecker (Background)
                                  └─► [If failure count == 10] ─► Trip CircuitBreaker (Pause 60s)
```
---

# Large-Scale Text Sorter & Test File Generator

This solution contains two console applications in one Visual Studio solution:

1. **Generator** — produces a large text file of the form
   `<Number>. <String>`
   (for example, `415. Apple`).
   You can control file size, duplicate rates, parallelism, I/O buffering, etc.

2. **Sorter** — performs an external-merge sort on a huge text file (≈100 GB)
   according to these rules:

   1. Sort by the string part (alphabetically, case-insensitive).
   2. If two lines have identical strings, sort by the leading number (ascending).

---

## 📦 Configuration

Both apps read their settings from `appsettings.json` (or environment-overrides).

### Sorter

```json
"Sorter": {
  "InputPath":       "big.txt",
  "OutputPath":      "sorted.txt",
  "TempDirectory":   "runs",
  "ChunkSizeMb":     128,
  "Degree":          12
}
```

| Setting         | Type     | Description                                                               |
| --------------- | -------- | ------------------------------------------------------------------------- |
| `InputPath`     | `string` | Path to the input file to sort.                                           |
| `OutputPath`    | `string` | Path where the fully sorted file will be written.                         |
| `TempDirectory` | `string` | Directory for intermediate “run\_\*.tmp” files during external merge.     |
| `ChunkSizeMb`   | `int`    | Maximum size (in MB) of each in-memory chunk before it’s flushed to disk. |
| `Degree`        | `int`    | Degree of parallelism: number of concurrent sort-and-merge workers.       |

### Generator

```json
"Storage": {
  "OutputPath":          "data/big.txt",

  "OutputPathPattern":   "data/big-part-{0}.tmp",

  "PartitionCount":      16,

  "TargetSize":          "10GB",

  "BufferSize":          1048576,

  "WordListPath":        "words.json",

  "MaxNumber":           1000000
},

"Pipeline": {
  "DuplicatePercentage": 15,

  "ChannelCapacity":     65536,

  "FlushIntervalSec":    5,

  "RetryCount":          3,

  "WorkerCount":         32,

  "BatchWrite":          128
}
```

#### Storage

| Setting             | Type     | Description                                                                   |
| ------------------- | -------- | ----------------------------------------------------------------------------- |
| `OutputPath`        | `string` | Final combined output file path.                                              |
| `OutputPathPattern` | `string` | Pattern for intermediate partitions, e.g. `big-part-0.tmp`, `big-part-1.tmp`. |
| `PartitionCount`    | `int`    | Number of partitions (files) to generate in parallel.                         |
| `TargetSize`        | `string` | Desired total size of the output (e.g. `"10GB"`, `"500MB"`).                  |
| `BufferSize`        | `int`    | Size of the internal file-stream buffer, in bytes (1 MiB = 1048576).          |
| `WordListPath`      | `string` | Path to a JSON file containing an array of words to use for the string part.  |
| `MaxNumber`         | `int`    | Maximum random integer prefix for each line (0…MaxNumber).                    |

#### Pipeline

| Setting               | Type  | Description                                                                          |
| --------------------- | ----- | ------------------------------------------------------------------------------------ |
| `DuplicatePercentage` | `int` | Percentage (0–100) of lines that should be duplicates of previously generated lines. |
| `ChannelCapacity`     | `int` | Bounded channel capacity between producer and consumer tasks.                        |
| `FlushIntervalSec`    | `int` | Period (in seconds) to flush buffered writes to disk.                                |
| `RetryCount`          | `int` | Number of retries on transient I/O errors.                                           |
| `WorkerCount`         | `int` | Number of parallel producer threads generating lines.                                |
| `BatchWrite`          | `int` | How many lines to accumulate in-memory before writing them out in one batch.         |

---

## 🚀 Quick Start

1. **Clone** this repository.
2. **Configure** `appsettings.json` for each project (see above).
3. **Build** and **run**:

   ```bash
   # Generate a test file
   dotnet run --project src/Generator/Generator.csproj

   # Sort the generated file
   dotnet run --project src/Sorter/Sorter.csproj
   ```
4. **Monitor** the console logs (via Serilog), adjust settings as needed.

---

## 🛠️ Implementation Notes

* The **Generator** uses a pipeline of producers and batch writers, with configurable buffering and retries.
* The **Sorter** uses an external-merge strategy: it splits the file into in-memory sorted runs, writes them to disk, and then performs a multi-way merge.

---

### ⚙️ Architecture Note: Why Channels?

This sorting engine uses [`System.Threading.Channels`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel) as a core part of its internal pipeline architecture. Channels enable **asynchronous, lock-free, and highly efficient** communication between producer and consumer components.

#### ✅ Benefits of using `Channel<T>`:

- **Asynchronous Producer-Consumer Pattern**  
  Channels allow chunks to be generated and sorted in parallel without blocking threads. This improves performance and responsiveness, especially with large files.

- **Built-in Backpressure**  
  Using `BoundedChannelOptions` with `FullMode = Wait` ensures the system slows down gracefully if sorters can't keep up, preventing out-of-memory scenarios.

- **Clear Separation of Concerns**  
  - `ChunkProducer` focuses only on reading and parsing the input file.  
  - `RunSorter` handles sorting and writing runs to disk.  
  - Communication happens via `Channel<Chunk>`, with no shared mutable state.

- **Scalable Parallelism**  
  You can increase the degree of parallelism (`Degree` option) without touching core logic, thanks to channel-driven isolation.

- **Minimal Memory Overhead**  
  Channels avoid the allocation and locking costs of traditional queues and allow low-level control over buffering.

This makes `Channel<T>` the optimal choice for building a robust, scalable, and memory-safe sorting pipeline — especially when working with large datasets and asynchronous IO.


---

## 📄 License

MIT License — see [LICENSE](LICENSE).

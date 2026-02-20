## 2025-01-24 - [Avalonia Image Memory Optimization]
**Learning:** In Avalonia UI applications that display lists of images, binding `Image.Source` directly to a file path string causes the full-resolution image to be decoded into memory. This leads to massive RAM usage (tens of MBs per image) and poor scrolling performance. Using `Bitmap.DecodeToWidth(stream, width)` in a converter reduces memory footprint by >95% for 24MP photos.
**Action:** Always use a custom converter with `Bitmap.DecodeToWidth` or `Bitmap.DecodeToHeight` for image previews in list controls to ensure memory efficiency.

## 2025-01-24 - [Dataset Generation I/O Optimization]
**Learning:** Creating archives by copying files to a temporary directory before zipping (`Read -> Write (Temp) -> Read (Temp) -> Write (Zip)`) is highly inefficient for large datasets. Streaming files directly into `ZipArchive` entries (`Read -> Write (Zip)`) reduces Disk I/O by ~50% and eliminates temporary disk space requirements.
**Action:** Stream files directly from source to target archive whenever possible, avoiding intermediate temporary files.

## 2025-01-24 - [Batch I/O and Processing Parallelization]
**Learning:** Sequential file operations (like saving individual caption files) and manual Task management (like `Select` + `Task.WhenAll` with `SemaphoreSlim`) can be improved using `Parallel.ForEachAsync`. This provides better I/O throughput for small files on SSDs and more memory-efficient task management by not creating all Task objects upfront.
**Action:** Use `Parallel.ForEachAsync` for batch I/O and concurrent API processing to improve throughput and reduce task management overhead.

## 2025-01-24 - [JSON Serialization and ZIP Compression]
**Learning:** JsonSerializerOptions instances should be cached and reused to avoid reflection overhead on every API call. Additionally, compressing already-compressed files like JPEGs in a ZIP archive with 'Fastest' or 'Optimal' levels is a waste of CPU; 'NoCompression' is significantly faster for large datasets.
**Action:** Always use static readonly JsonSerializerOptions in API clients and NoCompression for media files in archives.

## 2025-01-24 - [Efficient Large Binary Data Handling in JSON APIs]
**Learning:** Manual base64 conversion of large images before JSON serialization is inefficient. `System.Text.Json` can serialize `byte[]` directly to base64 into its internal buffer, avoiding a large intermediate string allocation. For cases where a data URI is required (e.g., OpenAI compatible APIs), using `string.Create` with `Convert.TryToBase64Chars` builds the final string in a single allocation, further reducing memory pressure.
**Action:** Pass `byte[]` directly to `JsonSerializer` when possible, or use `string.Create` for complex URI construction with binary data to minimize RAM usage.

## 2025-01-24 - [Zero-Allocation String Formatting and Metadata Caching]
**Learning:** Even with modern C# interpolation, creating strings in a loop (like entry names in a ZIP archive) still incurs allocation and formatting overhead. Using `string.Create` with `TryFormat` and `Span<char>` provides zero-allocation formatting for the template part of the string. Additionally, caching metadata like file extensions in the Model during discovery avoids redundant `Path` parsing and string allocations in later processing stages.
**Action:** Use `string.Create` for hot-path string construction and cache derived file properties in models if they are needed more than once.

## 2025-02-12 - [Source-Generated JSON Context Maintenance]
**Learning:** When using .NET source generators for JSON serialization (`JsonSerializerContext`), ALL nested types used in serialization must be explicitly registered with `[JsonSerializable]`. Missing nested types can lead to "type not found" errors or runtime failures during reflection-free execution. Additionally, keeping the context in a central service file while referencing it from API clients requires careful namespace management.
**Action:** Always register the full hierarchy of request/response models in `AppJsonContext.cs` and ensure proper `using` directives in clients.

## 2025-02-14 - [Memory Optimization for Large Object Collections]
**Learning:** In applications handling thousands of items (like image lists), making every item model an `ObservableObject` adds significant memory overhead (events, delegates, base class state). Keeping the Model as a POCO and wrapping it in an `ObservableObject` ViewModel is more efficient. Additionally, calling `.ToString()` on repeated values (like file extensions) during discovery creates thousands of identical string objects. Canonical interning (returning static literals for common values) eliminates these redundant allocations.
**Action:** Use POCO models for large collections and implement canonical string interning for repeated metadata during data discovery.

## 2025-05-15 - [API Client and Data Export Optimizations]
**Learning:** Using `JsonElement` for API response parsing is flexible but inefficient due to string-based property lookups and DOM overhead. Typed `record` models with source-generated `JsonSerializerContext` provide faster, AOT-friendly deserialization. Additionally, caching `Uri` objects and pre-calculating loop-invariant strings (like format strings or Data URI prefixes) significantly reduces allocations in high-frequency paths.
**Action:** Always prefer typed response models for APIs and hoist string construction/formatting out of loops whenever the result is invariant.

## 2025-05-20 - [32-bit Signature Detection and Stream Buffering]
**Learning:** Performing multiple 8-bit comparisons for file signature detection is less efficient than a single 32-bit comparison using `BinaryPrimitives.ReadUInt32BigEndian`. Additionally, `ZipArchive` creates many small write operations during metadata generation; wrapping the output stream in a `BufferedStream` with a large buffer (e.g., 128KB) significantly reduces syscall overhead.
**Action:** Use 32-bit signature checks for headers and always wrap high-frequency/small-write I/O streams in a `BufferedStream`.

## 2025-06-15 - [Sequential I/O and StreamWriter Allocation]
**Learning:** For high-throughput file reading (like dataset zipping), specifying `FileOptions.SequentialScan` and `FileOptions.Asynchronous` in `FileStream` improves performance by hinting read-ahead to the OS. Furthermore, using `StreamWriter` in a tight loop for small string writes to zip entries adds unnecessary object allocation and buffering overhead; direct byte writing with `Encoding.UTF8.GetBytes` is more efficient.
**Action:** Use `FileOptions.SequentialScan` for sequential reads and avoid `StreamWriter` for frequent small writes to streams.

## 2025-07-20 - [Zero-Allocation Caption Writing with ArrayPool]
**Learning:** Even when avoiding `StreamWriter`, calling `Encoding.UTF8.GetBytes(string)` still allocates a new `byte[]` for every call. In high-frequency loops (like exporting thousands of captions to a ZIP archive), these allocations add up and increase GC pressure. Using `ArrayPool<byte>.Shared.Rent` combined with the `Span`-based `Encoding.UTF8.GetBytes` overload eliminates these redundant allocations.
**Action:** Use `ArrayPool<byte>` for string-to-byte conversions in high-frequency loops to achieve zero-allocation data processing.

## 2025-08-15 - [Collection Materialization and Compression Tuning]
**Learning:** Iterating over an `ObservableCollection` using an indexer or enumerator in a hot loop (like dataset export) incurs virtual call overhead. Materializing the collection to an array (`.ToArray()`) provides a stable snapshot and faster access. Additionally, for very small text files (captions), `CompressionLevel.Optimal` is CPU-heavy with negligible space benefit compared to `CompressionLevel.Fastest`.
**Action:** Always materialize UI collections to arrays before batch processing and use `Fastest` compression for small text metadata in archives.
- Performance Pattern: For individual caption saving, use ArrayPool<byte>.Shared and File.WriteAllBytesAsync to minimize allocations and improve I/O efficiency.
- UI Pattern: In Avalonia, use WrapPanel in ListBox.ItemsPanel for responsive grid layouts. Use Styles with Transitions for smooth hover effects without layout shifts.

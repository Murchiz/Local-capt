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

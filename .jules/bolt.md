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

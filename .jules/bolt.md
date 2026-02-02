## 2025-01-24 - [Avalonia Image Memory Optimization]
**Learning:** In Avalonia UI applications that display lists of images, binding `Image.Source` directly to a file path string causes the full-resolution image to be decoded into memory. This leads to massive RAM usage (tens of MBs per image) and poor scrolling performance. Using `Bitmap.DecodeToWidth(stream, width)` in a converter reduces memory footprint by >95% for 24MP photos.
**Action:** Always use a custom converter with `Bitmap.DecodeToWidth` or `Bitmap.DecodeToHeight` for image previews in list controls to ensure memory efficiency.

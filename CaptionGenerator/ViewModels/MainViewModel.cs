using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CaptionGenerator.ApiClients;
using CaptionGenerator.Models;
using CaptionGenerator.Services;
using CaptionGenerator.Views;

namespace CaptionGenerator.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private Settings _settings = new();
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _errorDialogSemaphore = new(1, 1);

    [ObservableProperty]
    private ObservableCollection<PromptTemplateSetting> _promptTemplates = new();

    [ObservableProperty]
    private PromptTemplateSetting? _selectedPromptTemplate;

    [ObservableProperty]
    private string? _assignedModelName;

    [ObservableProperty]
    private ObservableCollection<ImageCaptionViewModel> _imageCaptions = new();

    [ObservableProperty]
    private bool _createDataset;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSaving;

    public IStorageProvider? StorageProvider { get; set; }
    public Window? MainWindow { get; set; }

    public MainViewModel()
    {
        _ = LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadSettingsAsync();
        PromptTemplates = _settings.PromptTemplates;
    }

    partial void OnSelectedPromptTemplateChanged(PromptTemplateSetting? value)
    {
        AssignedModelName = value?.ModelName;
    }

    [RelayCommand]
    private async Task SelectFolder()
    {
        if (StorageProvider is null) return;

        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Image Folder",
            AllowMultiple = false
        });

        if (result is [var folder, ..])
        {
            // ⚡ Bolt Optimization: Offload folder scanning to a background thread and use Parallel.ForEachAsync for caption loading.
            // This prevents UI freezes during large folder discovery and significantly speeds up existing caption loading
            // by parallelizing I/O operations for matching text files.
            var imageFiles = await Task.Run(async () =>
            {
                // ⚡ Bolt Optimization: Cache the LocalPath during discovery to avoid redundant property access on IStorageFile
                // which might involve Uri parsing or platform-specific IPC inside the parallel loop.
                List<(string imagePath, string canonicalExtension)> storageFiles;
                HashSet<string> existingCaptions;

                var localPath = folder.Path.LocalPath;
                if (!string.IsNullOrEmpty(localPath))
                {
                    // ⚡ Bolt Optimization: Use Directory.GetFiles to get an initial count for pre-allocating collections.
                    // This avoids multiple internal array re-allocations as the dataset is discovered.
                    var allFiles = Directory.GetFiles(localPath);
                    storageFiles = new List<(string imagePath, string canonicalExtension)>(allFiles.Length);
                    existingCaptions = new HashSet<string>(allFiles.Length, StringComparer.OrdinalIgnoreCase);

                    // ⚡ Bolt Optimization: Use Directory.GetFiles for faster I/O on local drives.
                    // This avoids the overhead of creating IStorageFile wrappers for every file in the folder.
                    foreach (var filePath in allFiles)
                    {
                        // ⚡ Bolt Optimization: Use filePath.AsSpan() and span-based Path methods to extract
                        // filename and extension without creating temporary string objects.
                        var filePathSpan = filePath.AsSpan();
                        var fileName = Path.GetFileName(filePathSpan);
                        var extension = Path.GetExtension(fileName);

                        if (extension is ['.', 't' or 'T', 'x' or 'X', 't' or 'T'])
                        {
                            existingCaptions.Add(filePath);
                            continue;
                        }

                        var canonicalExtension = GetCanonicalExtension(extension);
                        if (canonicalExtension != null)
                        {
                            storageFiles.Add((filePath, canonicalExtension));
                        }
                    }
                }
                else
                {
                    storageFiles = new List<(string imagePath, string canonicalExtension)>();
                    existingCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Fallback for cloud/virtual storage where LocalPath is not available
                    await foreach (var item in folder.GetItemsAsync())
                    {
                        if (item is IStorageFile file)
                        {
                            // ⚡ Bolt Optimization: Cache LocalPath to avoid redundant property access and URI parsing.
                            var itemLocalPath = file.Path.LocalPath;
                            var fileName = file.Name.AsSpan();
                            var extension = Path.GetExtension(fileName);

                            if (extension is ['.', 't' or 'T', 'x' or 'X', 't' or 'T'])
                            {
                                existingCaptions.Add(itemLocalPath);
                                continue;
                            }

                            var canonicalExtension = GetCanonicalExtension(extension);
                            if (canonicalExtension != null)
                            {
                                storageFiles.Add((itemLocalPath, canonicalExtension));
                            }
                        }
                    }
                }

                var results = new ImageCaptionViewModel[storageFiles.Count];
                // ⚡ Bolt Optimization: Use a higher MaxDegreeOfParallelism for I/O bound tasks like loading many small text files.
                // This saturates SSD I/O and significantly reduces wait time for large folders with existing captions.
                var parallelOptions = new ParallelOptions
                {
                    // ⚡ Bolt Optimization: Scale concurrency based on hardware but ensure a minimum of 16
                    // to effectively saturate I/O on modern SSDs during folder discovery.
                    MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount, 16),
                    CancellationToken = CancellationToken.None // We don't have a CTS here yet
                };
                await Parallel.ForEachAsync(Enumerable.Range(0, storageFiles.Count), parallelOptions, async (i, ct) =>
                {
                    var (imagePath, canonicalExtension) = storageFiles[i];
                    // ⚡ Bolt Optimization: Cache the caption path once during discovery to avoid redundant Path.ChangeExtension calls
                    // and associated string allocations in discovery, individual save, and batch save operations.
                    var captionPath = Path.ChangeExtension(imagePath, ".txt");
                    string caption = "";
                    // ⚡ Bolt Optimization: Use the HashSet for zero-allocation existence check instead of File.Exists syscall.
                    // This eliminates thousands of costly I/O operations, especially on high-latency network storage.
                    if (existingCaptions.Contains(captionPath))
                    {
                        caption = await File.ReadAllTextAsync(captionPath, ct);
                    }

                    results[i] = new ImageCaptionViewModel(new ImageCaption
                    {
                        ImagePath = imagePath,
                        Extension = canonicalExtension,
                        Caption = caption,
                        CaptionPath = captionPath
                    });
                });
                return results;
            });
            ImageCaptions = new ObservableCollection<ImageCaptionViewModel>(imageFiles);
        }
    }

    [RelayCommand]
    private async Task GenerateCaptions()
    {
        // ⚡ Bolt Optimization: Materialize ImageCaptions to an array for faster iteration and better partitioning in Parallel.ForEachAsync.
        // This avoids ObservableCollection's virtual indexer/enumerator overhead in a hot loop and provides a stable snapshot.
        var imageCaptions = ImageCaptions.ToArray();
        int totalCount = imageCaptions.Length;
        if (SelectedPromptTemplate is null || totalCount == 0 || MainWindow is null) return;

        IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();

        var apiEndpoint = _settings.ApiEndpoints.FirstOrDefault(e => e.Name == SelectedPromptTemplate.ModelName);
        if (apiEndpoint is null)
        {
            // Handle error: API endpoint not found
            IsBusy = false;
            return;
        }

        var client = CreateApiClient(apiEndpoint);
        if (client is null)
        {
            // Handle error: Invalid provider
            IsBusy = false;
            return;
        }

        var prompt = SelectedPromptTemplate.Prompt.Replace("{output_format}", SelectedPromptTemplate.OutputFormat);

        try
        {
            if (_settings.EnableAsyncProcessing)
            {
                // ⚡ Bolt Optimization: Use Parallel.ForEachAsync for better task management and reduced overhead.
                // This avoids creating all Tasks upfront and provides a cleaner implementation of concurrency limiting.
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = _cancellationTokenSource.Token
                };

                await Parallel.ForEachAsync(imageCaptions, parallelOptions, async (imageCaption, ct) =>
                {
                    imageCaption.IsProcessing = true;
                    try
                    {
                        var imageData = await File.ReadAllBytesAsync(imageCaption.ImagePath, ct);
                        var generatedCaption = await client.GenerateCaptionAsync(imageData, prompt);
                        // ⚡ Bolt Optimization: Use the public property setter to ensure that AI-generated captions
                        // are correctly flagged as modified relative to the original disk baseline.
                        // This allows them to be included in bulk save operations.
                        imageCaption.Caption = generatedCaption;
                    }
                    catch (Exception ex)
                    {
                        await _errorDialogSemaphore.WaitAsync(ct);
                        try
                        {
                            if (ct.IsCancellationRequested) return;
                            var result = await ErrorDialog.ShowAsync(MainWindow, ex.Message);
                            if (result == ErrorDialogResult.Stop)
                            {
                                _cancellationTokenSource.Cancel();
                            }
                        }
                        finally
                        {
                            _errorDialogSemaphore.Release();
                        }
                    }
                    finally
                    {
                        imageCaption.IsProcessing = false;
                    }
                });
            }
            else
            {
                foreach (var imageCaption in imageCaptions)
                {
                    if (_cancellationTokenSource.IsCancellationRequested) break;

                    imageCaption.IsProcessing = true;
                    try
                    {
                        var imageData = await File.ReadAllBytesAsync(imageCaption.ImagePath, _cancellationTokenSource.Token);
                        var generatedCaption = await client.GenerateCaptionAsync(imageData, prompt);
                        // ⚡ Bolt Optimization: Use the public property setter to ensure that AI-generated captions
                        // are correctly flagged as modified relative to the original disk baseline.
                        // This allows them to be included in bulk save operations.
                        imageCaption.Caption = generatedCaption;
                    }
                    catch (Exception ex)
                    {
                        var result = await ErrorDialog.ShowAsync(MainWindow, ex.Message);
                        if (result == ErrorDialogResult.Stop)
                        {
                            _cancellationTokenSource.Cancel();
                            break;
                        }
                    }
                    finally
                    {
                        imageCaption.IsProcessing = false;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IVisionLanguageModelClient? CreateApiClient(ApiEndpointSetting endpoint)
    {
        return endpoint.Provider switch
        {
            "Ollama" => new ApiClients.OllamaApiClient(endpoint.Url, endpoint.ModelIdentifier),
            "LM Studio" => new ApiClients.OpenAiCompatibleApiClient(endpoint.Url, endpoint.ModelIdentifier),
            "llama.cpp" => new ApiClients.OpenAiCompatibleApiClient(endpoint.Url, endpoint.ModelIdentifier),
            "Oobabooga" => new ApiClients.OpenAiCompatibleApiClient(endpoint.Url, endpoint.ModelIdentifier),
            _ => null
        };
    }

    [RelayCommand]
    private async Task SaveCaptions()
    {
        // ⚡ Bolt Optimization: Materialize ImageCaptions to an array for faster iteration and indexing.
        // This avoids ObservableCollection's virtual indexer/enumerator overhead in a hot loop and provides a stable snapshot.
        var imageCaptions = ImageCaptions.ToArray();
        int totalCount = imageCaptions.Length;
        if (totalCount == 0 || StorageProvider is null) return;

        IsSaving = true;

        if (CreateDataset)
        {
            var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Dataset Archive",
                SuggestedFileName = "Dataset.zip",
                DefaultExtension = ".zip"
            });

            if (result is not null)
            {
                // ⚡ Bolt Optimization: Zip directly to the output stream to avoid redundant I/O and temp files.
                // This reduces disk I/O by ~50% and uses zero temporary disk space.
                // We also prompt for the file location BEFORE doing any work.
                try
                {
                    using var zipStream = await result.OpenWriteAsync();
                    // ⚡ Bolt Optimization: Wrap the output stream in a BufferedStream to improve write performance.
                    // This reduces the number of small write operations to the underlying file stream, especially beneficial for archives.
                    using var bufferedStream = new BufferedStream(zipStream, 131072);
                    using var archive = new ZipArchive(bufferedStream, ZipArchiveMode.Create);

                    // ⚡ Bolt Optimization: Pre-calculate indexLength outside the loop to avoid redundant math operations for every file.
                    // This also ensures consistent zero-padding for all files in the dataset.
                    int maxIndex = totalCount > 0 ? totalCount - 1 : 0;
                    int indexLength = Math.Max(3, maxIndex < 1 ? 1 : (int)Math.Floor(Math.Log10(maxIndex)) + 1);
                    string format = "D" + indexLength;

                    for (var i = 0; i < totalCount; i++)
                    {
                        var imageCaption = imageCaptions[i];
                        var index = i;

                        // ⚡ Bolt Optimization: Build the entry name in a single allocation using string.Create.
                        // This avoids multiple intermediate strings and interpolation overhead.
                        var imageEntryName = string.Create(indexLength + imageCaption.Extension.Length, (index, imageCaption.Extension, indexLength, format), (span, state) =>
                        {
                            state.index.TryFormat(span[..state.indexLength], out _, state.format);
                            state.Extension.AsSpan().CopyTo(span[state.indexLength..]);
                        });

                        // Add image entry
                        // ⚡ Bolt Optimization: Use NoCompression for images as they are already compressed (JPEG/PNG).
                        // This saves CPU cycles and speeds up archive creation significantly for large datasets.
                        var imageEntry = archive.CreateEntry(imageEntryName, CompressionLevel.NoCompression);
                        using (var entryStream = imageEntry.Open())
                        // ⚡ Bolt Optimization: Use FileStream with SequentialScan and Asynchronous options to improve read throughput.
                        // Standardizing on a 128KB buffer size aligns with our output stream buffering for optimal performance.
                        using (var fileStream = new FileStream(imageCaption.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            await fileStream.CopyToAsync(entryStream, 131072);
                        }

                        var captionEntryName = string.Create(indexLength + 4, (index, indexLength, format), (span, state) =>
                        {
                            state.index.TryFormat(span[..state.indexLength], out _, state.format);
                            // ⚡ Bolt Optimization: Use direct character assignment for small constant suffixes instead of .AsSpan().CopyTo().
                            // This avoids span slicing and copy overhead in high-frequency archival loops.
                            span[state.indexLength] = '.';
                            span[state.indexLength + 1] = 't';
                            span[state.indexLength + 2] = 'x';
                            span[state.indexLength + 3] = 't';
                        });

                        // Add caption entry
                        // ⚡ Bolt Optimization: Use Fastest compression for text files.
                        // For typical short captions, the space saving of 'Optimal' is negligible, but 'Fastest' reduces CPU overhead.
                        var captionEntry = archive.CreateEntry(captionEntryName, CompressionLevel.Fastest);
                        using (var entryStream = captionEntry.Open())
                        {
                            // ⚡ Bolt Optimization: Use ArrayPool to avoid byte[] allocations for every caption.
                            // Combined with GetMaxByteCount, this achieves zero-allocation encoding and avoids an extra pass over the string.
                            // This reduces memory pressure and CPU overhead during large dataset creation.
                            var captionSpan = imageCaption.Caption.AsSpan();
                            int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(captionSpan.Length);
                            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                            try
                            {
                                int written = System.Text.Encoding.UTF8.GetBytes(captionSpan, rentedBuffer);
                                await entryStream.WriteAsync(rentedBuffer.AsMemory(0, written));
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(rentedBuffer);
                            }
                        }
                        imageCaption.MarkAsPersisted();
                    }
                }
                catch (Exception ex)
                {
                    if (MainWindow is not null)
                    {
                        await ErrorDialog.ShowAsync(MainWindow, $"Error saving dataset: {ex.Message}");
                    }
                }
            }
        }
        else
        {
            // ⚡ Bolt Optimization: Filter the collection to only include modified items.
            // This eliminates redundant disk I/O for unchanged captions, making saving nearly instantaneous for large datasets.
            var modifiedCaptions = imageCaptions.Where(ic => ic.IsModified).ToArray();
            if (modifiedCaptions.Length == 0)
            {
                IsSaving = false;
                return;
            }

            // ⚡ Bolt Optimization: Parallelize saving individual text files to improve I/O throughput.
            // On modern SSDs and network storage, this significantly reduces the time to save large sets of captions.
            // Scaling concurrency based on hardware (minimum 16) ensures optimal throughput for many small I/O operations.
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount, 16) };
            await Parallel.ForEachAsync(modifiedCaptions, parallelOptions, async (imageCaption, ct) =>
            {
                // ⚡ Bolt Optimization: Use the cached CaptionPath to avoid redundant Path.ChangeExtension allocations.
                var captionPath = imageCaption.CaptionPath;

                // ⚡ Bolt Optimization: Use ArrayPool to avoid byte[] allocations for every caption.
                // This reduces memory pressure and GC overhead during large export operations.
                var captionSpan = imageCaption.Caption.AsSpan();
                int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(captionSpan.Length);
                byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

                try
                {
                    int written = System.Text.Encoding.UTF8.GetBytes(captionSpan, rentedBuffer);
                    // ⚡ Bolt Optimization: Use File.WriteAllBytesAsync for efficient asynchronous writing of the encoded caption.
                    await File.WriteAllBytesAsync(captionPath, rentedBuffer.AsMemory(0, written), ct);
                    imageCaption.MarkAsPersisted();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            });
        }

        IsSaving = false;
    }

    private static string? GetCanonicalExtension(ReadOnlySpan<char> extension)
    {
        // ⚡ Bolt Optimization: Use C# 11 list pattern matching on ReadOnlySpan<char> for zero-allocation extension filtering.
        // Reordering cases to put most common formats (.jpg, .png, .webp) first for faster matching in the jump table.
        // This replaces manual bitwise normalization and multiple 'if' statements with a highly optimized decision tree,
        // improving readability and performance during large folder discovery.
        return extension switch
        {
            ['.', 'j' or 'J', 'p' or 'P', 'g' or 'G'] => ".jpg",
            ['.', 'p' or 'P', 'n' or 'N', 'g' or 'G'] => ".png",
            ['.', 'w' or 'W', 'e' or 'E', 'b' or 'B', 'p' or 'P'] => ".webp",
            ['.', 'j' or 'J', 'p' or 'P', 'e' or 'E', 'g' or 'G'] => ".jpeg",
            ['.', 'j' or 'J', 'f' or 'F', 'i' or 'I', 'f' or 'F'] => ".jfif",
            ['.', 'b' or 'B', 'm' or 'M', 'p' or 'P'] => ".bmp",
            _ => null
        };
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        if (MainWindow is null) return;

        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settingsService)
        };
        await settingsWindow.ShowDialog(MainWindow);
        await LoadSettingsAsync();
    }
}

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
            // ⚡ Bolt Optimization: Offload folder scanning to a background thread and use Span-based extension checking.
            // This prevents UI freezes during large folder discovery and reduces per-file allocations.
            var imageFiles = await Task.Run(async () =>
            {
                var files = new List<ImageCaptionViewModel>();
                await foreach (var item in folder.GetItemsAsync())
                {
                    if (item is IStorageFile file)
                    {
                        var fileName = file.Name.AsSpan();
                        var extension = Path.GetExtension(fileName);
                        var canonicalExtension = GetCanonicalExtension(extension);
                        if (canonicalExtension != null)
                        {
                            files.Add(new ImageCaptionViewModel(new ImageCaption
                            {
                                ImagePath = file.Path.LocalPath,
                                Extension = canonicalExtension
                            }));
                        }
                    }
                }
                return files;
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
                        imageCaption.Caption = await client.GenerateCaptionAsync(imageData, prompt);
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
                        imageCaption.Caption = await client.GenerateCaptionAsync(imageData, prompt);
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
                            // ⚡ Bolt Optimization: Direct character assignment for the .txt suffix.
                            // This avoids the overhead of AsSpan() and CopyTo() for a small constant suffix.
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
                            // ⚡ Bolt Optimization: Use ArrayPool with GetMaxByteCount to avoid byte[] allocations and eliminate the redundant GetByteCount pass.
                            // This reduces CPU cycles and GC pressure by performing a single pass over the caption string.
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
            // ⚡ Bolt Optimization: Parallelize saving individual text files to improve I/O throughput.
            // On modern SSDs and network storage, this significantly reduces the time to save large sets of captions.
            await Parallel.ForEachAsync(imageCaptions, async (imageCaption, ct) =>
            {
                var captionPath = Path.ChangeExtension(imageCaption.ImagePath, ".txt");

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
        // ⚡ Bolt Optimization: Use bitwise character comparisons for extension interning.
        // This achieves zero-allocation, zero-call case-insensitive validation by avoiding Slice and Equals calls.
        // Standardizing on static literals eliminates redundant string allocations during discovery.
        if (extension.Length is < 4 or > 5 || extension[0] != '.') return null;

        return extension.Length switch
        {
            4 => (extension[1] | 0x20) switch
            {
                'j' when (extension[2] | 0x20) == 'p' && (extension[3] | 0x20) == 'g' => ".jpg",
                'p' when (extension[2] | 0x20) == 'n' && (extension[3] | 0x20) == 'g' => ".png",
                'b' when (extension[2] | 0x20) == 'm' && (extension[3] | 0x20) == 'p' => ".bmp",
                _ => null
            },
            5 when (extension[1] | 0x20) == 'j' && (extension[2] | 0x20) == 'p' && (extension[3] | 0x20) == 'e' && (extension[4] | 0x20) == 'g' => ".jpeg",
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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace ComVis.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _modelPath = "Модель не выбрана";

    [ObservableProperty]
    private string _mediaPath = "Файл не выбран";

    [ObservableProperty]
    private string _metricsResult = "Здесь появятся результаты метрик...";

    private string? _selectedModelFilePath;
    private string? _selectedMediaFilePath;

    [RelayCommand]
    private async Task SelectModelAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите ONNX модель",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("ONNX файлы") { Patterns = new[] { "*.onnx" } } }
        });

        if (files.Count > 0)
        {
            _selectedModelFilePath = files[0].Path.LocalPath;
            ModelPath = _selectedModelFilePath;
        }
    }

    [RelayCommand]
    private async Task SelectMediaAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение или видео",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Медиа файлы") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.mp4", "*.avi" } } }
        });

        if (files.Count > 0)
        {
            _selectedMediaFilePath = files[0].Path.LocalPath;
            MediaPath = _selectedMediaFilePath;
        }
    }

    [RelayCommand]
    private async Task RunInferenceAsync()
    {
        if (string.IsNullOrEmpty(_selectedModelFilePath) || !File.Exists(_selectedModelFilePath))
        {
            MetricsResult = "Ошибка: Выберите корректную ONNX модель!";
            return;
        }

        if (string.IsNullOrEmpty(_selectedMediaFilePath) || !File.Exists(_selectedMediaFilePath))
        {
            MetricsResult = "Ошибка: Выберите медиафайл!";
            return;
        }

        MetricsResult = "Запуск инференса...";

        await Task.Run(() =>
        {
            try
            {
                using var session = new InferenceSession(_selectedModelFilePath);
                string ext = Path.GetExtension(_selectedMediaFilePath).ToLower();
                bool isVideo = ext == ".mp4" || ext == ".avi";

                Stopwatch sw = new Stopwatch();

                if (isVideo)
                {
                    using var capture = new VideoCapture(_selectedMediaFilePath);
                    if (!capture.IsOpened())
                    {
                        MetricsResult = "Не удалось открыть видеофайл.";
                        return;
                    }

                    int frameCount = (int)capture.Get(VideoCaptureProperties.FrameCount);
                    double fps = capture.Get(VideoCaptureProperties.Fps);
                    
                    sw.Start();
                    int processedFrames = 0;
                    using var frame = new Mat();

                    while (capture.Read(frame) && !frame.Empty())
                    {
                        processedFrames++;
                    }
                    sw.Stop();

                    double totalSeconds = sw.Elapsed.TotalSeconds;
                    double avgFps = processedFrames / totalSeconds;

                    MetricsResult = $"[Видео] Обработано кадров: {processedFrames}\n" +
                                    $"Общее время: {totalSeconds:F2} сек\n" +
                                    $"Средний FPS: {avgFps:F2}\n" +
                                    $"Исходный FPS видео: {fps:F2}";
                }
                else
                {
                    using var img = Cv2.ImRead(_selectedMediaFilePath);
                    if (img.Empty())
                    {
                        MetricsResult = "Не удалось загрузить изображение.";
                        return;
                    }

                    sw.Start();
                    sw.Stop();

                    MetricsResult = $"[Изображение] Успешно обработано!\n" +
                                    $"Время инференса: {sw.ElapsedMilliseconds} мс";
                }
            }
            catch (Exception ex)
            {
                MetricsResult = $"Ошибка во время инференса: {ex.Message}";
            }
        });
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
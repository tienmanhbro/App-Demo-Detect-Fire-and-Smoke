using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AppDemo.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty] private BitmapImage? _inputImageSource;
        [ObservableProperty] private BitmapImage? _outputImageSource;
        [ObservableProperty] private MediaSource? _outputVideoSource;
        [ObservableProperty] private bool _isImageOutputVisible;
        [ObservableProperty] private bool _isVideoOutputVisible;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Sẵn sàng";

        public MainViewModel() { }

        [RelayCommand]
        private async Task ProcessFileAsync(StorageFile inputFile)
        {
            if (inputFile == null) return;
            IsBusy = true;
            IsImageOutputVisible = false;
            IsVideoOutputVisible = false;

            // Hiển thị ảnh/video gốc
            await SetInputFileAsync(inputFile);

            StatusMessage = "Đang xử lý, vui lòng đợi...";

            try
            {
                string? resultPath = await RunPythonScriptAndWaitAsync(inputFile.Path);
                if (!string.IsNullOrEmpty(resultPath))
                {
                    StatusMessage = "Đã xử lý xong! Đang hiển thị kết quả...";
                    await LoadResultFileAsync(resultPath);
                    StatusMessage = "Hoàn tất!";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi không xác định: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string?> RunPythonScriptAndWaitAsync(string inputPath)
        {
                string outputDirectory = "D:\\DO AN TOT NGHIEP\\rs";
                Directory.CreateDirectory(outputDirectory);
                string outputFileName = $"result_{Path.GetFileName(inputPath)}";
                string outputFilePath = Path.Combine(outputDirectory, outputFileName);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        // !!! THAY ĐỔI 2 ĐƯỜNG DẪN NÀY CHO ĐÚNG VỚI MÁY CỦA BẠN !!!
                        FileName = @"D:\python\python.exe",
                        Arguments = $"\"D:\\myapps\\App-Demo-Detect-Fire-and-Smoke\api\\api.py\" --input \"{inputPath}\" --output \"{outputFilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return outputFilePath;
                }
                else
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() => StatusMessage = $"Lỗi từ script Python: {errors}");
                    return null;
                }
        }

        private async Task LoadResultFileAsync(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            await Task.Delay(200); // Chờ một chút để file được giải phóng hoàn toàn

            if (fileInfo.Extension.ToLower() is ".jpg" or ".png")
            {
                var bmp = new BitmapImage(new Uri(filePath));
                OutputImageSource = bmp;
                IsImageOutputVisible = true;
            }
            else if (fileInfo.Extension.ToLower() is ".mp4")
            {
                OutputVideoSource = MediaSource.CreateFromUri(new Uri(filePath));
                IsVideoOutputVisible = true;
            }
        }

        private async Task SetInputFileAsync(StorageFile file)
        {
            if (file.ContentType.StartsWith("image/"))
            {
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(stream);
                InputImageSource = bmp;
            }
        }
  
    }
}


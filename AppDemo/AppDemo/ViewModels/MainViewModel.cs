using AppDemo.Models;
using AppDemo.Services;
using ClassLibrary1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public static MainViewModel instance;
        public static MainViewModel GetInstance()
        {
            if (instance == null)
            {
                instance = new MainViewModel();
            }
            return instance;
        }

        HistoryService historyService = HistoryService.GetInstance();

        [ObservableProperty] private BitmapImage? _inputImageSource;
        [ObservableProperty] private BitmapImage? _outputImageSource;
        [ObservableProperty] private MediaSource? _outputVideoSource;
        [ObservableProperty] private MediaSource? _inputVideoSource;
        [ObservableProperty] private bool _isImageOutputVisible;
        [ObservableProperty] private bool _isVideoOutputVisible;
        [ObservableProperty] private bool _isVideoInputVisible;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Sẵn sàng";

        [ObservableProperty] private ObservableCollection<HistoryItem> _detectionHistory = new();

        public MainViewModel()
        {
            InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            StatusMessage = "Đang tải lịch sử...";
            var historyPaths = await historyService.LoadHistoryAsync();
            foreach (var path in historyPaths)
            {
                if (File.Exists(path))
                {
                    DetectionHistory.Add(new HistoryItem
                    {
                        FilePath = path,
                        ImageSource = new BitmapImage(new Uri(path))
                    });
                }
            }
            StatusMessage = "Sẵn sàng";
        }

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
                string? resultPath = await API(inputFile.Path);
                if (!string.IsNullOrEmpty(resultPath))
                {
                    if (Path.GetExtension(resultPath).ToLower() is ".jpg" or ".png")
                    {
                        // BƯỚC KIỂM TRA MỚI: Dùng LINQ.Any() để xem có mục nào đã có cùng FilePath chưa
                        bool daTonTai = DetectionHistory.Any(item => item.FilePath == resultPath);

                        if (!daTonTai) // Nếu CHƯA tồn tại thì mới thêm
                        {
                            // Thêm vào danh sách để UI tự cập nhật
                            DetectionHistory.Add(new HistoryItem
                            {
                                FilePath = resultPath,
                                ImageSource = new BitmapImage(new Uri(resultPath))
                            });

                            // Lưu đường dẫn vào file log
                            await historyService.AddToHistoryAsync(resultPath);
                        }
                        else // neu da ton tai
                        {

                        }
                    }
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


        // api
        private async Task<string?> API(string inputPath)
        {
            Class1.Manh();
            string outputDirectory = "C:\\DO AN TOT NGHIEP\\rs";
            Directory.CreateDirectory(outputDirectory);
            string outputFileName = $"result_{Path.GetFileName(inputPath)}";
            string outputFilePath = Path.Combine(outputDirectory, outputFileName);

            bool tonTai = DetectionHistory.Any(item => item.FilePath == outputFilePath);
            if (tonTai)
            {
                return outputFilePath;
            }
            else
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        // !!! THAY ĐỔI 2 ĐƯỜNG DẪN NÀY CHO ĐÚNG VỚI MÁY CỦA BẠN !!!
                        FileName = @"C:\Users\ameri\AppData\Local\Programs\Python\Python313\python.exe",
                        Arguments = $"\"C:\\Users\\ameri\\source\\repos\\api\\api.py\" --input \"{inputPath}\" --output \"{outputFilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return outputFilePath;
                }
                else
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusMessage = $"Lỗi từ script Python: {errors}";
                        Debug.WriteLine("Lỗi Python: " + errors);
                    });

                    return null;
                }
            }
        }

        private async Task LoadResultFileAsync(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

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
                IsVideoInputVisible = false;
                await bmp.SetSourceAsync(stream);
                InputImageSource = bmp;
            }
            else
            {
                if (file.ContentType.StartsWith("video/"))
                {
                    InputVideoSource = MediaSource.CreateFromStorageFile(file);
                    IsVideoInputVisible = true;
                }
            }
        }
    }
}


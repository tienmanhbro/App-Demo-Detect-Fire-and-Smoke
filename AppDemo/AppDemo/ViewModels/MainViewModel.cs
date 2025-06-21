using AppDemo.Helpers;
using AppDemo.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AppDemo.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; set; }

        private readonly FireDetector _fireDetector;
        private CancellationTokenSource _videoCts;

        [ObservableProperty]
        private SoftwareBitmapSource _imageSource;

        [ObservableProperty]
        private string _statusText = "Chọn một tệp ảnh hoặc video để bắt đầu";

        public MainViewModel()
        {
            var modelPath = Path.Combine(AppContext.BaseDirectory, "Assets", "best.onnx");
            _fireDetector = new FireDetector(modelPath);
            ImageSource = new SoftwareBitmapSource();
        }

        [RelayCommand]
        private async Task LoadImageAsync()
        {
            StopVideoProcessing();
            var file = await PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".bmp" });
            if (file == null) return;

            StatusText = "Đang xử lý ảnh...";
            try
            {
                await ProcessImage(file);
                StatusText = $"Đã xử lý xong: {file.Name}";
            }
            catch (Exception ex)
            {
                StatusText = $"Lỗi xử lý ảnh: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadVideoAsync()
        {
            StopVideoProcessing();
            var file = await PickFileAsync(new[] { ".mp4", ".avi", ".mov", ".wmv" });
            if (file == null) return;

            _videoCts = new CancellationTokenSource();
            try
            {
                await ProcessVideo(file, _videoCts.Token);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Đã dừng xử lý video.";
            }
            catch (Exception ex)
            {
                StatusText = $"Lỗi xử lý video: {ex.Message}";
            }
        }

        private async Task ProcessImage(StorageFile file)
        {
            await Task.Run(async () =>
            {
                using var stream = await file.OpenStreamForReadAsync();
                using var image = Mat.FromStream(stream, ImreadModes.Color);
                if (image.Empty()) throw new Exception("Không thể đọc tệp ảnh.");

                using var resultImage = _fireDetector.DetectAndDraw(image);
                await UpdateImageSource(resultImage);
            });
        }

        private Task ProcessVideo(StorageFile file, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                using var capture = new VideoCapture(file.Path);
                if (!capture.IsOpened()) throw new Exception("Không thể mở tệp video.");

                using var frame = new Mat();
                while (!token.IsCancellationRequested)
                {
                    if (!capture.Read(frame) || frame.Empty()) break;

                    using var resultFrame = _fireDetector.DetectAndDraw(frame);
                    await UpdateImageSource(resultFrame);
                    await Task.Delay(30, token);
                }

                DispatcherQueue?.TryEnqueue(() => StatusText = "Video đã kết thúc hoặc đã dừng.");
            }, token);
        }

        private void StopVideoProcessing()
        {
            _videoCts?.Cancel();
            _videoCts = null;
        }

        private async Task UpdateImageSource(Mat mat)
        {
            // SỬA LỖI: Gọi hàm helper thay vì phương thức mở rộng
            using var softwareBitmap = ImageConverter.MatToSoftwareBitmap(mat);

            if (DispatcherQueue != null && softwareBitmap != null)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await ImageSource.SetBitmapAsync(softwareBitmap);
                });
            }
        }

        private static async Task<StorageFile> PickFileAsync(string[] fileTypeFilter)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            foreach (var filter in fileTypeFilter)
            {
                picker.FileTypeFilter.Add(filter);
            }

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            return await picker.PickSingleFileAsync();
        }
    }
}

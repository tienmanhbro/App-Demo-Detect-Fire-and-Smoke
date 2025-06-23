using AppDemo.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AppDemo.Services
{
    public class HistoryService
    {

        public static HistoryService instance;
        public static HistoryService GetInstance()
        {
            if (instance == null)
            {
                instance = new HistoryService();
            }
            return instance;
        }

        private const string HistoryFileName = @"C:\Users\ameri\source\repos\AppDemo\AppDemo\DataHistory\detection_history.log";
        private readonly StorageFolder _localFolder = ApplicationData.Current.LocalFolder;

        /// <summary>
        /// Đọc tất cả các đường dẫn file từ file lịch sử.
        /// </summary>
        public async Task<List<string>> LoadHistoryAsync()
        {
            try
            {
                //var file = await _localFolder.TryGetItemAsync(HistoryFileName) as StorageFile;
                if (true)
                {
                    var lines = await File.ReadAllLinesAsync(HistoryFileName);
                    return new List<string>(lines);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi khi đọc file lịch sử: {ex.Message}");
            }
            return new List<string>();
        }

        /// <summary>
        /// Thêm một đường dẫn mới vào cuối file lịch sử.
        /// </summary>
        public async Task AddToHistoryAsync(string resultPath)
        {
            try
            {
                // Mở file ở chế độ ghi nối tiếp (append)
                await File.AppendAllTextAsync(Path.Combine(_localFolder.Path, HistoryFileName), resultPath + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi khi ghi file lịch sử: {ex.Message}");
            }
        }
    }
}

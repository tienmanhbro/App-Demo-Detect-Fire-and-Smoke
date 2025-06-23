using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDemo.Models
{
    public class HistoryItem
    {
        public string FilePath { get; set; }

        // Nguồn ảnh để binding trực tiếp vào giao diện
        public BitmapImage ImageSource { get; set; }
    }
}

using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace AppDemo.Helpers
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public static class ImageConverter
    {
        public static unsafe SoftwareBitmap MatToSoftwareBitmap(Mat mat)
        {
            // SỬA LỖI: Dùng phương thức .Empty() thay cho .IsEmpty
            if (mat == null || mat.Empty())
                return null;

            // SoftwareBitmap yêu cầu định dạng BGRA8 với alpha premultiplied
            Mat matBgra = new Mat();
            Cv2.CvtColor(mat, matBgra, ColorConversionCodes.BGR2BGRA);

            var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, matBgra.Width, matBgra.Height, BitmapAlphaMode.Premultiplied);

            using (BitmapBuffer buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                // Lấy con trỏ đến bộ đệm của SoftwareBitmap
                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);

                // Sao chép dữ liệu từ Mat sang SoftwareBitmap
                matBgra.GetArray(out byte[] matData);
                Marshal.Copy(matData, 0, (IntPtr)dataInBytes, matData.Length);
            }
            matBgra.Dispose();
            return softwareBitmap;
        }
    }
}

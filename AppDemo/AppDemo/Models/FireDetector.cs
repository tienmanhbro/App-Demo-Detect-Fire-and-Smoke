using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDemo.Models
{
    public class FireDetector
    {
        private readonly InferenceSession _session;
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private Size _originalImageSize;

        private const float CONF_THRESHOLD = 0.5f;
        private const float NMS_THRESHOLD = 0.4f;

        public FireDetector(string modelPath)
        {
            try
            {
                var options = new SessionOptions();
                options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
                _session = new InferenceSession(modelPath, options);
                var inputMeta = _session.InputMetadata.First();
                _inputHeight = inputMeta.Value.Dimensions[2];
                _inputWidth = inputMeta.Value.Dimensions[3];
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Không thể tải mô hình ONNX từ '{modelPath}'. Lỗi: {ex.Message}", ex);
            }
        }

        private DenseTensor<float> Preprocess(Mat image)
        {
            _originalImageSize = image.Size();
            var resizedImage = image.Resize(new Size(_inputWidth, _inputHeight));
            Cv2.CvtColor(resizedImage, resizedImage, ColorConversionCodes.BGR2RGB);

            var inputTensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            resizedImage.ConvertTo(resizedImage, MatType.CV_32F, 1.0 / 255.0);

            Mat[] channels = Cv2.Split(resizedImage);
            var channelSize = _inputWidth * _inputHeight;
            for (int i = 0; i < 3; i++)
            {
                var channelData = channels[i].Reshape(1, channelSize);
                for (int j = 0; j < channelSize; j++)
                {
                    inputTensor[0, i, j] = channelData.At<float>(0, j);
                }
            }
            return inputTensor;
        }

        private List<Rect> Postprocess(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            var outputTensor = results.First().AsTensor<float>();
            var boxes = new List<Rect>();
            var scores = new List<float>();

            var tensorDimensions = outputTensor.Dimensions;
            if (tensorDimensions.Length != 3 || tensorDimensions[0] != 1)
            {
                throw new NotSupportedException("Chỉ hỗ trợ đầu ra có hình dạng [1, attributes, detections].");
            }

            int numAttributes = tensorDimensions[1]; // 6
            int numDetections = tensorDimensions[2]; // 8400

            if (numAttributes < 5)
            {
                throw new NotSupportedException($"Đầu ra phải có ít nhất 5 thuộc tính (x,y,w,h,score), nhưng chỉ có {numAttributes}.");
            }

            var outputData = outputTensor.ToArray();

            // Dữ liệu được lưu trữ theo cột, vì vậy chúng ta truy cập nó giống như một ma trận đã được chuyển vị.
            // [cx1, cx2, ..., cx8400, cy1, cy2, ..., cy8400, ... ]

            float xFactor = (float)_originalImageSize.Width / _inputWidth;
            float yFactor = (float)_originalImageSize.Height / _inputHeight;

            for (int i = 0; i < numDetections; i++)
            {
                // Tìm điểm số lớp cao nhất cho đối tượng phát hiện `i` hiện tại.
                // Các điểm số lớp bắt đầu từ chỉ số 4.
                float maxClassScore = 0.0f;
                for (int classIdx = 4; classIdx < numAttributes; classIdx++)
                {
                    int scoreIndex = classIdx * numDetections + i;
                    // THÊM BƯỚC KIỂM TRA AN TOÀN
                    if (scoreIndex >= outputData.Length)
                    {
                        throw new IndexOutOfRangeException($"Lỗi truy cập chỉ mục điểm số. Chỉ mục: {scoreIndex}, Kích thước mảng: {outputData.Length}. Điều này cho thấy logic phân tích đầu ra bị sai.");
                    }
                    float currentScore = outputData[scoreIndex];
                    if (currentScore > maxClassScore)
                    {
                        maxClassScore = currentScore;
                    }
                }

                if (maxClassScore > CONF_THRESHOLD)
                {
                    scores.Add(maxClassScore);

                    // Lấy tọa độ hộp giới hạn cho đối tượng phát hiện `i`
                    int cxIndex = 0 * numDetections + i;
                    int cyIndex = 1 * numDetections + i;
                    int wIndex = 2 * numDetections + i;
                    int hIndex = 3 * numDetections + i;

                    // THÊM BƯỚC KIỂM TRA AN TOÀN
                    if (hIndex >= outputData.Length)
                    {
                        throw new IndexOutOfRangeException($"Lỗi truy cập chỉ mục tọa độ. Chỉ mục lớn nhất: {hIndex}, Kích thước mảng: {outputData.Length}.");
                    }

                    float cx = outputData[cxIndex];
                    float cy = outputData[cyIndex];
                    float w = outputData[wIndex];
                    float h = outputData[hIndex];

                    int left = (int)((cx - w / 2) * xFactor);
                    int top = (int)((cy - h / 2) * yFactor);
                    int width = (int)(w * xFactor);
                    int height = (int)(h * yFactor);

                    boxes.Add(new Rect(left, top, width, height));
                }
            }

            if (boxes.Count > 0)
            {
                CvDnn.NMSBoxes(boxes, scores, CONF_THRESHOLD, NMS_THRESHOLD, out int[] indices);
                return indices.Select(idx => boxes[idx]).ToList();
            }

            return new List<Rect>();
        }

        public Mat DetectAndDraw(Mat image)
        {
            var inputTensor = Preprocess(image);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_session.InputNames.First(), inputTensor)
            };

            using var results = _session.Run(inputs);
            var boxes = Postprocess(results);

            var resultImage = image.Clone();
            foreach (var box in boxes)
            {
                Cv2.Rectangle(resultImage, box, Scalar.Red, 3);
                Cv2.PutText(resultImage, "Chay", new Point(box.Left, box.Top - 10), HersheyFonts.HersheySimplex, 1, Scalar.Red, 2);
            }

            return resultImage;
        }
    }
}

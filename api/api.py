# File: process_file.py (Debug Version - No Thresholds)
import cv2
import numpy as np
import onnxruntime as ort
import argparse
import os
import sys

# --- HÀM XỬ LÝ LOGIC ---
def get_detections_raw(session, image: np.ndarray):
    # Phần tiền xử lý không đổi
    input_name = session.get_inputs()[0].name
    input_shape = session.get_inputs()[0].shape
    input_height, input_width = input_shape[2], input_shape[3]
    original_height, original_width = image.shape[:2]
    
    ratio = min(input_width / original_width, input_height / original_height)
    new_width, new_height = int(original_width * ratio), int(original_height * ratio)
    resized_img = cv2.resize(image, (new_width, new_height), interpolation=cv2.INTER_AREA)
    
    padded_img = np.full((input_height, input_width, 3), 114, dtype=np.uint8)
    padded_img[(input_height - new_height) // 2 : (input_height - new_height) // 2 + new_height, 
               (input_width - new_width) // 2 : (input_width - new_width) // 2 + new_width] = resized_img
    
    input_tensor = np.transpose(padded_img, (2, 0, 1)).astype(np.float32) / 255.0
    input_tensor = np.expand_dims(input_tensor, axis=0)

    # Chạy dự đoán
    outputs = session.run(None, {input_name: input_tensor})

    # Hậu xử lý - BỎ QUA TẤT CẢ BỘ LỌC
    predictions = np.squeeze(outputs[0]).T
    
    # Lấy tất cả điểm tin cậy, không lọc
    scores = np.max(predictions[:, 4:], axis=1)
    
    if len(predictions) == 0: 
        return [], [], []

    # Lấy tất cả class id và bounding box
    class_ids = np.argmax(predictions[:, 4:], axis=1)
    boxes = predictions[:, :4]
    
    # Vẫn scale lại tọa độ box
    boxes[:, 0] = (boxes[:, 0] - (input_width - new_width) / 2) / ratio
    boxes[:, 1] = (boxes[:, 1] - (input_height - new_height) / 2) / ratio
    boxes[:, 2] /= ratio
    boxes[:, 3] /= ratio
    boxes[:, 0] -= boxes[:, 2] / 2
    boxes[:, 1] -= boxes[:, 3] / 2
    boxes[:, 2] += boxes[:, 0]
    boxes[:, 3] += boxes[:, 1]

    # Trả về TOÀN BỘ dữ liệu thô, không qua NMS
    return boxes, scores, class_ids

# --- HÀM MAIN ĐỂ CHẠY SCRIPT ---
if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Đường dẫn file đầu vào.")
    parser.add_argument("--output", required=True, help="Đường dẫn file kết quả đầu ra.")
    args = parser.parse_args()

    try:
        LABELS = ["Fire", "Smoke"]
        # Sử dụng đường dẫn tuyệt đối đã được sửa lỗi cú pháp
        MODEL_PATH = r"D:\myapps\App-Demo-Detect-Fire-and-Smoke\api\best.onnx"
        SESSION = ort.InferenceSession(MODEL_PATH, providers=['CPUExecutionProvider'])

        file_extension = os.path.splitext(args.input)[1].lower()
        
        # Hàm vẽ đã được cập nhật để xử lý kết quả thô
        def draw_raw_detections(image, boxes, scores, class_ids):
            if len(boxes) > 0:
                for i in range(len(boxes)):
                    score = scores[i]
                    # Chỉ vẽ những box có độ tin cậy > 10% để ảnh không bị đen kịt
                    if score > 0.1: 
                        box = boxes[i].astype(int)
                        class_id = class_ids[i]
                        label = f"{LABELS[class_id]}: {score:.2f}"
                        # Dùng màu khác để phân biệt với kết quả cuối cùng
                        cv2.rectangle(image, (box[0], box[1]), (box[2], box[3]), (255, 0, 0), 1) # Màu xanh dương, nét mỏng
                        cv2.putText(image, label, (box[0], box[1] - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 0, 0), 1)

        if file_extension in ['.jpg', '.jpeg', '.png', '.bmp']:
            img = cv2.imread(args.input)
            boxes, scores, class_ids = get_detections_raw(SESSION, img)
            draw_raw_detections(img, boxes, scores, class_ids)
            cv2.imwrite(args.output, img)

        elif file_extension in ['.mp4', '.avi', '.mov']:
            cap = cv2.VideoCapture(args.input)
            fps = int(cap.get(cv2.CAP_PROP_FPS))
            width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
            fourcc = cv2.VideoWriter_fourcc(*'mp4v')
            out = cv2.VideoWriter(args.output, fourcc, fps, (width, height))

            while cap.isOpened():
                ret, frame = cap.read()
                if not ret: break
                
                boxes, scores, class_ids = get_detections_raw(SESSION, frame)
                draw_raw_detections(frame, boxes, scores, class_ids)
                out.write(frame)
                
            cap.release()
            out.release()

        print(f"Success (Debug Mode): {args.output}")
        sys.exit(0)

    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
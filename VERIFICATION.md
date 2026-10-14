# Bằng chứng và giới hạn kiểm chứng

Ngày: 12/10/2026, múi giờ Asia/Saigon. Báo cáo này áp dụng **source bản giao cuối**, không suy rộng thành chứng nhận thư viện hay toàn bộ MVP.

## Môi trường thật

- Windows 11 x64, OS 10.0.26200.0.
- Build bằng .NET SDK 10.0.401, target `net8.0-windows`, `win-x64`; runtime .NET 8.0.31.
- Build Release: **0 lỗi, 0 cảnh báo**.
- NuGet restore thành công: EasyWindowsTerminalControl 1.0.38, CI.Microsoft.Terminal.Wpf 1.25.260303002, Microsoft.Windows.Console.ConPTY 1.24.260710001. Các DLL native được copy vào output.
- Windows PowerShell của hệ thống; Codex CLI chạy bên trong shell: **0.160.0**. CLI executable kèm ứng dụng Codex nhìn thấy từ shell công cụ có phiên bản khác; không dùng phiên bản đó để ghi kết quả của terminal nhúng.
- Một lần chạy sandbox ẩn không cho kết quả; các PASS dưới đây lấy từ lần chạy WPF trên desktop Windows thật, không phải chỉ từ build hoặc đọc mã.

## Kết quả bản cuối

`evidence/smoke-results.json`: ExitCode 0, 14 dòng PASS; `evidence/lifecycle.txt`: trình tự dọn phiên. Bộ thử dùng chính MainWindow và TerminalSession của app, không dùng backend giả.

| Hạng mục | Kết quả | Bằng chứng/phạm vi |
|---|---|---|
| Restore + build | Đã đạt | DLL managed/native được resolve, 0 warning/error |
| Khởi tạo WPF/renderer/PowerShell | Đã đạt ở cấp API | Control được load; shell thực thi lệnh, output qua ConPTY |
| Nhập lệnh và output | Đã đạt qua API | Shell ghi file chứa PID và biến; callback nhận output |
| Hai phiên độc lập | Đã đạt | Hai PID và hai giá trị biến khác nhau |
| Chuyển tab giữ phiên | Đã đạt qua chọn tab bằng code | 20 lần đổi vẫn đúng PID/biến; tab ẩn vẫn thực thi lệnh |
| Resize | Đã đạt qua thay kích thước Window bằng code | Console trả 71×13, khớp Columns/Rows của control sau resize |
| Ngắt PowerShell | Đã đạt qua API | Gửi byte Ctrl+C, lệnh Sleep bị ngắt, shell nhận lệnh tiếp |
| Đóng tab | Đã đạt với cây tiến trình mẫu | Shell A và PowerShell con hết PID; B vẫn tương tác và giữ biến |
| Đóng ngay sau tạo | Đã đạt | Phiên chưa khởi động không bị khởi động muộn |
| Shell sai / shell tự exit | Đã đạt | Báo lỗi khởi động; `exit` hoàn tất, tab đóng được |
| Codex --version | Đã đạt | Trả `codex-cli 0.160.0` trong terminal |
| Codex TUI khởi động | Quan sát được output | Nhận title, ô prompt và hộp cập nhật trong stream VT |
| Agent nhận prompt và phản hồi | **Chưa đạt kiểm chứng** | Probe gửi prompt đã gặp hộp cập nhật; không có phản hồi agent được xác minh |
| Mũi tên/Tab/Enter trong agent | **Chưa kiểm chứng** | Không được suy ra từ nhập lệnh PowerShell qua API |
| Phê duyệt và ngắt tác vụ agent | **Chưa kiểm chứng** | Không có task agent được chạy và hủy có bằng chứng |
| Copy/paste bằng chuột/phím | **Đã viết, chưa kiểm chứng** | Có GetSelectedText/Clipboard/nút/shortcut; cần thao tác thủ công |
| Chất lượng render, scrollback, IME, DPI | **Chưa kiểm chứng đầy đủ** | API resize không chứng minh màn hình không bị chồng chữ |
| Tắt app khi nhiều agent chạy | **Chưa kiểm chứng thủ công** | Có đường cleanup chung; cần kiểm tra hộp xác nhận và cây tiến trình thực tế |

ExitCode 0 ở đây chỉ nói các assertion đã viết chạy qua. Các dòng UNVERIFIED vẫn là điều kiện phải xử lý, không được coi là PASS.

## Sự cố được phát hiện và thay đổi

1. Adapter ban đầu không bật reader nền trước khi `TermPTY.Start` tới vòng chờ tiến trình. Đóng shell xong nhưng reader vẫn chờ pipe, dẫn đến timeout. Đã thêm `SessionPty` với một reader nền duy nhất, để vòng đời tiến trình đi tới cleanup của thư viện. Không kết luận mọi cách sử dụng EasyTerminalControl đều có lỗi này.
2. Sau khi library đóng pipe, `CloseStdinToApp` có thể báo ObjectDisposedException khi flush. Bản cuối chỉ bỏ qua lỗi IO/disposed tại **bước cleanup sau khi run kết thúc**, không bỏ qua lỗi kill/start.
3. `TERM=dumb` kế thừa làm CLI hỏi có tiếp tục không. Shell của từng tab hiện tự khai báo `TERM=xterm-256color` trong môi trường của **chính tiến trình đó**; không thay environment toàn máy.
4. **Tác dụng phụ probe:** một lần tự gửi Escape rồi prompt qua API không bỏ qua hộp update như dự kiến; Enter sau đó kích hoạt npm cập nhật Codex lên 0.162.1. Không có bằng chứng prompt đã tới agent. Đã khôi phục npm package chính về **0.160.0**, kiểm tra lại `codex.cmd --version` cả trước và sau lần chạy cuối. Không xóa bằng tay thư mục tạm npm để tránh chạm binary đang được dùng. Bản giao đã loại bỏ hoàn toàn việc tự gửi phím/prompt sau khi CLI khởi động. Đây là lý do không dùng stream output cũ để quyết định tự bấm hộp thoại.

## Vì sao còn checklist thủ công?

Máy có Windows, nhưng test tự động ở đây dùng API C#/ConPTY, không phải một người thật thao tác bàn phím. Công cụ Windows Computer Use trong phiên này quy định “Do not automate the ChatGPT desktop app UI or Codex CLI or Codex extensions within Windows apps.” và “Do not automate user authentication dialogs.” Việc thử prompt qua API không chứng minh đường phím native/clipboard.

Hướng dẫn này thuộc môi trường thử, không phải yêu cầu đối với đồ án của bạn: `C:/Users/Admin/.codex/plugins/cache/openai-bundled/computer-use/26.1007.21434/skills/computer-use/SKILL.md`, tham chiếu `../../docs/guidance.md`. Không thay công cụ khác để tự động bấm hộp thoại CLI. Dùng CHECKLIST để bạn kiểm tra trực tiếp.

## Điều không được suy ra

- Không có bằng chứng khôi phục hội thoại, giữ agent sống sau khi tắt app hoặc tự hiểu trạng thái nghiệp vụ.
- Không coi process tree kill là bảo đảm thu gom service/daemon tách rời hoặc xử lý crash.
- Không nói “Codex tương thích hoàn toàn” từ việc TUI xuất được output.
- Không chứng nhận package beta ổn định trên mọi Windows, kiến trúc CPU hay phiên bản .NET.
- Chưa có kiểm thử lâu dài về RAM/handle hoặc hàng trăm lần mở/đóng tab.

Kết luận thử nghiệm: **có nền tảng kỹ thuật để tiếp tục tuần 1; chưa qua cổng quyết định cho MVP agent tương tác hoàn chỉnh**.

# Checklist quyết định tiếp tục đồ án

Ghi Windows build, kiến trúc, .NET SDK/runtime, `Get-Command codex -All`, `codex --version`. Dùng thư mục demo trống. Thử cùng CLI ngoài app trước để tách lỗi agent/tài khoản khỏi lỗi terminal.

Mỗi mục ghi PASS / FAIL / CHƯA THỬ và bằng chứng. Đừng đánh dấu PASS vì nhìn code có vẻ đúng.

## A. Một terminal

1. Mở app; PowerShell hiện prompt tại đúng thư mục. Gõ `Get-Date`, `Get-Location`, `$PID`; output đọc được và không phải một cửa sổ console ngoài app.
2. Gõ sai một ký tự rồi sửa bằng Backspace, Delete, Home, End, trái/phải. Thử Up/Down gọi lịch sử. Tab hoàn tất tên file. Focus phải ở terminal.
3. Phóng to/thu nhỏ rồi gõ `$Host.UI.RawUI.WindowSize`; số cột/hàng đổi, prompt không biến mất. In 100 dòng, scrollback và chọn text vẫn dùng được.
4. Kéo chọn output → nút Copy → dán vào Notepad. Thử thêm Ctrl+Shift+C. Copy phải lấy vùng chọn, không gửi Ctrl+C vào tác vụ.
5. Copy một dòng `Write-Output 'paste-ok'` → nút Paste → kiểm tra chưa tự chạy → Enter. Thử Ctrl+Shift+V. Bản thử từ chối nhiều dòng; không đánh dấu tính năng multiline là PASS.
6. Gõ `Start-Sleep -Seconds 30`, bấm Ctrl+C hoặc nút Ngắt; prompt phải trở lại. Shell còn dùng được.
7. Gõ tiếng Việt có dấu và dán tiếng Việt; ghi rõ kết quả IME. Bản thử chưa đảm bảo IME.

## B. Hai tab giữ phiên

1. Tab A: `$tag='alpha'; $PID`; ghi PID A. Tab B: `$tag='beta'; $PID`; phải có PID B khác A.
2. Đổi tab 20 lần. Kiểm tra `$PID; $tag` ở cả hai; không đổi PID, không mất biến và nội dung màn hình cũ.
3. Tab A chạy `1..10 | ForEach-Object { $_; Start-Sleep 1 }`; sang B chờ rồi trở lại A. Output A tiếp tục trong lúc bị ẩn; B không nhận output A.
4. Đang nhập nửa dòng ở A, đổi sang B rồi quay lại; dòng nhập phải còn nguyên. Focus sau mỗi lần chọn tab phải đúng.
5. Resize khi B được chọn rồi quay lại A. Cả hai nhận đúng kích thước khi được hiện.

## C. Coding agent — bắt buộc trước khi chốt đề tài

1. Chạy `codex --no-daemon` nếu CLI hỗ trợ. Nếu xuất hiện update/login/trust dialog, **tự đọc và xử lý bằng bàn phím**, không dùng `SendText` hay macro tự xác nhận. Bản giao không làm việc này thay bạn.
2. Nhập prompt: `Chỉ trả lời AGENTDESK_OK. Không dùng công cụ hoặc sửa file.` Agent phải nhận đúng một prompt và hiển thị phản hồi đọc được.
3. Trong ô prompt, thử trái/phải, Home/End, Backspace/Delete; nhập hai từ rồi sửa từ đầu. Thử Up/Down trong một menu slash command mà CLI hiện có; Tab nếu menu hỗ trợ. Không giả định mọi phiên bản có cùng phím.
4. Trong một thư mục demo, dùng chế độ quyền yêu cầu phê duyệt phù hợp với phiên bản CLI. Yêu cầu tạo `hello-agentdesk.txt`. Nếu có màn hình xin quyền: đổi lựa chọn bằng mũi tên, **từ chối trước**, kiểm tra file chưa được tạo; thử lại và tự xác nhận cho phép. Không hạ sandbox chỉ để khiến demo dễ chạy. Nếu CLI không hiện approval, mục này vẫn CHƯA THỬ, không tự tính PASS.
5. Yêu cầu một tác vụ đủ lâu, ví dụ chạy PowerShell chờ 30 giây trong thư mục demo. Khi đang chạy, dùng phím ngắt hiển thị trong CLI (thường Esc/Ctrl+C), kiểm tra tác vụ ngừng và vẫn nhập prompt mới được. Phân biệt hủy một tác vụ và thoát cả CLI.
6. Khi agent đang hoạt động, đổi sang tab khác 20 giây rồi trở lại. Hội thoại/màn hình không bị tạo lại; agent của B không bị ảnh hưởng.
7. Resize lúc menu/phản hồi đang hiển thị; ký tự không chồng lấp, chọn menu không sai. Nếu lỗi, ghi lại cùng phép thử với `codex --no-alt-screen` để chẩn đoán; đừng che lỗi trong báo cáo.

## D. Dọn tiến trình và lỗi

1. Trong A chạy:

```powershell
$child = Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 120' -WindowStyle Hidden -PassThru
$PID
$child.Id
```

Đóng A và xác nhận; từ B dùng `Get-Process -Id <PID_A>,<PID_child> -ErrorAction SilentlyContinue`. Không còn hai PID đó; B vẫn nhận lệnh. Không giết theo tên `powershell` hoặc `codex` vì có thể chạm phiên khác.

2. Đang chạy Codex cục bộ ở A, xem PID/process tree bằng Task Manager. Đóng A; shell và CLI thuộc tab phải kết thúc. Không tuyên bố service hoặc daemon dùng chung cũng kết thúc.
3. Gõ `exit` để shell tự thoát rồi đóng tab: không crash. Đóng ngay sau khi thêm tab: không để shell xuất hiện muộn thành tiến trình mồ côi.
4. Mở 3 tab rồi đóng cửa sổ; xác nhận tất cả phiên của app dừng. Bấm No ở hộp xác nhận phải giữ nguyên các phiên.
5. Gõ thư mục không tồn tại rồi mở tab; có lỗi dễ hiểu. Thử shell executable sai khi debug; status phải báo lỗi.
6. Đóng tất cả tab rồi mở tab mới; hoạt động được. Lặp mở/đóng 20 phiên để phát hiện lỗi lifecycle, theo dõi bộ nhớ/handle nếu có thời gian.
7. Thoát/mở app: bản thử không khôi phục tab. Về sau EF Core chỉ khôi phục metadata; PID/hội thoại không được coi là đã khôi phục.

## Tiêu chuẩn go/no-go

GO có điều kiện khi A1–A6, B1–B5, C1–C7 và D1–D5 qua trên máy bảo vệ, không để tiến trình mồ côi trong các tình huống đó. Nếu có tính năng phím không được CLI phiên bản đó hỗ trợ, ghi ngoại lệ cụ thể và cách thao tác tương đương; không dùng ngoại lệ để bỏ prompt/xác nhận/ngắt.

Một CLI hiện phiên bản hoặc hiện màn hình mở đầu chưa đạt C. Nếu còn lỗi C sau tuần đầu, chưa nên cam kết AgentDesk trong 4 tuần.

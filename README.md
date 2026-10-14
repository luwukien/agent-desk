# AgentDesk — thử nghiệm terminal trước khi làm đồ án

Đây là bản thử kỹ thuật, chưa phải MVP hoàn chỉnh. Có cửa sổ WPF, PowerShell tương tác, thêm/chuyển/đóng tab, copy vùng chọn, paste một dòng và ngắt lệnh. Không có database, quản lý dự án, ghi chú hay trạng thái kiểm tra nghiệp vụ.

Đọc `ASSESSMENT.md` để xem đánh giá khả thi và kế hoạch; đọc `CHECKLIST.md` để kiểm tra trên máy bảo vệ. `VERIFICATION.md` phân biệt phần đã chạy và chưa kiểm chứng.

## Chạy trên Windows

1. Dùng Windows 11 x64 và .NET 8 SDK; hoặc SDK mới hơn có thể target .NET 8, kèm .NET 8 Desktop Runtime. Bản thử đã chạy trên Windows build 26200, .NET Desktop 8.0.31, build bằng SDK 10.0.401.
2. Giải nén toàn bộ source vào thư mục có quyền ghi. Mở PowerShell tại thư mục chứa `AgentDesk.Spike.csproj`.
3. Chạy:

```powershell
dotnet restore .\AgentDesk.Spike.csproj --locked-mode
dotnet build .\AgentDesk.Spike.csproj -c Release --no-restore
dotnet run --project .\AgentDesk.Spike.csproj -c Release --no-build
```

Không cần build repository của thư viện hoặc cài Windows Terminal để dùng control. NuGet tải DLL native; giữ cả thư mục output khi copy sang máy khác, không chỉ copy EXE. Mạng cần thiết cho lần restore đầu. Nếu dùng Visual Studio, mở `.csproj`, cài workload .NET desktop development và dùng cấu hình x64.

Windows PowerShell được lấy từ thư mục System của Windows, không giả định máy có `pwsh.exe`. Có thể đổi lệnh khởi động trong `TerminalSession.cs` nếu đã cài PowerShell 7. Không đưa lệnh agent vào `StartupCommandLine`: mở shell trước, tự chạy agent để dễ tách lỗi PATH, đăng nhập và lỗi terminal.

## Dùng cửa sổ

- App mở một tab. Nhập `Get-Date`, `$PID`, `Get-Location` trực tiếp trong terminal.
- Điền một thư mục tồn tại rồi bấm **+ PowerShell** để mở tab thứ hai tại đó. Đổi ô đường dẫn không đổi working directory của tab đã chạy.
- Chọn header để chuyển tab. Mỗi view vẫn ở trong cùng Grid; tab không được chọn dùng `Visibility.Hidden`. Không tháo view, không gọi restart, không chạy lại shell.
- Kéo chọn chữ rồi bấm **Copy vùng chọn**, hoặc thử `Ctrl+Shift+C`.
- Dán bằng **Paste một dòng** hoặc `Ctrl+Shift+V`. Không tự thêm Enter. Bản thử chủ động chỉ hỗ trợ một dòng; paste nhiều dòng/bracketed paste chưa làm.
- `Ctrl+C` bình thường hoặc nút **Ngắt** gửi tín hiệu nhập ngắt. Với agent, tác dụng cụ thể tùy CLI; thử thêm Esc theo CHECKLIST.
- **Đóng tab** có xác nhận: dừng shell và cây tiến trình con. Nếu muốn chương trình thoát nhẹ nhàng, tự thoát CLI và gõ `exit` trước. Đóng tab là dừng cưỡng bức, không phải lưu công việc.
- Đóng cửa sổ lần lượt dọn các phiên. Nếu dọn thất bại, giữ cửa sổ/tab để hiển thị lỗi và cho phép thử lại.

## Chạy Codex CLI

Tại terminal nhúng:

```powershell
Get-Command codex -All
codex --version
codex --help
codex --no-daemon
```

`--no-daemon` có trong CLI được kiểm tra, giúp phiên thử không chủ động dùng app-server nền dùng chung. Nếu phiên bản trên máy không có cờ này, xem `codex --help` và dùng chế độ chạy cục bộ phù hợp; ghi rõ khác biệt khi thử đóng tab. Một daemon bên ngoài cây tiến trình của shell không thuộc bảo đảm dọn dẹp của bản thử.

Nếu chưa có CLI hoặc chưa đăng nhập, làm theo [hướng dẫn Codex CLI chính thức](https://learn.chatgpt.com/docs/codex/cli), chạy thử ngoài app trước. Không cần API key trong code AgentDesk: tài khoản, model, kết nối mạng và phiên hội thoại do CLI quản lý. AgentDesk không gọi API AI.

Nếu PowerShell báo không chạy được `codex.ps1`, kiểm tra có `codex.cmd` và chạy shim đó; không cần đổi execution policy toàn máy để sửa bản thử. Nếu CLI ngoài app cũng lỗi thì chưa thể kết luận lỗi do control.

## Tự chạy kiểm tra tích hợp

`--smoke` dùng PowerShell và ConPTY thật, tạo file bằng chứng, tạo một tiến trình PowerShell con rồi đóng nó. Nó gửi dữ liệu bằng API, **không giả lập bàn phím hay chứng minh chất lượng hiển thị**. Chạy trong thư mục thử mới mỗi lần để tránh file cũ làm sai kết quả:

```powershell
$spikeExe = (Resolve-Path '.\bin\Release\net8.0-windows\win-x64\AgentDesk.Spike.exe').Path
$runDir = Join-Path (Get-Location) ('smoke-runs\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
Start-Process -FilePath $spikeExe -ArgumentList '--smoke' -WorkingDirectory $runDir -Wait
Get-Content (Join-Path $runDir 'smoke-results.json')
```

Thêm `--codex-probe` vào danh sách ArgumentList để khởi động CLI tương tác, ghi nhận output rồi đóng phiên. Bản giao không gửi phím hoặc prompt sau khi CLI mở: hộp cập nhật/đăng nhập phải kiểm tra thủ công. `codex-probe.txt` là raw VT cục bộ, không phải transcript đã xử lý. Không chia sẻ raw log nếu có nội dung riêng tư. Xem sự cố probe trong `VERIFICATION.md`.

Không coi ExitCode 0 của smoke là toàn bộ MVP đạt: đọc từng dòng `PASS`, `UNVERIFIED`, `OBSERVATION` và hoàn thành kiểm tra thủ công.

## Đọc code theo thứ tự

1. `MainWindow.xaml`: bố cục; TabControl chỉ chứa header, Grid chứa terminal.
2. `MainWindow.xaml.cs`: thêm/chọn/đóng phiên, copy/paste, xác nhận đóng, dọn khi thoát.
3. `TerminalSession.cs`: một phiên sở hữu một control, một ConPTY và một shell; chờ ready, bắt lỗi, quản lý teardown.
4. `SessionPty.cs`: phần tích hợp khó nhất — tách duy nhất một reader để vòng chờ tiến trình vẫn có thể hoàn tất và đóng pipe.
5. `SmokeTest.cs`: bằng chứng thực nghiệm, không phải nghiệp vụ của app.

Prototype dùng code-behind có chủ đích để thấy rõ vòng đời. Khi làm MVP, chuyển CRUD/ghi chú/trạng thái sang ViewModel; giữ terminal adapter ở lớp service/view riêng. Không cần tự viết generic repository hoặc framework DI để chứng minh Design Pattern.

## Giới hạn đã biết

- Chỉ target Windows x64. Metadata `net8.0-windows7.0` của package không có nghĩa ConPTY chạy trên Windows 7.
- Chưa kiểm tra Windows 10, ARM64, màn hình HiDPI, nhiều màn hình, IME/tiếng Việt, chạy liên tục nhiều giờ hoặc memory/handle leaks.
- Chỉ paste một dòng. Shortcut đi qua HWND cần kiểm tra thực tế; nút copy/paste là đường thao tác bổ sung.
- `Kill(true)` chỉ nhắm cây tiến trình còn truy được từ shell. Không quản lý service, daemon dùng chung hoặc tiến trình đã tách rời; không cam kết cleanup sau app crash/Task Manager End task.
- Timeout đóng phiên được báo lỗi, không được che giấu thành thành công. Không có Windows Job Object trong prototype.
- `SessionPty` dựa vào hành vi phiên bản 1.0.38. Nâng package phải chạy lại smoke và checklist.
- Thông tin tab chưa lưu; dù về sau lưu vào EF Core, mở lại app vẫn tạo **tiến trình mới**.

## Phụ thuộc được khóa

| Gói | Phiên bản |
|---|---|
| EasyWindowsTerminalControl | 1.0.38 |
| CI.Microsoft.Terminal.Wpf | 1.25.260303002 |
| Microsoft.Windows.Console.ConPTY | 1.24.260710001 |

`packages.lock.json` khóa cả gói gián tiếp và hash. Lần đầu build có thể cần Visual C++ Runtime x64 nếu máy báo thiếu DLL native; ghi lại đúng tên DLL/lỗi trước khi sửa. Đây không phải lỗi XAML thuần túy.

Source kiểm tra: [repository ứng viên](https://github.com/mitchcapper/EasyWindowsTerminalControl), [NuGet 1.0.38](https://www.nuget.org/packages/EasyWindowsTerminalControl/1.0.38), [CI terminal WPF](https://www.nuget.org/packages/CI.Microsoft.Terminal.Wpf/1.25.260303002), [ConPTY](https://www.nuget.org/packages/Microsoft.Windows.Console.ConPTY/1.24.260710001).

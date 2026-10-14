# Đánh giá AgentDesk cho PRN212

Ngày kiểm tra: 12/10/2026. Giả định thời gian của bạn: **10–15 giờ/tuần, tổng 40–60 giờ trong 4 tuần**. Chưa giả định bạn thành thạo WPF, EF Core hay async/await.

## Kết luận thẳng

**Đề tài phù hợp môn học, nhưng không phải lựa chọn ít rủi ro cho người mới trong 4 tuần.** Với 12–15 giờ/tuần, chỉ hỗ trợ một CLI, vài tab, một máy Windows và dùng thư viện terminal đã qua kiểm thử, có cơ hội hoàn thành MVP. Với 10 giờ/tuần và cả WPF, EF Core, async đều mới, tôi không khuyên cam kết chắc chắn bản đầy đủ.

Ước lượng làm và học: **48–60 giờ** nếu phần tích hợp terminal đạt sớm. Nếu phải thay thư viện, sửa native interop hoặc học cả ba mảng trên từ đầu với nhiều vướng mắc, có thể lên **65–85 giờ**. Đây là ước lượng kỹ thuật có điều kiện, không phải số liệu đo năng lực của bạn.

Bản thử đã xác nhận nhiều hành vi shell thật; chưa xác nhận toàn bộ tương tác Codex. Vì vậy chưa thể kết luận “đề tài chắc chắn làm được trong một tháng”. Điều kiện quyết định: bạn tự chạy CHECKLIST trên máy sẽ bảo vệ, hiểu được vì sao tab giữ phiên và vì sao đóng tab dọn được tiến trình.

## Scope

| Mức | Phạm vi |
|---|---|
| Bắt buộc | CRUD dự án: tên, đường dẫn thư mục tồn tại, mô tả |
| Bắt buộc | Terminal PowerShell thật, nhập trực tiếp; output, resize, copy/paste cơ bản |
| Bắt buộc | Thêm/chọn/đóng nhiều tab; mỗi tab có dự án và tên công việc; mục tiêu demo 2–4 tab |
| Bắt buộc | Chạy được **một** coding agent đã cài; ưu tiên Codex CLI; prompt, điều hướng, xác nhận, ngắt tác vụ phải qua kiểm tra |
| Bắt buộc | Ghi chú và trạng thái do người dùng chọn: Chưa kiểm tra / Cần làm tiếp / Đã kiểm tra |
| Bắt buộc | EF Core + SQLite lưu metadata; tìm tên dự án, tên việc, ghi chú bằng LINQ |
| Bắt buộc | Dọn phiên khi đóng tab/app, báo lỗi khởi động và xác nhận dừng công việc |
| Tùy chọn, ưu tiên đầu | Ghim công việc — tương đối nhỏ: một bool và thứ tự hiển thị |
| Tùy chọn | Lọc trạng thái, ghi nhớ tab đã mở dưới dạng cấu hình, xuất metadata JSON |
| Tùy chọn thấp | Xuất một đoạn log theo yêu cầu, paste nhiều dòng đúng bracketed-paste; cần thử kỹ |
| Bỏ trong 4 tuần | Giữ tiến trình sống khi app đóng, khôi phục tự động cuộc hội thoại, tự nhận biết agent đã xong |
| Bỏ trong 4 tuần | Hooks bên ngoài, đồng bộ nhiều app/máy, SSH, điều phối agent, nhiều provider, marketplace plugin |
| Bỏ trong 4 tuần | Tự viết terminal emulator, ConPTY/ANSI parser từ đầu, kéo tab giữa cửa sổ, giao diện giống toàn bộ Herdr |

Terminal vẫn là cốt lõi. Nếu hết thời gian mà terminal hoặc agent không ổn, không gọi một app nhật ký thay thế là AgentDesk MVP. Khi đó phải điều chỉnh thời hạn hoặc chọn đề tài khác một cách rõ ràng.

## Phần nào là kiến thức môn?

| Kiến thức | Áp dụng thật trong MVP | Mức cần hiểu |
|---|---|---|
| C#, OOP | Project, WorkItem, TerminalSession; trách nhiệm và vòng đời đối tượng | Phân biệt model lưu DB và đối tượng phiên đang sống |
| Generic | List<T>, ObservableCollection<T>, Task<T> | Biết lợi ích an toàn kiểu; không cần sáng tạo generic repository |
| Delegate/Event | Click, PropertyChanged, TermReady, StateChanged | Biết đăng ký, phát sự kiện, hủy đăng ký và luồng gọi callback |
| LINQ | Tìm/lọc/sắp xếp công việc; chọn tab theo ID | Biết IEnumerable và truy vấn EF không hoàn toàn giống nhau |
| WPF | XAML, binding, DataContext, ICommand, ItemsControl/TabControl | Biết visual tree, Loaded/Unloaded, focus và UI thread |
| EF Core | Quan hệ Project 1–n WorkItem, enum trạng thái, migration, CRUD | DbContext ngắn hạn, SaveChangesAsync, không dùng chung context giữa nhiều luồng |
| Design Pattern | MVVM cho nghiệp vụ, Adapter cho terminal | Giải thích được vấn đề pattern giải quyết |
| File/JSON | Kiểm tra thư mục; xuất metadata nếu còn giờ | Không giả định JSON là snapshot tiến trình |
| Concurrency | Shell chờ/đọc output ở nền; UI cập nhật qua Dispatcher; await khi đóng | Không chặn UI bằng .Wait()/.Result; bắt và hiển thị lỗi |

Phần **khó do tích hợp**, không chỉ do C#: DLL native/x64, ConPTY, phím điều khiển, focus HWND trong WPF, ANSI/VT, IME, resize TUI, shutdown và tiến trình con. AI có thể giúp viết adapter, nhưng bạn vẫn phải hiểu được hợp đồng của adapter và giới hạn của thư viện.

## Kiến trúc đủ nhỏ để bảo vệ

```text
MainWindow + ViewModel nghiệp vụ
  ├─ Project / WorkItem → EF Core → SQLite
  └─ Các TerminalSession đang sống, theo WorkItemId
       ├─ TerminalControl (render/input từ thư viện)
       └─ SessionPty → ConPTY → PowerShell → Codex CLI
```

`Project`: Id, Name, DirectoryPath, Description.

`WorkItem`: Id, ProjectId, Title, Notes, ReviewStatus, IsPinned (nếu làm), UpdatedAt.

Danh sách `TerminalSession` chỉ ở RAM. Không lưu Process, HWND, Task hoặc ConPTY vào DbContext. Có thể lưu OpenOnNextLaunch để hỏi/mở **shell mới**, không lưu PID rồi coi PID đó là phiên cũ. PID có thể được Windows tái sử dụng.

Trạng thái kỹ thuật như Đang chạy / Đã thoát khác `ReviewStatus`. Không đọc output “Done” để tự đánh dấu “Đã kiểm tra”.

Không cần Repository + UnitOfWork + mediator + event bus chỉ để có nhiều pattern. Một service EF nhỏ, các ViewModel nghiệp vụ và terminal adapter là đủ cho quy mô này.

## Bạn viết gì, thư viện làm gì?

**Code của đồ án:** quản lý dự án/công việc; lưu, tìm và sửa dữ liệu; gắn phiên với công việc; tạo/giữ/chọn/đóng phiên; thông báo lỗi; xác nhận dừng; nối clipboard; quyết định lúc nào cập nhật UI; bài test và tài liệu.

**Thư viện:** tạo pseudoconsole/pipe, nối chương trình console với terminal, nhận và render VT/ANSI, xử lý phần lớn input, scrollback, vùng chọn, native HWND/GPU. Bạn không tự viết engine terminal.

**Codex CLI:** giao tiếp model, đăng nhập, tools, hội thoại, màn hình xác nhận của agent. AgentDesk chỉ là host cho tiến trình; không “xây model AI” hay cần biết nội bộ suy luận của model.

Prototype dùng `TermPTY` của EasyWindowsTerminalControl và `TerminalControl` từ dependency. Không chỉ đặt `<EasyTerminalControl />` vì ta cần giữ Task khởi động/thoát, bắt lỗi, dọn tiến trình có kiểm soát. `SessionPty` override vòng đọc để chỉ có một reader ở nền. Đây là phần ngoài mức WPF nhập môn và phải học kỹ trước bảo vệ.

Ví dụ delegate/event ngay trong code:

```csharp
public event EventHandler? StateChanged;

// Đăng ký một phương thức nhận thông báo:
session.StateChanged += OnSessionStateChanged;

// Khi trạng thái đổi, chủ sở hữu phát thông báo:
StateChanged?.Invoke(this, EventArgs.Empty);
```

**Delegate** là kiểu biểu diễn một phương thức có chữ ký xác định; `EventHandler` yêu cầu phương thức nhận sender và EventArgs. **Event** cho bên ngoài đăng ký/hủy đăng ký callback, còn chỉ đối tượng sở hữu mới được phát event. Trong ví dụ, phiên báo thay đổi; cửa sổ cập nhật thanh trạng thái. Delegate/event không tự tạo luồng nền. `TermReady` phát từ luồng nền nên adapter phải chuyển về UI bằng Dispatcher.

`await` cũng không có nghĩa tự tạo thread: `Task.Run` ở đây tách lời gọi thư viện đang block; `await` cho UI trả quyền điều khiển trong lúc chờ. Dùng `.Wait()` trên UI có thể deadlock khi luồng nền đang chờ `Dispatcher.Invoke`.

## Kiểm tra thư viện ứng viên

- Đã đọc README, ví dụ WPF, project file và nguồn quản lý ConPTY/Process. Repository được kiểm tra tại commit `36cbce3683e15e55995e5586a7271ff92a5956fd`, ngày 16/07/2026. Gói NuGet 1.0.38 khai báo repository commit `0741a4b8853c47bcac4412d005ed4ae1d96d2c13`; không giả định branch master chính là binary.
- [NuGet](https://www.nuget.org/packages/EasyWindowsTerminalControl/1.0.38) liệt kê 1.0.38 ngày 16/07/2026; đã restore và build thực tế với .NET 8/x64. Gói CI WPF target .NET 8 và .NET Framework, vì thế không dựa riêng dòng “6.0+” ở README để kết luận tương thích.
- [README](https://github.com/mitchcapper/EasyWindowsTerminalControl) cảnh báo API dựa trên gói beta và nêu hạn chế airspace của HWND. Thanh công cụ/ghi chú nên nằm bên cạnh, không phủ WPF lên terminal. Chọn WPF; không chuyển sang bản WinUI alpha.
- `CI.Microsoft.Terminal.Wpf` do **CI2Nuget Repackage Team** phát hành; tên Microsoft trong tên assembly không biến nó thành control WPF sản phẩm được Microsoft cam kết ổn định. Xem [gói](https://www.nuget.org/packages/CI.Microsoft.Terminal.Wpf/1.25.260303002) và [issue productization](https://github.com/microsoft/terminal/issues/6999).
- ConPTY 1.24.260710001 là gói Microsoft. Metadata nêu Windows 10 build 17763 trở lên; bản thử chỉ thực nghiệm trên Windows 11 hiện có.
- Trang issues của ứng viên trả không có issue mở và hạn chế tạo issue tại lúc đọc; điều này không chứng minh không có lỗi. Lịch sử phát hành ít, chưa có bằng chứng kiểm thử dài ngày với Codex. Vì vậy giữ lockfile, adapter nhỏ và bộ kiểm tra.

**Lựa chọn hiện tại:** giữ ứng viên để tiếp tục spike vì đã có bằng chứng shell/tab/resize/cleanup, chưa chốt cho toàn bộ đồ án. Nếu thao tác Codex thất bại, trước hết đối chiếu cùng CLI trong terminal ngoài app và thử chế độ `--no-alt-screen`. Đây là phép thử chẩn đoán, không phải bằng chứng sửa mọi lỗi.

**Phương án khác có lý do:** WebView2 + [xterm.js](https://xtermjs.org/docs/) + một ConPTY backend C#. xterm.js giải quyết frontend terminal, không tự tạo shell. Phải thêm JS bridge, resize, input, lifecycle và WebView2 runtime; tôi ước lượng thêm 12–20 giờ tích hợp, chưa chạy phương án này. Không coi đây là fallback miễn phí cho người mới. Không dùng TextBox + RedirectStandardOutput hay `SetParent` cửa sổ Windows Terminal như thể đó là tương đương.

## Kế hoạch 4 tuần — tổng 48–60 giờ

| Tuần | Công việc và phân bổ | Giờ | Điều kiện hoàn thành |
|---|---|---:|---|
| 1 | C# event/Task/Dispatcher 3h; WPF bố cục/focus 2–3h; chạy và hiểu spike/checklist 5–6h; ghi quyết định 2–3h | 12–15 | Terminal + agent qua checklist cốt lõi trên máy bảo vệ; giải thích được SessionPty |
| 2 | Binding/MVVM tối thiểu 3–4h; model + EF/SQLite/migration 4–5h; CRUD và kiểm tra đường dẫn 5–6h | 12–15 | Tạo/sửa/xóa dự án và công việc; đóng/mở app còn metadata |
| 3 | Gắn terminal với công việc 4–5h; ghi chú/trạng thái/tìm kiếm LINQ 4–5h; lỗi/đóng/xóa dự án có phiên đang chạy 3–4h; ghim nếu còn 1h | 12–15 | Một luồng hoàn chỉnh: chọn dự án → chạy agent → ghi chú → tự đánh dấu → tìm lại |
| 4 | Regression và máy thứ hai 4–5h; sửa lỗi 3–4h; tài liệu/sơ đồ/demo 3–4h; tập bảo vệ 2h | 12–15 | Build sạch, demo dự phòng, hiểu code, có bảng test và giới hạn |

Các giờ học đã nằm trong bảng, không phải 48–60 giờ code cộng thêm học. Chưa tính xây backend native mới. Không dành tuần 4 cho thêm tính năng.

**Điểm quyết định:** sau 8–10 giờ đầu làm việc thực tế, nếu Codex vẫn không nhập prompt, điều hướng, xác nhận hoặc ngắt được ổn định, đừng tiếp tục trang trí/CRUD để che phần lõi. Dành tối đa phần còn lại của tuần 1 để chốt thư viện; nếu chưa đạt, đánh giá lại thời hạn hoặc đề tài. Nếu terminal đạt nhưng tuần 2 chậm, bỏ ghim/log/JSON trước, giữ terminal và dữ liệu cốt lõi.

## Tự kiểm tra khả năng bảo vệ

Bạn cần trả lời và minh họa được:

1. Vì sao đổi tab giữ `$PID` và biến trong PowerShell? Khác gì restart?
2. TerminalControl, ConPTY, PowerShell và Codex là bốn thành phần nào?
3. Vì sao event không đồng nghĩa concurrency? `await` và `Task.Run` khác nhau ở đây thế nào?
4. Vì sao không chặn Dispatcher bằng `.Result`? Bản sửa reader tránh điều gì?
5. Đóng tab giết phạm vi nào? Daemon tách rời có nằm trong đó không?
6. Metadata nào nằm trong DB? Vì sao mở lại cấu hình không khôi phục tiến trình?
7. Vì sao agent thoát không có nghĩa công việc “Đã kiểm tra”?
8. Nếu terminal không khởi động hoặc thư mục bị xóa, UI báo gì?
9. Bạn tự viết phần nào, sử dụng thư viện phần nào, AI hỗ trợ phần nào?

Nếu chưa giải thích được 1–5 sau tuần đầu, kiến trúc này vẫn quá khó để tự tin bảo vệ dù AI đã tạo được source.

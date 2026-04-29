# Ma trận Phân Vai AI - NumStrata

> 2026-04-18 AI-Tag
> Tài liệu phối hợp đa AI cho dự án Unity NumStrata.

## 1) Nguyên tắc điều hành

- Người quyết định cuối cùng: Bạn (CEO / Product Owner).
- Mục tiêu: Mọi đầu ra của AI phải đồng bộ, kiểm thử được, và xác minh được trong Unity Play Mode.

## 2) Phân vai chính thức

### Bạn (CEO / Product Owner)

- Định nghĩa mục tiêu tính năng, ưu tiên, và tiêu chí chấp nhận.
- Duyệt phạm vi và hướng kiến trúc.
- Chạy Play Mode, đánh giá trải nghiệm, và chốt kết quả cuối.

### Unity Assistant (Technical Lead trong Unity Editor)

- Quản lý ngữ cảnh Unity: Scene, Prefab, Asset, Project Settings.
- Kiểm tra thiết lập trong Editor (missing reference, component setup, layer/tag/input).
- Điều phối thứ tự triển khai để tích hợp vào Unity an toàn.
- Đối chiếu hành vi code với trạng thái thực tế trong Editor.
- Thực hiện chi tiết ví dụ: "Viết chú thích logic bên trong hàm CalculateEquation()."

### Copilot Agent trong VS Code (Code Lead)

- Triển khai script C# và gameplay logic.
- Refactor code để dễ bảo trì và dễ mở rộng.
- Viết unit test cho các logic có tính xác định (toán học, rule, state transition).
- Sửa lỗi compile/runtime liên quan đến code dựa trên log và kết quả test.
- Nếu yêu cầu chưa rõ hoặc còn thiếu thông tin quan trọng, được quyền hỏi lại để xác nhận trước khi thực hiện.

### Figma AI (Design Support)

- Tạo đề xuất layout UI và các phương án biến thể.
- Cung cấp token thiết kế (màu, khoảng cách, typo, hierarchy).
- Bàn giao thông số để triển khai Unity UI nhất quán.

## 3) Ranh giới năng lực (quan trọng)

- Unity Assistant mạnh nhất ở ngữ cảnh tổng thể trong Editor và quan hệ giữa các đối tượng Unity.
- Copilot mạnh nhất ở viết code nhanh, refactor, và tạo test trong workspace.
- Mọi kết luận về ảnh hưởng dây chuyền đều cần xác minh lại bằng Scene/Prefab/runtime trong Unity.

## 4) Quy trình chuẩn mỗi tính năng

1. Bạn mô tả mục tiêu + acceptance criteria.
2. Unity Assistant lập kế hoạch tích hợp Unity (Scene/Prefab/Asset cần đổi).
3. Copilot triển khai code và test theo interface/contract đã thống nhất.
4. Unity Assistant + Bạn xác minh trong Unity Editor và Play Mode.
5. Copilot sửa vòng 2 dựa trên log/feedback thực tế.
6. Bạn duyệt hoàn tất.

## 5) Định nghĩa Hoàn thành (Definition of Done)

- Tính năng đáp ứng acceptance criteria.
- Không có compile error mới trong Unity Console liên quan đến thay đổi.
- Logic cốt lõi có test cơ bản nếu áp dụng được.
- Reference Scene/Prefab hợp lệ, không bị gãy.
- Có ghi chú ngắn cho bước tiếp theo.

## 6) Mẫu bàn giao giữa các AI (khuyến nghị)

Dùng mẫu ngắn sau:

- Context: Đang sửa phần nào, phạm vi nào.
- Constraints: Ràng buộc hiệu năng, nền tảng, thiết kế.
- Task: Yêu cầu triển khai cụ thể.
- Output: Danh sách file/artifact kỳ vọng.
- Validation: Cách test và kết quả mong đợi.

## 7) Quy tắc cập nhật GDD khi đổi gameplay/quy trình

- Nếu có thay đổi về quy trình hoặc nguyên tắc gameplay, agent phải cập nhật lại tài liệu `GDD_DETAILS.md` tương ứng trong cùng đợt xử lý.
- Mọi thay đổi phải gắn nhãn rõ ràng theo 2 loại:
	- **Chính thức (Official):** đã được Bạn phê duyệt để áp dụng làm nguồn sự thật; phải sửa trực tiếp nội dung chuẩn trong `GDD_DETAILS.md`.
	- **Tạm thời (Temporary):** mới là phương án thử nghiệm/thảo luận; không ghi đè luật chính thức, chỉ ghi ở mục ghi chú hoặc trạng thái tạm thời, chờ quyết định cuối.
- Trước khi kết thúc task, agent phải tự kiểm tra các phần liên quan trong `GDD_DETAILS.md` để tránh mâu thuẫn giữa luật cũ và luật mới.
- Nếu chưa rõ thay đổi thuộc Official hay Temporary, mặc định xử lý là Temporary và yêu cầu xác nhận trước khi chuyển thành Official.

---

Trạng thái: Mô hình phân vai này có hiệu lực cho NumStrata từ 2026-04-18.

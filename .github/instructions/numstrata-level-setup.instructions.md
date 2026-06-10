---
name: numstrata-level-setup
description: Dùng khi cần tạo, chỉnh sửa hoặc tính toán các thông số của màn chơi (Level Setup) dựa theo luật của NumStrata GDD. Hướng dẫn thiết lập layout, pool số liệu và layer.
applyTo: ["**/LevelLoader.cs", "**/FormulaManager.cs", "**/*Level*.cs"]
---

# Hướng dẫn thiết lập màn chơi (Level Setup) - NumStrata

1. **Tuân thủ Cấu trúc Layered Grids**: Các Tile được đặt vào Board_Grid (11x11). Anchor của Tile lấy chính giữa (0.5, 0.5) để không bị scale.
2. **Quy tắc Sinh Pool (Spawn Pool)**:
   - $S = O + N$ (Số slot spawn = Số Operator + Số Number tile).
   - $3 \times O \le N \le 4 \times O$.
   - Các phép chia sinh dư $r \neq 0$ bắt buộc phải dùng cho bước tiếp theo.
3. **Deadlock**: Thuật toán spawn phải đảm bảo giải được 100%. Nếu có nguy cơ deadlock, fallback về random hoặc cảnh báo người chơi (chỉ có trong Campaign Mode).
4. **Overlap Tree**: $Tile_B$ đè lên $Tile_A$ nếu Layer của B lớn hơn A và $|X_B - X_A| \le 1, |Y_B - Y_A| \le 1$.
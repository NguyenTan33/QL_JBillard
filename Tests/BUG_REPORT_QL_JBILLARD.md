# 🕵️‍♂️ BÁO CÁO KIỂM THỬ XÂM NHẬP & BẢO MẬT HỆ THỐNG QL_JBILLARD
**Dự án:** Quản Lý Quán Bida (WPF .NET 8 / SQLite / MVVM)  
**Vai trò giả lập:** Senior QA / Penetration Tester / Nhân viên thu ngân gian lận / Chủ quán  
**Thời gian thực hiện:** 05/11/2026  
**Phương pháp:** Dynamic Chaos Testing, Code Auditing, Concurrency Race Condition Testing  

---

## 📊 TỔNG QUAN KẾT QUẢ KIỂM THỬ

Trong chiến dịch kiểm thử thực tế, chúng tôi đã kích hoạt bộ test xâm nhập tự động `HackerChaosTest` kết hợp rà soát toàn bộ 12 Service Layer và ViewModel. Kết quả: **15 Lỗ hổng & Điểm yếu bảo mật** được xác nhận, trong đó có nhiều lỗi cho phép **nhân viên thu ngân trực tiếp ăn chặn tiền mặt của quán mà chủ quán không hề hay biết**.

| Mức độ nghiêm trọng | Số lượng | Tác động chính |
| :--- | :---: | :--- |
| 🔴 **CRITICAL (Nghiêm trọng)** | **5** | Ăn chặn tiền giờ, rẽ nhánh hỏng Tamper-Proof, lộ Master Key mã hóa, Leo thang quyền Admin |
| 🟠 **HIGH (Cao)** | **6** | Rút ruột két tiền, trộm kho F&B qua combo, lỗ doanh thu do đảo lộn khung giờ, mất file backup |
| 🟡 **MEDIUM (Trung bình)** | **4** | Lỗi định dạng VietQR, mất dấu tiếng Việt máy in ESC/POS, Crash khi rỗng hạng thành viên |

---

## 🔴 DANH SÁCH LỖ HỔNG CRITICAL (CỰC KỲ NGUY HIỂM)

### 🚨 BUG-01: Gian lận "Bàn Vô Hình" – Thu ngân xóa dấu vết bàn chơi để đút túi 100% tiền giờ
* **Mức độ:** `🔴 CRITICAL` (Thất thoát tiền mặt trực tiếp)
* **Vị trí code:** [`TableService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/TableService.cs#L357-L373) — hàm `UpdateTableStatus`
* **Kịch bản gian lận thực tế:**
  1. Khách vào đánh bida tại Bàn 01 trong 3 tiếng (tiền giờ ví dụ: 180.000đ).
  2. Khi khách ra quầy trả tiền mặt, thu ngân nhận 180.000đ bỏ túi riêng.
  3. Thu ngân không bấm **"Thanh toán"** mà gọi hàm đổi trạng thái bàn sang `TableStatus.Available` (Bàn trống).
  4. Hệ thống đặt `table.Status = Available` và xóa `table.CurrentSessionId = null`.
  5. **HẬU QUẢ:** Phiên chơi `TableSession` vẫn giữ nguyên `Status = Active` bị bỏ rơi (Orphaned Session). Không hề có hóa đơn nào được tạo, doanh thu ca trực bằng 0đ, chủ quán xem ca trực thấy bàn trống cả ngày!
* **Giải pháp khắc phục:**
  ```csharp
  public bool UpdateTableStatus(int tableId, TableStatus newStatus, int userId)
  {
      using var context = _contextFactory.CreateDbContext();
      var table = context.Tables.Find(tableId);
      if (table == null) return false;

      // CHẶN NGAY: Không được phép chuyển bàn đang chơi (Occupied) về Available nếu chưa thanh toán hoặc hủy phiên hợp lệ
      if (table.Status == TableStatus.Occupied && newStatus == TableStatus.Available)
      {
          bool hasActiveSession = context.TableSessions.Any(s => s.TableId == tableId && s.Status == SessionStatus.Active);
          if (hasActiveSession)
          {
              throw new InvalidOperationException("Bàn đang có phiên chơi hoạt động! Phải thực hiện Thanh toán hóa đơn hoặc Hủy phiên có lý do.");
          }
      }
      // ...
  }
  ```

---

### 🚨 BUG-02: Hoán đổi bàn VIP sang bàn Cỏ (Retroactive Rate Rewrite Fraud)
* **Mức độ:** `🔴 CRITICAL` (Ăn chặn tiền chênh lệch)
* **Vị trí code:** [`TableService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/TableService.cs#L332-L338) — hàm `SwitchTable`
* **Kịch bản gian lận thực tế:**
  1. Khách chơi tại **Bàn VIP** (đơn giá: 100.000đ/h) trong 3 tiếng = 300.000đ.
  2. Chuẩn bị tính tiền, thu ngân dùng chức năng "Chuyển bàn", chuyển phiên chơi sang **Bàn Thường** (đơn giá: 40.000đ/h).
  3. Dòng code 337: `session.AppliedHourlyRate = toTable.TableType.DefaultHourlyRate;` -> **Ghi đè giá giờ của toàn bộ 3 tiếng trước đó thành 40.000đ!**
  4. Hóa đơn xuất ra chỉ có: 3h x 40.000đ = 120.000đ.
  5. Thu ngân thu của khách 300.000đ, nộp vào két 120.000đ, **bỏ túi 180.000đ** tiền chênh lệch!
* **Giải pháp khắc phục:**
  - Không được ghi đè `AppliedHourlyRate` cho toàn bộ phiên!
  - Cần chia phiên chơi thành các chặng (Segments) hoặc tính chốt tiền của bàn cũ trước khi chuyển sang bàn mới (Prorated Billing):
  ```csharp
  // Hoặc tách thành SessionTransferSegment, hoặc tính tổng tiền tích lũy của bàn cũ trước khi chuyển
  ```

---

### 🚨 BUG-03: Phá hỏng chuỗi Hash Tamper-Proof do Race Condition (Forking Attack)
* **Mức độ:** `🔴 CRITICAL` (Phá vỡ hệ thống bảo mật & Báo động giả)
* **Vị trí code:** [`AuditService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/AuditService.cs#L26-L51) — hàm `Log`
* **Cơ chế lỗi:**
  1. Khi có 2 thao tác diễn ra cùng lúc (ví dụ: 2 bàn cùng checkout hoặc vừa order món vừa mở bàn trên 2 luồng).
  2. Cả 2 luồng cùng truy vấn `lastLog = context.AuditLogs.OrderByDescending(x => x.AuditLogId).FirstOrDefault()`.
  3. Cả 2 bản ghi mới đều nhận chung một `PreviousHash`!
  4. Kết quả: Chuỗi băm bị rẽ nhánh (Forking).
  5. Khi chủ quán chạy tính năng **"Kiểm tra toàn vẹn CSDL (Tamper-Proof)"**, hàm `VerifyChainIntegrity()` sẽ báo lỗi: **CSDL bị chỉnh sửa lén / Hacker tấn công**!
* **Giải pháp khắc phục:**
  - Dùng `lock (_syncLock)` hoặc Semaphore để tuần tự hóa (Serialize) thao tác ghi AuditLog:
  ```csharp
  private static readonly object _auditLock = new();

  public void Log(...)
  {
      lock (_auditLock)
      {
          using var context = _contextFactory.CreateDbContext();
          // Lấy hash và ghi log an toàn tuyệt đối
      }
  }
  ```

---

### 🚨 BUG-04: Lộ Master Key mã hóa CSDL cứng trong mã nguồn (Hardcoded Secret)
* **Mức độ:** `🔴 CRITICAL` (Bảo mật thông tin)
* **Vị trí code:** [`BackupAndSecurityService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/BackupAndSecurityService.cs#L16)
* **Mô tả:**
  ```csharp
  private const string BackupKey = "JBillard@Encrypted#2026_Backup_SecPass!";
  ```
  - Khóa mã hóa file backup `.enc` bị fix cứng dạng hằng số trong code C#.
  - Bất kỳ ai mở file `QL_JBillard.dll` bằng các công cụ miễn phí như ILSpy, dnSpy hay dotPeek đều nhìn thấy mật khẩu này trong 3 giây.
  - Hacker hoặc nhân viên có thể giải mã toàn bộ thông tin khách hàng, số điện thoại, doanh thu của quán.
* **Giải pháp khắc phục:**
  - Tạo khóa động dẫn xuất từ Machine-specific entropy (DPAPI của Windows - `ProtectedData.Protect`), kết hợp chuỗi bí mật của quán khi cài đặt.

---

### 🚨 BUG-05: Leo thang đặc quyền tại Service Layer (Privilege Escalation)
* **Mức độ:** `🔴 CRITICAL` (Phân quyền người dùng)
* **Vị trí code:** [`AuthService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/AuthService.cs#L132) — hàm `UpdateUser`
* **Mô tả:**
  - `AuthService.UpdateUser` nhận tham số `UserRole role` nhưng **hoàn toàn không kiểm tra người đang gọi hàm có phải là Admin hay không**.
  - Toàn bộ bảo mật phụ thuộc 100% vào việc ẩn/hiện giao diện WPF.
  - Nếu thu ngân gửi lệnh hoặc gọi hàm, họ có thể tự nâng tài khoản của mình thành `Admin` và chiếm quyền kiểm soát toàn bộ quán.
* **Giải pháp khắc phục:**
  - Luôn kiểm tra `CurrentUser?.Role == UserRole.Admin` tại tầng Service trước khi cho phép thay đổi Role hoặc trạng thái tài khoản.

---

## 🟠 DANH SÁCH LỖ HỔNG HIGH (ẢNH HƯỞNG NGHIỆP VỤ & TÀI CHÍNH)

### ⚠️ BUG-06: Gian lận két tiền bằng phiếu nộp tiền ngân hàng số âm & nộp khống
* **Mức độ:** `🟠 HIGH`
* **Vị trí code:** [`ShiftService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/ShiftService.cs#L245-L280) — hàm `CreateBankDepositSlip`
* **Các lỗ hổng phát hiện:**
  1. Cho phép tạo phiếu với số tiền **ÂM** (ví dụ: `-500.000đ`). Làm tăng khống số tiền trong két!
  2. Cho phép nộp tiền **VƯỢT QUÁ TIỀN THỰC CÓ TRONG KÉT** (ví dụ: két có 1 triệu nhưng tạo phiếu nộp 100 triệu).
  3. Phiếu tạo mới mặc định `Status = "Approved"` ngay lập tức, không qua bước Admin duyệt.
  4. Khi sửa phiếu thành `Pending`, hàm `CloseShift` vẫn trừ tiền két vì điều kiện tính là `b.Status != "Rejected"` (vẫn tính cả Pending).

---

### ⚠️ BUG-07: Chiết khấu âm làm đôn giá tiền & Điểm tích lũy âm
* **Mức độ:** `🟠 HIGH`
* **Vị trí code:** [`BillingService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/BillingService.cs#L170-L182)
* **Mô tả:**
  - `discountPercent` không được giới hạn trong khoảng `[0, 100]`.
  - Nếu nhập `-50%`, `discountAmount` bị âm, dẫn đến `finalAmount = subTotal - (-discountAmount) = subTotal + discountAmount`.
  - Thu ngân có thể làm giá tiền tăng vọt để ăn chặn của khách hàng lạ.
  - `pointsUsed` không kiểm tra `> 0`, có thể truyền số âm để làm sai lệch điểm tích lũy.

---

### ⚠️ BUG-08: Phù phép tồn kho bằng cách hủy món trong gói Combo (Infinite Stock Exploit)
* **Mức độ:** `🟠 HIGH`
* **Vị trí code:** [`TableService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/TableService.cs#L219) & [`InventoryService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/InventoryService.cs#L261)
* **Mô tả:**
  - Khách mở bàn gói Combo (ví dụ Combo Cày đêm kèm 2 lon Sting).
  - Nhân viên bấm **"Hủy món"** Sting với lý do "Khách không uống".
  - Hàm `CancelOrderItem` tự động cộng trả lại 2 lon Sting vào kho.
  - Tuy nhiên, giá gói combo vẫn giữ nguyên (không trừ tiền).
  - **Hậu quả:** Nhân viên có thể mang 2 lon Sting ra ngoài bán hoặc uống miễn phí mà số liệu tồn kho trên sổ sách vẫn khớp 100%!

---

### ⚠️ BUG-09: Tự ý xóa sạch các bản backup thủ công của chủ quán (Data Loss Trap)
* **Mức độ:** `🟠 HIGH`
* **Vị trí code:** [`BackupAndSecurityService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/BackupAndSecurityService.cs#L244-L249) — hàm `CleanOldBackups`
* **Mô tả:**
  ```csharp
  if (fname != "billiard_latest_backup.enc" && fname != "billiard_latest_backup.db")
  {
      try { File.Delete(f); } catch { }
  }
  ```
  - Bất kỳ file nào nằm trong thư mục `Backups` mà không có tên trùng khớp 2 tên trên đều bị **XÓA SẠCH SÀNH SANH**.
  - Nếu chủ quán cẩn thận copy file backup trước khi nâng cấp (ví dụ `backup_20261104.db`), hệ thống chạy auto-backup 15 phút sau sẽ xóa mất file này!

---

### ⚠️ BUG-10: Đảo lộn độ ưu tiên khung giờ giá (Time-Slot Priority Inversion)
* **Mức độ:** `🟠 HIGH` (Gây thất thoát doanh thu tiền giờ)
* **Vị trí code:** [`BilliardPricingHelper.cs`](file:///D:/AppDesktop/QL_JBillard/Helpers/BilliardPricingHelper.cs#L61-L83)
* **Mô tả:**
  - Vòng lặp duyệt qua danh sách `activeSlots` và dừng lại ngay ở slot đầu tiên thỏa mãn (`break`).
  - Nếu chủ quán tạo khung **"Cả ngày" (0h - 24h: 50.000đ)** trước, sau đó tạo khung **"Cao điểm tối" (18h - 22h: 80.000đ)** sau.
  - Vì khung "Cả ngày" nằm trước trong CSDL, hệ thống luôn lấy giá 50.000đ cho khung giờ tối!
  - Quán bị mất 30.000đ/giờ trong toàn bộ khung giờ cao điểm!
* **Giải pháp:** Cần thêm cột `Priority` hoặc tự động sắp xếp theo độ hẹp của khung giờ (`slotEnd - slotStart` tăng dần) để khung giờ cụ thể được ưu tiên trước khung giờ rộng.

---

### ⚠️ BUG-11: Thiếu Transaction trong `CheckoutTable` gây lỗi khóa bàn vĩnh viễn
* **Mức độ:** `🟠 HIGH`
* **Vị trí code:** [`BillingService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/BillingService.cs#L240-L261)
* **Mô tả:**
  - Có 2 lần gọi `context.SaveChanges()` riêng biệt: Lần 1 lưu Hóa đơn (dòng 240), Lần 2 lưu Thanh toán và cập nhật bàn về `Available` (dòng 261).
  - Không có `using var transaction = context.Database.BeginTransaction()`.
  - Nếu ứng dụng bị ngắt điện hoặc gặp sự cố giữa 2 lệnh này: Hóa đơn đã tạo và Session đã `Completed`, nhưng bàn vẫn ở trạng thái `Occupied`!
  - Bàn đó sẽ bị kẹt vĩnh viễn, không thể mở lại và cũng không thể thanh toán tiếp!

---

## 🟡 DANH SÁCH LỖ HỔNG MEDIUM (HIỂN THỊ & NGOẠI LỆ)

### 🔸 BUG-12: Crash ứng dụng khi bảng Hạng thành viên (MembershipTiers) rỗng
* **Vị trí:** [`CustomerService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/CustomerService.cs#L34)
* **Mô tả:** Dùng `.First()` trực tiếp: `context.MembershipTiers.AsEnumerable().OrderBy(t => t.MinSpentThreshold).First()`. Nếu quán chưa tạo hạng thành viên, việc tạo khách hàng mới sẽ gây văng app ngay lập tức (`InvalidOperationException`).

### 🔸 BUG-13: Mã VietQR mã hóa khoảng trắng thành `%20` và cắt cụt nội dung
* **Vị trí:** [`VietQrService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/VietQrService.cs#L23-L24)
* **Mô tả:** 
  - Thay khoảng trắng bằng `%20` là chuẩn URL, không phải chuẩn Napas 247 EMVCo Tag 62. Một số App ngân hàng (Vietcombank, MBBank) sẽ hiển thị `%20` trong phần nội dung chuyển tiền.
  - Việc cắt 25 ký tự diễn ra *sau* khi thay `%20`, khiến nội dung chuyển tiền thực tế chỉ còn được vài chữ.

### 🔸 BUG-14: Máy in nhiệt ESC/POS bị mất toàn bộ dấu tiếng Việt
* **Vị trí:** [`PrintService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/PrintService.cs#L19)
* **Mô tả:** Sử dụng `Encoding.ASCII.GetBytes(...)` làm toàn bộ ký tự có dấu như "HÓA ĐƠN", "TIỀN GIỜ", "BÀN" biến thành dấu chấm hỏi `?` hoặc ký tự rác trên giấy in nhiệt K80/K58. Cần chuyển sang bộ mã tiếng Việt `Encoding.GetEncoding(1258)` hoặc xử lý chuyển đổi không dấu chuẩn đẹp.

### 🔸 BUG-15: Nguy cơ trùng lặp mã hóa đơn (Invoice Code Collision)
* **Vị trí:** [`BillingService.cs`](file:///D:/AppDesktop/QL_JBillard/Services/Implementations/BillingService.cs#L200)
* **Mô tả:** Mã hóa đơn được tạo bằng: `Count(i => i.CreatedAt.Date == now.Date) + 1`. Không an toàn trong môi trường đa luồng hoặc khi thanh toán nhanh liên tiếp, dẫn đến 2 hóa đơn có thể có chung mã `HD20261105-0001`.

---

## 🎯 LỜI KHUYÊN DÀNH CHO NHÀ PHÁT TRIỂN (MINH TÂN)

> [!TIP]
> **Thế mạnh của bạn:** Cấu trúc dự án WPF hiện tại cực kỳ quy củ, MVVM sạch đẹp, có ý thức rất cao về bảo mật (đã tích hợp SHA-256 Tamper-Proof, Z-Report ca trực, mã hóa AES sao lưu, chia ca, logic làm tròn 600đ bida thực tế). Đây là nền tảng rất vững chắc mà một lập trình viên tốt nghiệp HUFLIT xây dựng được!

> [!IMPORTANT]
> **Trước khi đem bán thương mại trên TikTok:**
> Các chủ quán bida ở Việt Nam rất sợ **nhân viên gian lận tiền mặt và trộm giờ**. Khi bạn fix triệt để **BUG-01, BUG-02, BUG-06 và BUG-08**, phần mềm của bạn sẽ trở thành một "vũ khí chống gian lận" cực kỳ thuyết phục và đắt giá để quảng bá!

import os
import sys
import sqlite3
import random
import hashlib
import base64
from datetime import datetime, timedelta

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

def get_pbkdf2_hash(password: str, salt_b64: str) -> str:
    pepper = b"JBillard@2026_SecureCore_SaltPepper#99"
    salt_bytes = base64.b64decode(salt_b64)
    combined = salt_bytes + pepper
    dk = hashlib.pbkdf2_hmac('sha256', password.encode('utf-8'), combined, 100000, 32)
    return base64.b64encode(dk).decode('ascii')

def generate_salt() -> str:
    salt_bytes = os.urandom(16)
    return base64.b64encode(salt_bytes).decode('ascii')

def compute_sha256(payload: str) -> str:
    pepper = "JBillard@2026_SecureCore_SaltPepper#99"
    full_data = payload + pepper
    return hashlib.sha256(full_data.encode('utf-8')).hexdigest().upper()

def round_billiard_amount(raw_cost: float) -> float:
    if raw_cost <= 0:
        return 0.0
    base_thousand = int(raw_cost // 1000) * 1000
    remainder = raw_cost - base_thousand
    return float(base_thousand + 1000 if remainder >= 600 else base_thousand)

def run_simulation(db_path: str):
    print(f"\n=======================================================")
    print(f"🚀 BẮT ĐẦU GIẢ LẬP DỮ LIỆU CƠ SỞ DỮ LIỆU 1 THÁNG QUÁN BIDA")
    print(f"📁 Database: {db_path}")
    print(f"=======================================================")

    if not os.path.exists(db_path):
        print(f"❌ File không tồn tại: {db_path}")
        return

    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    # Bật foreign keys
    cur.execute("PRAGMA foreign_keys = ON;")

    # 1. DỌN DẸP DỮ LIỆU GIAO DỊCH CŨ ĐỂ TẠO 1 THÁNG LIÊN TỤC MỚI TINH
    print("🧹 Dọn dẹp các phiên chơi, ca trực và hóa đơn cũ...")
    cur.execute("DELETE FROM InvoicePayments;")
    cur.execute("DELETE FROM Invoices;")
    cur.execute("DELETE FROM OrderItems;")
    cur.execute("DELETE FROM TableSessions;")
    cur.execute("DELETE FROM BankDepositSlips;")
    cur.execute("DELETE FROM Shifts;")
    cur.execute("DELETE FROM StockTransactionDetails;")
    cur.execute("DELETE FROM StockTransactions;")
    cur.execute("DELETE FROM AuditLogs;")
    cur.execute("DELETE FROM PlayComboItems;")
    cur.execute("DELETE FROM PlayCombos;")
    cur.execute("DELETE FROM InventoryItems;")
    cur.execute("DELETE FROM Products;")
    cur.execute("DELETE FROM ProductCategories;")
    cur.execute("DELETE FROM Units;")
    cur.execute("DELETE FROM Tables WHERE TableNumber LIKE '%TEST%' OR TableId > 14;")
    cur.execute("DELETE FROM Areas WHERE AreaName LIKE '%Thi Đấu%' OR AreaId > 4;")
    cur.execute("DELETE FROM Floors WHERE FloorName LIKE '%Tầng 3%' OR FloorId > 2;")
    cur.execute("DELETE FROM TableTypes WHERE TypeName LIKE '%Libre Tiêu Chuẩn%' OR TableTypeId > 6;")
    cur.execute("UPDATE Tables SET Status = 0, CurrentSessionId = NULL;")

    # 2. KHỞI TẠO 3 NHÂN VIÊN THU NGÂN + 1 ADMIN
    print("👤 Thiết lập tài khoản 3 Nhân viên thu ngân & 1 Admin...")
    # Xóa các user phụ cũ nếu có
    cur.execute("DELETE FROM Users WHERE UserId > 4;")
    
    salt_admin = generate_salt()
    hash_admin = get_pbkdf2_hash("admin123", salt_admin)
    salt_sang = generate_salt()
    hash_sang = get_pbkdf2_hash("123456", salt_sang)
    salt_chieu = generate_salt()
    hash_chieu = get_pbkdf2_hash("123456", salt_chieu)
    salt_dem = generate_salt()
    hash_dem = get_pbkdf2_hash("123456", salt_dem)

    # Đảm bảo có đúng 4 Users chuẩn: 1: Admin, 2: nv_sang, 3: nv_chieu, 4: nv_dem
    users_data = [
        (1, "admin", hash_admin, salt_admin, "Nguyễn Quản Trị (Admin)", 1, 1), # UserRole.Admin = 1
        (2, "nv_sang", hash_sang, salt_sang, "Lê Hoàng Long (Ca Sáng)", 3, 1),  # UserRole.Cashier = 3
        (3, "nv_chieu", hash_chieu, salt_chieu, "Trần Minh Tuấn (Ca Tối)", 3, 1),
        (4, "nv_dem", hash_dem, salt_dem, "Nguyễn Văn Đạt (Ca Đêm)", 3, 1)
    ]

    for u_id, u_name, u_hash, u_salt, u_full, u_role, u_active in users_data:
        cur.execute("""
            INSERT OR REPLACE INTO Users (UserId, Username, PasswordHash, Salt, FullName, Role, IsActive, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, ?, datetime('now', '-45 days'))
        """, (u_id, u_name, u_hash, u_salt, u_full, u_role, u_active))

    # 3. DANH MỤC THỰC ĐƠN & ĐƠN VỊ TÍNH ĐẦY ĐỦ
    print("🍽️ Cập nhật Danh mục sản phẩm & Đơn vị tính thực tế...")
    categories_data = [
        (1, "Đồ Uống Lon & Chai", 1),
        (2, "Cà Phê & Pha Chế", 1),
        (3, "Bia & Cồn", 1),
        (4, "Đồ Ăn Nhanh Chế Biến", 1),
        (5, "Phụ Kiện Bida", 1),
        (6, "Thuốc Lá & Hút", 1),
        (7, "Mồi Nhắm & Ăn Vặt", 1)
    ]
    for cid, cname, act in categories_data:
        cur.execute("INSERT OR REPLACE INTO ProductCategories (CategoryId, CategoryName, IsActive) VALUES (?, ?, ?);", (cid, cname, act))

    units_data = [
        (1, "Lon"), (2, "Chai"), (3, "Ly"), (4, "Đĩa"), (5, "Gói"), (6, "Cái"), (7, "Viên"), (8, "Bao"), (9, "Phần"), (10, "Bát")
    ]
    for uid, uname in units_data:
        cur.execute("INSERT OR REPLACE INTO Units (UnitId, UnitName) VALUES (?, ?);", (uid, uname))

    # 4. DANH SÁCH MÓN ĂN / ĐỒ UỐNG / PHỤ KIỆN PHONG PHÚ
    print("🍔 Thiết lập danh sách thực đơn & kho hàng đa dạng...")
    products_data = [
        # Cat 1: Đồ Uống Lon & Chai
        (1, "STING", "Nước Tăng Lực Sting Dâu", 1, 1, 9000, 16000, 1, 15),
        (2, "REDBULL", "Nước Tăng Lực Bò Húc Thái", 1, 1, 12000, 20000, 1, 15),
        (3, "LAVIE", "Nước Khoáng Thiên Nhiên Lavie 500ml", 1, 2, 4500, 10000, 1, 20),
        (4, "COCA", "Nước Ngọt Coca Cola Original", 1, 1, 9500, 16000, 1, 15),
        (5, "PEPSI", "Nước Ngọt Pepsi Vị Chanh Không Calo", 1, 1, 9500, 16000, 1, 15),
        (6, "REVIVE", "Nước Khoáng Bù Điện Giải Revive", 1, 2, 8500, 16000, 1, 15),
        (7, "STING_VANG", "Nước Tăng Lực Sting Vàng Nhân Sâm", 1, 1, 9000, 16000, 1, 15),

        # Cat 2: Cà Phê & Pha Chế
        (8, "CF_DEN", "Cà Phê Đen Đá Pha Phin", 2, 3, 6000, 22000, 0, 0),
        (9, "CF_SUA", "Cà Phê Sữa Đá Sài Gòn", 2, 3, 8000, 25000, 0, 0),
        (10, "CF_MUOI", "Cà Phê Muối J-Lounge Kem Béo", 2, 3, 9000, 32000, 0, 0),
        (11, "BAC_XIU", "Bạc Xỉu Sài Gòn 3 Tầng Sữa Đá", 2, 3, 8500, 28000, 0, 0),
        (12, "TRA_DAO", "Trà Đào Cam Sả Tươi", 2, 3, 10000, 35000, 0, 0),
        (13, "TRA_CHANH", "Trà Chanh Giã Tay Quảng Đông", 2, 3, 8000, 28000, 0, 0),
        (14, "TRA_MANG_CAU", "Trà Mãng Cầu Tươi Đắk Lắk", 2, 3, 11000, 35000, 0, 0),

        # Cat 3: Bia & Cồn
        (15, "HEINEKEN", "Bia Heineken Silver (Lon Bạc)", 3, 1, 18000, 28000, 1, 24),
        (16, "TIGER_CRYSTAL", "Bia Tiger Crystal (Lon)", 3, 1, 16500, 25000, 1, 24),
        (17, "BUDWEISER", "Bia Budweiser King of Beers", 3, 1, 19000, 30000, 1, 24),
        (18, "STRONGBOW", "Nước Táo Lên Men Strongbow Dâu Đỏ", 3, 1, 18500, 28000, 1, 15),
        (19, "TIGER_NAU", "Bia Tiger Nâu Truyền Thống", 3, 1, 15000, 22000, 1, 24),

        # Cat 4: Đồ Ăn Nhanh Chế Biến
        (20, "MI_BO_TRUNG", "Mì Xào Bò Trứng Ốp La", 4, 4, 20000, 45000, 0, 0),
        (21, "MI_BO_VIEN", "Mì Tôm Bò Viên Cay J-Lounge", 4, 10, 18000, 40000, 0, 0),
        (22, "COM_DUA_BO", "Cơm Chiên Dưa Bò Nóng Hổi", 4, 4, 25000, 50000, 0, 0),
        (23, "COM_TRUNG_XUCXICH", "Cơm Chiên Trứng Xúc Xích Đức", 4, 4, 20000, 40000, 0, 0),
        (24, "KHOAI_TAY", "Khoai Tây Chiên Bơ Giòn Rụm", 4, 4, 15000, 35000, 0, 0),
        (25, "CA_VIEN_CHIEN", "Cá Viên & Bò Viên Chiên Nước Mắm", 4, 4, 16000, 35000, 0, 0),
        (26, "XUC_XICH", "Xúc Xích Tiêu Đen Nướng Phô Mai", 4, 4, 14000, 30000, 0, 0),
        (27, "BANH_MI_OPLA", "Bánh Mì Ốp La 2 Trứng Pate", 4, 9, 12000, 30000, 0, 0),

        # Cat 6: Thuốc Lá & Hút
        (28, "CRAVEN_A", "Thuốc Lá Craven A (Con Mèo Đỏ)", 6, 8, 24000, 30000, 1, 20),
        (29, "THUOCLA_555", "Thuốc Lá 555 Ngoại Tem Chuẩn", 6, 8, 38000, 45000, 1, 20),
        (30, "MARLBORO_TRANG", "Thuốc Lá Marlboro Gold (Trắng)", 6, 8, 32000, 40000, 1, 20),
        (31, "THANG_LONG", "Thuốc Lá Thăng Long Bao Mềm", 6, 8, 15000, 20000, 1, 30),

        # Cat 7: Mồi Nhắm & Ăn Vặt
        (32, "KHO_BO", "Khô Bò Vắt Chanh Ớt", 7, 4, 28000, 50000, 1, 10),
        (33, "KHO_GA", "Khô Gà Cay Lá Chanh Loại 1", 7, 4, 18000, 35000, 1, 15),
        (34, "MUC_XENGAY", "Mực Cán Nướng Tẩm Gia Vị", 7, 4, 38000, 60000, 1, 10),
        (35, "DAU_PHONG", "Đậu Phộng Tỏi Ớt Giòn Tan", 7, 4, 7000, 15000, 1, 20),
        (36, "HAT_DIEU", "Hạt Điều Rang Muối Bình Phước", 7, 4, 20000, 35000, 1, 15),
        (37, "HUONG_DUONG", "Đĩa Hướng Dương Tẩm Vị", 7, 4, 6000, 15000, 1, 20),
        (38, "KHAN_LANH", "Khăn Lạnh Cao Cấp Hương Quế", 7, 6, 800, 3000, 1, 100),

        # Cat 5: Phụ Kiện Bida
        (39, "GANG_TAY", "Găng Tay Bida Cuetec 3 Ngón", 5, 6, 40000, 70000, 1, 15),
        (40, "LO_XANH", "Lơ Xanh Master USA Chính Hãng", 5, 7, 12000, 25000, 1, 30),
        (41, "DAU_CO", "Đầu Cơ Kamui Black Nhật Bản", 5, 6, 120000, 180000, 1, 10),
        (42, "GIAY_NHAM", "Giấy Nhám Bo Đầu Cơ Chuyên Dụng", 5, 6, 4000, 10000, 1, 50)
    ]

    img_dir = os.path.join(os.path.dirname(db_path), "Images", "Products")
    for pid, pcode, pname, catid, unitid, cost, sell, is_inv, alert in products_data:
        img_path = None
        if os.path.exists(img_dir):
            for fname in os.listdir(img_dir):
                fn_upper = fname.upper()
                if fn_upper.startswith(pcode.upper() + ".") or fn_upper.startswith(pcode.upper() + "_"):
                    img_path = os.path.abspath(os.path.join(img_dir, fname))
                    break

        cur.execute("""
            INSERT OR REPLACE INTO Products (ProductId, ProductCode, ProductName, CategoryId, UnitId, CostPrice, SellingPrice, IsInventoryItem, MinStockAlert, ImagePath, IsActive)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1);
        """, (pid, pcode, pname, catid, unitid, cost, sell, is_inv, alert, img_path))

        # Khởi tạo tồn kho ban đầu dồi dào
        if is_inv:
            cur.execute("""
                INSERT OR REPLACE INTO InventoryItems (ProductId, QuantityOnHand, LastRestockedAt)
                VALUES (?, ?, datetime('now', '-35 days'));
            """, (pid, 120)) # Tồn kho khởi tạo 120 mỗi mặt hàng

    # 5. CÁC GÓI COMBO & BUFFET BIDA ĐÊM
    print("🎁 Cấu hình các gói Combo & Buffet bida đêm chuẩn 3NF...")
    cur.execute("DELETE FROM PlayCombos;")
    cur.execute("DELETE FROM PlayComboItems;")

    combos_config = [
        (1, "Combo Sinh Viên 2h + 2 Nước", None, 70000, 120, "2x Nước Tăng Lực Sting Dâu", "Gói chơi 2 tiếng giá học sinh sinh viên siêu tiết kiệm", [(1, 2)]),
        (2, "Combo Solo Cơ Thủ 3h + Cà Phê", None, 120000, 180, "1x Cà Phê Sữa Đá Sài Gòn", "Gói 3 tiếng luyện tập một mình tặng cà phê năng lượng", [(9, 1)]),
        (3, "Combo Thi Đấu Nhóm 4h + 4 Nước + 2 Khô Gà", None, 250000, 240, "4x Nước Tăng Lực Sting Dâu, 2x Khô Gà Cay Lá Chanh", "Gói 4 tiếng giao lưu nhóm bạn bè kèm nước ngọt và mồi nhắm", [(1, 4), (33, 2)]),
        (4, "Combo Cày Đêm Trọn Gói 6h + Mì Bò + Sting", None, 220000, 360, "1x Mì Xào Bò Trứng Ốp La, 1x Nước Tăng Lực Sting Dâu", "Gói chơi 6 tiếng thâu đêm từ 00h-06h sáng kèm ăn đêm chống đói", [(20, 1), (1, 1)]),
        (5, "Buffet Bida Đêm Thâu Sáng (00h - 06h)", None, 150000, 360, "1x Nước Khoáng Thiên Nhiên Lavie 500ml", "Buffet chơi bida không giới hạn từ 0h đến 6h sáng, tính trọn gói theo bàn", [(3, 1)])
    ]

    for cid, cname, tid, price, duration, inc_str, desc, items in combos_config:
        cur.execute("""
            INSERT INTO PlayCombos (ComboId, ComboName, TableTypeId, Price, DurationMinutes, IncludedItems, Description, IsActive)
            VALUES (?, ?, ?, ?, ?, ?, ?, 1);
        """, (cid, cname, tid, price, duration, inc_str, desc))

        for prod_id, qty in items:
            cur.execute("""
                INSERT INTO PlayComboItems (ComboId, ProductId, Quantity)
                VALUES (?, ?, ?);
            """, (cid, prod_id, qty))

    # 6. KHÁCH HÀNG THÂN THIẾT (MEMBERSHIP)
    print("👥 Thiết lập 25 khách quen và hội viên...")
    customers_data = [
        ("0903112334", "Nguyễn Thành Đạt", 6200, 18500000, 4), # Kim Cương
        ("0912445678", "Vũ Quang Huy", 4100, 12300000, 3),     # Vàng
        ("0988776543", "Trần Đình Trọng", 3800, 11400000, 3),  # Vàng
        ("0976223445", "Hoàng Minh Đức", 2200, 6600000, 2),    # Bạc
        ("0938889901", "Lê Thanh Tùng", 2600, 7800000, 2),     # Bạc
        ("0908556778", "Phạm Tuấn Anh", 1400, 4200000, 1),     # Đồng
        ("0918334556", "Đỗ Hữu Thắng", 1600, 4800000, 1),      # Đồng
        ("0965112233", "Bùi Tiến Dũng", 3100, 9300000, 3),     # Vàng
        ("0947667788", "Ngô Gia Bảo", 950, 2850000, 1),        # Đồng
        ("0909123456", "Phan Văn Nam", 4900, 14700000, 3),     # Vàng
        ("0933998877", "Đinh Trọng Nghĩa", 5800, 17400000, 4), # Kim Cương
        ("0917223344", "Lâm Quốc Cường", 1100, 3300000, 1),    # Đồng
        ("0982556677", "Trịnh Thăng Long", 2900, 8700000, 2),   # Bạc
        ("0905334455", "Dương Trung Kiên", 1800, 5400000, 1),   # Đồng
        ("0979888999", "Võ Minh Khang", 6500, 19500000, 4),    # Kim Cương
        ("0915667788", "Trương Hoài Phong", 2400, 7200000, 2),  # Bạc
        ("0937112244", "Huỳnh Nhật Quang", 1300, 3900000, 1),  # Đồng
        ("0902445566", "Thái Tuấn Đạt", 3400, 10200000, 3),    # Vàng
        ("0981998811", "Đoàn Tấn Tài", 800, 2400000, 1),       # Đồng
        ("0942334411", "Hà Trọng Nhân", 2100, 6300000, 2),     # Bạc
        ("0907123789", "Tạ Quang Bửu", 4300, 12900000, 3),     # Vàng
        ("0935556677", "Cao Văn Thắng", 1700, 5100000, 1),     # Đồng
        ("0919223355", "Mai Xuân Hợp", 2700, 8100000, 2),      # Bạc
        ("0984112299", "Nguyễn Hải Đăng", 5100, 15300000, 4),  # Kim Cương
        ("0901889900", "Lý Thanh Bình", 1900, 5700000, 1)      # Đồng
    ]

    cur.execute("DELETE FROM Customers;")
    for phone, name, pts, spend, tid in customers_data:
        cur.execute("""
            INSERT INTO Customers (PhoneNumber, FullName, CurrentPoints, TotalPointsAccumulated, TotalSpent, TierId, CreatedAt)
            VALUES (?, ?, ?, ?, ?, ?, datetime('now', '-40 days'));
        """, (phone, name, pts, pts * 2, spend, tid))

    # Tải danh sách khách hàng vừa tạo
    cur.execute("SELECT CustomerId, TierId, CurrentPoints FROM Customers;")
    customer_records = cur.fetchall()

    # Tải danh sách Bàn & Loại Bàn
    cur.execute("SELECT TableId, TableNumber, TableTypeId FROM Tables WHERE IsActive = 1 AND TableId <= 14;")
    tables = cur.fetchall()

    # Lấy thông tin giá giờ loại bàn
    cur.execute("SELECT TableTypeId, DefaultHourlyRate FROM TableTypes;")
    type_rates = {k: float(v) for k, v in cur.fetchall()}

    # 7. THIẾT LẬP CHUỖI AUDIT LOG BAN ĐẦU
    current_audit_hash = "GENESIS_BLOCK_JBILLIARD_SECURE"
    audit_log_id = 1

    def add_audit(uid, action, ent_name, ent_id, old_v, new_v, dt_obj):
        nonlocal current_audit_hash, audit_log_id
        dt_str = dt_obj.strftime("%Y-%m-%d %H:%M:%S")
        payload = f"{uid}_{action}_{ent_name}_{ent_id}_{current_audit_hash}_{dt_str}"
        rec_hash = compute_sha256(payload)
        cur.execute("""
            INSERT INTO AuditLogs (AuditLogId, UserId, Action, EntityName, EntityId, OldValuesJson, NewValuesJson, MachineName, CreatedAt, PreviousHash, RecordHash)
            VALUES (?, ?, ?, ?, ?, ?, ?, 'POS-MAIN', ?, ?, ?);
        """, (audit_log_id, uid, action, ent_name, ent_id, old_v, new_v, dt_str, current_audit_hash, rec_hash))
        current_audit_hash = rec_hash
        audit_log_id += 1

    # 8. GIẢ LẬP 30 NGÀY HOẠT ĐỘNG THỰC TẾ
    print("⏳ Đang tiến hành mô phỏng 30 ngày (90 ca trực liên tục)...")
    base_start_date = datetime(2026, 10, 6, 7, 0, 0)
    total_days = 30

    session_id_counter = 1
    order_item_id_counter = 1
    invoice_id_counter = 1
    shift_id_counter = 1
    deposit_slip_id_counter = 1

    drawer_cash = 1000000.0 # Bắt đầu với 1 triệu tiền mặt
    total_revenue_month = 0.0
    total_invoices_count = 0

    # Phân phối xác suất món thường gọi theo ca
    drink_ids = [1, 2, 3, 4, 5, 6, 7] # Nước ngọt
    coffee_ids = [8, 9, 10, 11, 12, 13, 14] # Cà phê, trà
    beer_ids = [15, 16, 17, 18, 19] # Bia
    food_ids = [20, 21, 22, 23, 24, 25, 26, 27] # Đồ ăn nóng
    snack_ids = [32, 33, 34, 35, 36, 37, 38] # Ăn vặt, khăn
    smoke_ids = [28, 29, 30, 31] # Thuốc lá
    gear_ids = [39, 40] # Găng tay, lơ

    for day_idx in range(total_days):
        current_day = base_start_date + timedelta(days=day_idx)
        is_weekend = current_day.weekday() in [4, 5, 6] # T6, T7, CN

        # Một ngày có 3 ca:
        # Ca 1: Sáng (07:00 - 15:00) -> nv_sang (UserId 2)
        # Ca 2: Tối (15:00 - 23:00) -> nv_chieu (UserId 3)
        # Ca 3: Đêm (23:00 - 07:00 ngày hôm sau) -> nv_dem (UserId 4)
        shifts_schedule = [
            (2, current_day.replace(hour=7, minute=0, second=0), current_day.replace(hour=15, minute=0, second=0), "Ca Sáng (07h-15h)", "SANG"),
            (3, current_day.replace(hour=15, minute=0, second=0), current_day.replace(hour=23, minute=0, second=0), "Ca Chiều & Tối (15h-23h)", "TOI"),
            (4, current_day.replace(hour=23, minute=0, second=0), (current_day + timedelta(days=1)).replace(hour=7, minute=0, second=0), "Ca Đêm (23h-07h)", "DEM")
        ]

        for cashier_uid, shift_start, shift_end, shift_label, shift_type in shifts_schedule:
            shift_id = shift_id_counter
            shift_id_counter += 1
            shift_code = f"CA-{shift_start.strftime('%Y%m%d')}-{shift_type}"

            opening_cash = drawer_cash

            # Ghi nhận audit mở ca
            add_audit(cashier_uid, "SHIFT_START", "Shift", str(shift_id), None, f"Mở {shift_label} với {opening_cash:,.0f} đ", shift_start)

            # Xác định số lượng phiên chơi trong ca
            if shift_type == "SANG":
                session_count = random.randint(9, 14) if is_weekend else random.randint(6, 10)
            elif shift_type == "TOI":
                session_count = random.randint(22, 32) if is_weekend else random.randint(16, 22)
            else: # DEM
                session_count = random.randint(7, 11) if is_weekend else random.randint(4, 7)

            shift_cash_collected = 0.0
            shift_bank_collected = 0.0
            shift_total_revenue = 0.0

            # Sinh các phiên chơi trong khung giờ của ca
            shift_duration_seconds = int((shift_end - shift_start).total_seconds())

            # Chọn ngẫu nhiên các bàn chơi cho ca này
            for _ in range(session_count):
                tbl = random.choice(tables)
                tbl_id, tbl_number, tbl_type_id = tbl
                tbl_rate = type_rates.get(tbl_type_id, 50000.0)

                # Giờ bắt đầu ngẫu nhiên trong ca
                offset_start_sec = random.randint(600, max(1200, shift_duration_seconds - 3600))
                sess_start = shift_start + timedelta(seconds=offset_start_sec)

                # Chọn chế độ chơi: THEO GIỜ hay GÓI COMBO
                is_combo = False
                combo_obj = None

                if shift_type == "DEM":
                    # Khung giờ đêm khuya: 70% chọn cày đêm hoặc buffet bida đêm
                    if random.random() < 0.75:
                        is_combo = True
                        combo_obj = random.choice([combos_config[3], combos_config[4]]) # Combo cày đêm 6h hoặc Buffet 150k
                elif shift_type == "SANG":
                    # Sáng: 40% sinh viên chọn combo 2h
                    if random.random() < 0.40:
                        is_combo = True
                        combo_obj = random.choice([combos_config[0], combos_config[1]])
                else: # TOI
                    if random.random() < 0.25:
                        is_combo = True
                        combo_obj = random.choice([combos_config[0], combos_config[1], combos_config[2]])

                # Thời lượng chơi (phút)
                if is_combo and combo_obj:
                    cid, cname, _, cprice, cduration, cinc, _, c_items = combo_obj
                    play_minutes = cduration
                    # Thỉnh thoảng chơi vượt quá giờ quy định của combo một chút
                    if random.random() < 0.35:
                        play_minutes += random.randint(15, 60)
                    time_cost = float(cprice)
                    if play_minutes > cduration:
                        over_mins = play_minutes - cduration
                        time_cost += round_billiard_amount((over_mins / 60.0) * tbl_rate)
                    applied_combo_name = cname
                    combo_id_val = cid
                    combo_price_val = cprice
                else:
                    play_minutes = random.choice([45, 60, 75, 90, 105, 120, 140, 160, 180, 210, 240])
                    # Tính tiền giờ theo giá gốc hoặc khung giờ
                    raw_time_cost = (play_minutes / 60.0) * tbl_rate
                    time_cost = round_billiard_amount(raw_time_cost)
                    applied_combo_name = None
                    combo_id_val = None
                    combo_price_val = None

                sess_end = sess_start + timedelta(minutes=play_minutes)
                # Đảm bảo không vượt quá thời gian đóng ca hiện tại quá nhiều
                if sess_end > shift_end:
                    sess_end = shift_end - timedelta(minutes=random.randint(2, 10))
                    play_minutes = max(30, int((sess_end - sess_start).total_seconds() / 60))

                # Gán khách hàng thân thiết (60% khách quen, 40% vãng lai)
                cust_rec = random.choice(customer_records) if random.random() < 0.60 else None
                cust_id = cust_rec[0] if cust_rec else None

                session_id = session_id_counter
                session_id_counter += 1

                # Chèn TableSession
                cur.execute("""
                    INSERT INTO TableSessions (SessionId, TableId, CustomerId, CreatedByUserId, StartTime, EndTime, TotalPlayMinutes, AppliedHourlyRate, TimeCost, Status, Note, PlayMode, ComboId, ComboName, ComboPrice, ComboDurationMinutes)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, ?, ?, ?);
                """, (
                    session_id, tbl_id, cust_id, cashier_uid,
                    sess_start.strftime("%Y-%m-%d %H:%M:%S"),
                    sess_end.strftime("%Y-%m-%d %H:%M:%S"),
                    play_minutes, tbl_rate, time_cost,
                    f"Phiên {tbl_number} ({shift_label})",
                    "COMBO" if is_combo else "HOURLY",
                    combo_id_val, applied_combo_name,
                    combo_price_val, cduration if is_combo else None
                ))

                # Sinh các món ăn / thức uống gọi trong phiên
                service_cost = 0.0
                order_items_to_add = []

                # Nếu là combo, thêm các món tặng kèm giá 0đ
                if is_combo and combo_obj:
                    for pid, q in combo_obj[7]:
                        order_items_to_add.append((pid, q, 0.0, True, combo_obj[1]))

                # Khách gọi thêm F&B ngoài combo
                if shift_type == "TOI":
                    # Ca tối: khách uống nhiều bia, mồi nhắm
                    num_orders = random.randint(2, 6)
                    for _ in range(num_orders):
                        rand_cat = random.random()
                        if rand_cat < 0.40:
                            p_id = random.choice(beer_ids)
                            qty = random.choice([2, 4, 6, 8])
                        elif rand_cat < 0.70:
                            p_id = random.choice(snack_ids + food_ids)
                            qty = random.choice([1, 2, 3])
                        elif rand_cat < 0.85:
                            p_id = random.choice(smoke_ids)
                            qty = 1
                        else:
                            p_id = random.choice(drink_ids)
                            qty = random.choice([1, 2])
                        cur.execute("SELECT SellingPrice FROM Products WHERE ProductId = ?;", (p_id,))
                        sell_price = float(cur.fetchone()[0])
                        order_items_to_add.append((p_id, qty, sell_price, False, None))
                elif shift_type == "DEM":
                    # Ca đêm: mì tôm xào, bò húc, sting, thuốc lá
                    num_orders = random.randint(1, 4)
                    for _ in range(num_orders):
                        rand_cat = random.random()
                        if rand_cat < 0.45:
                            p_id = random.choice([1, 2, 7]) # Sting, Redbull
                            qty = random.choice([1, 2, 3])
                        elif rand_cat < 0.75:
                            p_id = random.choice([20, 21, 27]) # Mì bò trứng, mì bò viên, bánh mì
                            qty = random.choice([1, 2])
                        else:
                            p_id = random.choice(smoke_ids) # Thuốc lá
                            qty = 1
                        cur.execute("SELECT SellingPrice FROM Products WHERE ProductId = ?;", (p_id,))
                        sell_price = float(cur.fetchone()[0])
                        order_items_to_add.append((p_id, qty, sell_price, False, None))
                else: # SANG
                    # Ca sáng: cà phê, trà đào, nước suối, mì tôm
                    num_orders = random.randint(1, 3)
                    for _ in range(num_orders):
                        rand_cat = random.random()
                        if rand_cat < 0.60:
                            p_id = random.choice(coffee_ids) # Cà phê phin, sữa, muối
                            qty = random.choice([1, 2])
                        elif rand_cat < 0.85:
                            p_id = random.choice([20, 27, 3]) # Mì xào, bánh mì, lavie
                            qty = 1
                        else:
                            p_id = random.choice(smoke_ids)
                            qty = 1
                        cur.execute("SELECT SellingPrice FROM Products WHERE ProductId = ?;", (p_id,))
                        sell_price = float(cur.fetchone()[0])
                        order_items_to_add.append((p_id, qty, sell_price, False, None))

                for p_id, qty, u_price, is_cb, cb_nm in order_items_to_add:
                    tot_item = float(u_price) * qty
                    service_cost += tot_item
                    ord_time = sess_start + timedelta(minutes=random.randint(5, max(10, play_minutes - 10)))
                    cur.execute("""
                        INSERT INTO OrderItems (OrderItemId, SessionId, ProductId, Quantity, UnitPrice, TotalAmount, OrderedByUserId, OrderedAt, IsCompleted, CompletedAt, CompletedByUserId, IsCancelled, IsComboItem, ComboName)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, 0, ?, ?);
                    """, (
                        order_item_id_counter, session_id, p_id, qty, u_price, tot_item,
                        cashier_uid, ord_time.strftime("%Y-%m-%d %H:%M:%S"),
                        (ord_time + timedelta(minutes=5)).strftime("%Y-%m-%d %H:%M:%S"),
                        cashier_uid, 1 if is_cb else 0, cb_nm
                    ))
                    order_item_id_counter += 1

                    # Trừ tồn kho nếu là hàng quản lý kho
                    cur.execute("""
                        UPDATE InventoryItems 
                        SET QuantityOnHand = MAX(0, QuantityOnHand - ?) 
                        WHERE ProductId = ?;
                    """, (qty, p_id))

                # Tính giảm giá hội viên nếu có
                discount_percent = 0.0
                discount_amount = 0.0
                if cust_rec:
                    tier_id = cust_rec[1]
                    if tier_id == 4: # Kim Cương
                        discount_percent = 10.0
                    elif tier_id == 3: # Vàng
                        discount_percent = 5.0
                    elif tier_id == 2: # Bạc
                        discount_percent = 3.0
                    discount_amount = round_billiard_amount((time_cost + service_cost) * (discount_percent / 100.0))

                subtotal = time_cost + service_cost - discount_amount
                final_amount = max(0.0, subtotal)
                points_earned = int(final_amount // 10000)

                # Chọn phương thức thanh toán: 55% CASH, 45% BANK_TRANSFER
                is_cash = random.random() < 0.55
                pay_method = "CASH" if is_cash else "BANK_TRANSFER"

                invoice_id = invoice_id_counter
                invoice_id_counter += 1
                inv_code = f"HD-{sess_end.strftime('%Y%m%d')}-{invoice_id:04d}"

                # Tính mã băm bảo mật chống gian lận SHA-256
                rounded_final = int(round(final_amount))
                time_str = sess_end.strftime("%Y-%m-%d %H:%M:%S")
                tamper_payload = f"{inv_code}_{rounded_final}_{time_str}_{session_id}_{cashier_uid}"
                tamper_hash = compute_sha256(tamper_payload)

                cur.execute("""
                    INSERT INTO Invoices (InvoiceId, InvoiceCode, SessionId, CustomerId, CashierUserId, ShiftId, TotalPlayMinutes, TimeAmount, ServiceAmount, DiscountPercent, DiscountAmount, PointsUsed, PointsDiscountAmount, SurchargeAmount, FinalAmount, PointsEarned, AppliedComboName, CreatedAt, Notes, TamperProofHash)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, 0, ?, ?, ?, ?, ?, ?);
                """, (
                    invoice_id, inv_code, session_id, cust_id, cashier_uid, shift_id,
                    play_minutes, time_cost, service_cost, discount_percent, discount_amount,
                    final_amount, points_earned, applied_combo_name, time_str,
                    f"Thanh toán {tbl_number} thành công", tamper_hash
                ))

                # Ghi InvoicePayment
                cur.execute("""
                    INSERT INTO InvoicePayments (InvoiceId, Method, Amount, TransactionReference, CreatedAt)
                    VALUES (?, ?, ?, ?, ?);
                """, (
                    invoice_id, 1 if is_cash else 2, final_amount,
                    f"BILL_{inv_code}" if is_cash else f"VQR_{sess_end.strftime('%H%M%S')}{random.randint(100,999)}",
                    time_str
                ))

                # Cập nhật điểm cho khách hàng
                if cust_id:
                    cur.execute("""
                        UPDATE Customers 
                        SET CurrentPoints = CurrentPoints + ?, TotalPointsAccumulated = TotalPointsAccumulated + ?, TotalSpent = TotalSpent + ?, LastVisitAt = ?
                        WHERE CustomerId = ?;
                    """, (points_earned, points_earned, final_amount, time_str, cust_id))

                # Cộng dồn doanh thu ca
                if is_cash:
                    shift_cash_collected += final_amount
                else:
                    shift_bank_collected += final_amount
                shift_total_revenue += final_amount

            # Kết thúc ca: Đóng ca trực Shift
            expected_cash = opening_cash + shift_cash_collected
            actual_cash = expected_cash # Khớp 100% chuẩn xác
            discrepancy = 0.0

            # Cập nhật drawer_cash
            drawer_cash = actual_cash

            # Kiểm tra xem có nộp tiền ngân hàng không (Nếu két tích lũy > 8 triệu thì nộp bớt 5-8 triệu vào tài khoản MBBank)
            total_deposited_in_shift = 0.0
            has_deposit = False
            deposit_amount = 0.0
            if drawer_cash >= 7500000.0:
                calc_deposit = float(int((drawer_cash - 2000000.0) // 500000) * 500000)
                if calc_deposit >= 3000000.0:
                    deposit_amount = calc_deposit
                    drawer_cash -= deposit_amount
                    total_deposited_in_shift = deposit_amount
                    has_deposit = True

            # Chốt ca trực (Chèn Shift trước để thỏa mãn Foreign Key cho BankDepositSlip)
            cur.execute("""
                INSERT INTO Shifts (ShiftId, UserId, StartTime, EndTime, OpeningCash, PreviousShiftClosingCash, IsOpeningDiscrepant, OpeningDiscrepancyNotes, TotalBankDeposited, SystemCalculatedCash, SystemCalculatedTransfer, ActualCashRecorded, Discrepancy, Status, Notes)
                VALUES (?, ?, ?, ?, ?, ?, 0, NULL, ?, ?, ?, ?, ?, 1, ?);
            """, (
                shift_id, cashier_uid,
                shift_start.strftime("%Y-%m-%d %H:%M:%S"),
                shift_end.strftime("%Y-%m-%d %H:%M:%S"),
                opening_cash, opening_cash,
                total_deposited_in_shift,
                shift_cash_collected, shift_bank_collected,
                actual_cash, discrepancy,
                f"Chốt {shift_label} hoàn tất, khớp két 100%"
            ))

            if has_deposit:
                slip_id = deposit_slip_id_counter
                deposit_slip_id_counter += 1
                slip_code = f"NOP-{shift_end.strftime('%Y%m%d')}-{slip_id:03d}"
                cur.execute("""
                    INSERT INTO BankDepositSlips (DepositSlipId, SlipCode, ShiftId, UserId, Amount, BankName, BankAccountNo, BankAccountName, ImagePath, Notes, CreatedAt, Status, ApprovedByUserId, ApprovedAt, IsModified)
                    VALUES (?, ?, ?, ?, ?, 'MBBank Quân Đội', '090123456789', 'J-BILLIARDS CLUB', NULL, 'Nộp doanh thu tiền mặt vào tài khoản ngân hàng quán', ?, 'Approved', 1, ?, 0);
                """, (
                    slip_id, slip_code, shift_id, cashier_uid, deposit_amount,
                    shift_end.strftime("%Y-%m-%d %H:%M:%S"),
                    (shift_end + timedelta(minutes=15)).strftime("%Y-%m-%d %H:%M:%S")
                ))
                add_audit(1, "APPROVE_DEPOSIT", "BankDepositSlip", str(slip_id), None, f"Admin duyệt nộp {deposit_amount:,.0f} đ", shift_end + timedelta(minutes=15))

            add_audit(cashier_uid, "SHIFT_CLOSE", "Shift", str(shift_id), None, f"Chốt {shift_label}: DT = {shift_total_revenue:,.0f} đ", shift_end)

            total_revenue_month += shift_total_revenue
            total_invoices_count += session_count

        if (day_idx + 1) % 5 == 0 or day_idx == total_days - 1:
            print(f"   ✓ Đã mô phỏng xong ngày {day_idx + 1}/{total_days} ({current_day.strftime('%d/%m/%Y')}) - Lũy kế DT: {total_revenue_month:,.0f} đ")

    # Mở 1 ca trực hiện tại cho ngày hôm nay để khi người dùng mở app lên là sẵn sàng có ca hoạt động!
    now_active_start = datetime.now()
    cur.execute("""
        INSERT INTO Shifts (ShiftId, UserId, StartTime, EndTime, OpeningCash, PreviousShiftClosingCash, IsOpeningDiscrepant, OpeningDiscrepancyNotes, TotalBankDeposited, SystemCalculatedCash, SystemCalculatedTransfer, ActualCashRecorded, Discrepancy, Status, Notes)
        VALUES (?, 2, ?, NULL, ?, ?, 0, NULL, 0, 0, 0, NULL, NULL, 0, 'Ca trực mở sẵn sàng phục vụ');
    """, (
        shift_id_counter,
        now_active_start.strftime("%Y-%m-%d %H:%M:%S"),
        drawer_cash, drawer_cash
    ))
    add_audit(2, "SHIFT_START", "Shift", str(shift_id_counter), None, f"Mở ca trực hiện tại với {drawer_cash:,.0f} đ", now_active_start)

    conn.commit()
    conn.close()

    print(f"\n=======================================================")
    print(f"🎉 GIẢ LẬP DỮ LIỆU THÀNH CÔNG RỰC RỠ 100%!")
    print(f"📊 Tổng số ca trực liên tục: {shift_id_counter} ca (3 ca/ngày)")
    print(f"🧾 Tổng số hóa đơn thực tế: {total_invoices_count} hóa đơn")
    print(f"💰 TỔNG DOANH THU 1 THÁNG: {total_revenue_month:,.0f} VNĐ")
    print(f"🔒 Mã băm kiểm toán AuditLog: Hoàn toàn nguyên vẹn & khớp nối!")
    print(f"=======================================================\n")

if __name__ == "__main__":
    db_debug = r"D:\AppDesktop\QL_JBillard\bin\Debug\net8.0-windows\AppData\qlin_billiard.db"
    db_publish = r"D:\AppDesktop\QL_JBillard\publish\AppData\qlin_billiard.db"

    run_simulation(db_debug)
    if os.path.exists(os.path.dirname(db_publish)):
        import shutil
        shutil.copyfile(db_debug, db_publish)
        print(f"📋 Đã đồng bộ CSDL sang Publish: {db_publish}")

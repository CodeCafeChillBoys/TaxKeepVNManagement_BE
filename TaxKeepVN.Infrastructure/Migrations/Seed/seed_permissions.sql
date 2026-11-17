-- =============================================================================
-- SEED: Danh mục Quyền (Permissions) và Phân quyền mặc định theo Vai trò (RolePermissions)
-- Phục vụ tính năng quản trị phân quyền (RBAC) cho Quản trị viên (Admin)
-- =============================================================================

-- 1. Bảng permissions: Chèn danh mục 65 quyền chức năng của hệ thống
INSERT INTO permissions (id, code, name, group_name) VALUES
-- ── Nhóm: Quyết toán thuế TNCN ────────────────────────────────────────────────
(1, 'settlement.view', 'Xem hồ sơ và kết quả quyết toán thuế', 'Quyết toán thuế TNCN'),
(2, 'settlement.calculate', 'Tính toán và dự thảo quyết toán thuế TNCN', 'Quyết toán thuế TNCN'),
(3, 'settlement.confirm', 'Xác nhận và lưu hồ sơ quyết toán chính thức', 'Quyết toán thuế TNCN'),
(4, 'settlement.export_pdf', 'Kết xuất tờ khai quyết toán mẫu 02/QTT-TNCN ra PDF', 'Quyết toán thuế TNCN'),
(5, 'settlement.export_package', 'Đóng gói hồ sơ quyết toán và chứng từ gốc (ZIP)', 'Quyết toán thuế TNCN'),
(6, 'settlement.policy_manage', 'Cập nhật bậc thuế lũy tiến và chính sách giảm trừ', 'Quyết toán thuế TNCN'),
(7, 'settlement.schedule_manage', 'Quản lý hạn chót và lịch nhắc nhở quyết toán', 'Quyết toán thuế TNCN'),

-- ── Nhóm: Kỳ tính thuế & Chứng từ ───────────────────────────────────────────
(8, 'tax_period.view', 'Xem danh sách kỳ tính thuế và tài liệu đính kèm', 'Kỳ tính thuế & Chứng từ'),
(9, 'tax_period.manage', 'Khởi tạo và cập nhật kỳ tính thuế', 'Kỳ tính thuế & Chứng từ'),
(10, 'tax_period.submit', 'Nộp hồ sơ quyết toán kỳ tính thuế', 'Kỳ tính thuế & Chứng từ'),
(11, 'document.upload', 'Tải lên chứng từ khấu trừ thuế (ảnh/PDF)', 'Kỳ tính thuế & Chứng từ'),
(12, 'document.view', 'Xem danh sách và chi tiết chứng từ thuế', 'Kỳ tính thuế & Chứng từ'),
(13, 'document.delete', 'Xóa chứng từ thuế trong kỳ', 'Kỳ tính thuế & Chứng từ'),
(14, 'document.review', 'Kiểm tra và xác nhận kết quả đọc chứng từ thuế', 'Kỳ tính thuế & Chứng từ'),
(15, 'document.ocr', 'Trích xuất thông tin chứng từ thuế bằng AI OCR', 'Kỳ tính thuế & Chứng từ'),

-- ── Nhóm: Người phụ thuộc ───────────────────────────────────────────────────
(16, 'dependent.view', 'Xem danh sách và chi tiết người phụ thuộc', 'Người phụ thuộc'),
(17, 'dependent.manage', 'Thêm mới, cập nhật và xóa người phụ thuộc', 'Người phụ thuộc'),
(18, 'dependent.document_manage', 'Tải lên và quản lý hồ sơ chứng minh người phụ thuộc', 'Người phụ thuộc'),
(19, 'dependent.reminder_view', 'Xem cảnh báo và nhắc nhở mốc tuổi người phụ thuộc', 'Người phụ thuộc'),
(20, 'dependent.rule_manage', 'Quản lý danh mục quy định giấy tờ người phụ thuộc', 'Người phụ thuộc'),

-- ── Nhóm: Quản lý thu nhập ──────────────────────────────────────────────────
(21, 'income.view', 'Xem danh sách và chi tiết các khoản thu nhập', 'Quản lý thu nhập'),
(22, 'income.manage', 'Thêm mới, cập nhật, xóa khoản thu nhập cá nhân', 'Quản lý thu nhập'),
(23, 'income_source.manage', 'Quản lý danh mục nguồn thu nhập', 'Quản lý thu nhập'),

-- ── Nhóm: Tư vấn & Lịch hẹn ─────────────────────────────────────────────────
(24, 'booking.create', 'Tìm kiếm chuyên gia và đặt lịch hẹn tư vấn thuế', 'Tư vấn & Lịch hẹn'),
(25, 'booking.view', 'Xem danh sách và chi tiết lịch hẹn tư vấn', 'Tư vấn & Lịch hẹn'),
(26, 'booking.cancel', 'Hủy lịch hẹn tư vấn theo chính sách hoàn tiền', 'Tư vấn & Lịch hẹn'),
(27, 'consultation.join', 'Tham gia phòng họp tư vấn trực tuyến', 'Tư vấn & Lịch hẹn'),
(28, 'consultation.complete', 'Xác nhận hoàn thành buổi tư vấn thuế', 'Tư vấn & Lịch hẹn'),
(29, 'review.create', 'Đánh giá và nhận xét chất lượng buổi tư vấn', 'Tư vấn & Lịch hẹn'),
(30, 'review.view', 'Xem danh sách đánh giá công khai của chuyên gia', 'Tư vấn & Lịch hẹn'),

-- ── Nhóm: Nghiệp vụ Chuyên gia ──────────────────────────────────────────────
(31, 'expert.apply', 'Nộp hồ sơ đăng ký trở thành chuyên gia tư vấn', 'Nghiệp vụ Chuyên gia'),
(32, 'expert.slot_manage', 'Quản lý khung giờ nhận lịch tư vấn (Slots)', 'Nghiệp vụ Chuyên gia'),
(33, 'expert.booking_manage', 'Tiếp nhận, chấp nhận hoặc từ chối lịch hẹn từ khách hàng', 'Nghiệp vụ Chuyên gia'),
(34, 'expert.review_reply', 'Phản hồi đánh giá của khách hàng', 'Nghiệp vụ Chuyên gia'),
(35, 'expert.wallet_view', 'Xem ví thu nhập chuyên gia và quỹ Escrow chờ giải ngân', 'Nghiệp vụ Chuyên gia'),
(36, 'expert.withdraw', 'Yêu cầu rút tiền từ ví thu nhập về tài khoản ngân hàng', 'Nghiệp vụ Chuyên gia'),

-- ── Nhóm: Khiếu nại & Tranh chấp ───────────────────────────────────────────
(37, 'dispute.create', 'Gửi khiếu nại buổi tư vấn và yêu cầu bồi hoàn', 'Khiếu nại & Tranh chấp'),
(38, 'dispute.view', 'Xem chi tiết khiếu nại và tiến trình xử lý', 'Khiếu nại & Tranh chấp'),
(39, 'dispute.expert_respond', 'Chuyên gia gửi giải trình về khiếu nại', 'Khiếu nại & Tranh chấp'),
(40, 'dispute.admin_resolve', 'Quản trị viên phân xử khiếu nại và ra quyết định hoàn tiền', 'Khiếu nại & Tranh chấp'),

-- ── Nhóm: Ví điện tử & Thanh toán ───────────────────────────────────────────
(41, 'wallet.view', 'Xem số dư ví và lịch sử biến động số dư', 'Ví điện tử & Thanh toán'),
(42, 'wallet.topup', 'Nạp tiền vào ví qua cổng thanh toán VNPay / VietQR', 'Ví điện tử & Thanh toán'),
(43, 'wallet.user_withdraw', 'Yêu cầu rút tiền hoàn từ ví cá nhân về tài khoản ngân hàng', 'Ví điện tử & Thanh toán'),
(44, 'payment.pay', 'Thanh toán lịch hẹn tư vấn qua cổng thanh toán hoặc ví', 'Ví điện tử & Thanh toán'),
(45, 'admin.withdrawal_manage', 'Quản trị viên duyệt và xử lý lệnh rút tiền', 'Ví điện tử & Thanh toán'),

-- ── Nhóm: Gói dịch vụ ───────────────────────────────────────────────────────
(46, 'subscription.view', 'Xem thông tin các gói dịch vụ và gói đang sử dụng', 'Gói dịch vụ'),
(47, 'subscription.purchase', 'Đăng ký mua hoặc nâng cấp gói dịch vụ qua VNPay', 'Gói dịch vụ'),
(48, 'admin.subscription_plan.manage', 'Quản trị viên tạo, sửa, xóa, ẩn/hiện các gói dịch vụ', 'Gói dịch vụ'),
(49, 'admin.user_subscription.manage', 'Quản trị viên quản lý thuê bao người dùng', 'Gói dịch vụ'),

-- ── Nhóm: Trợ lý AI & Tri thức thuế ────────────────────────────────────────
(50, 'tax_chat.use', 'Đặt câu hỏi và nhận tư vấn tự động từ Trợ lý AI', 'Trợ lý AI & Tri thức thuế'),
(51, 'admin.ai_key.manage', 'Quản lý danh sách API Key Google Gemini', 'Trợ lý AI & Tri thức thuế'),
(52, 'admin.prompt_template.manage', 'Quản lý các mẫu System Prompt cho Trợ lý AI', 'Trợ lý AI & Tri thức thuế'),
(53, 'admin.tax_rule.manage', 'Tải lên tài liệu luật thuế và đồng bộ vào Vector DB cho AI', 'Trợ lý AI & Tri thức thuế'),

-- ── Nhóm: Quản trị Chuyên gia ───────────────────────────────────────────────
(54, 'admin.expert_app.view', 'Xem danh sách hồ sơ ứng viên đăng ký chuyên gia tư vấn', 'Quản trị Chuyên gia'),
(55, 'admin.expert_app.audit', 'Thẩm định chứng chỉ, phê duyệt / từ chối hồ sơ chuyên gia', 'Quản trị Chuyên gia'),
(56, 'admin.specialization.manage', 'Quản lý danh mục lĩnh vực chuyên môn thuế', 'Quản trị Chuyên gia'),
(57, 'admin.fee_config.manage', 'Cấu hình bảng phí tư vấn chuẩn và khung thời lượng', 'Quản trị Chuyên gia'),
(58, 'admin.certificate_config.manage', 'Cấu hình danh mục loại chứng chỉ chuyên gia được công nhận', 'Quản trị Chuyên gia'),
(59, 'admin.search_config.manage', 'Cấu hình thuật toán tìm kiếm và trọng số xếp hạng chuyên gia', 'Quản trị Chuyên gia'),
(60, 'admin.platform_policy.manage', 'Cấu hình chính sách nền tảng (hoa hồng, thời hạn khiếu nại)', 'Quản trị Chuyên gia'),

-- ── Nhóm: Hệ thống & Cấu hình ───────────────────────────────────────────────
(61, 'admin.role_permission.manage', 'Quản lý danh sách vai trò và cấu hình phân quyền tính năng', 'Hệ thống & Cấu hình'),
(62, 'admin.system_config.manage', 'Quản lý các biến cấu hình và tham số động toàn hệ thống', 'Hệ thống & Cấu hình'),
(63, 'admin.url_rule.manage', 'Quản lý danh sách quy tắc kiểm tra URL an toàn', 'Hệ thống & Cấu hình'),
(64, 'admin.review.moderate', 'Kiểm duyệt và ẩn/hiện các đánh giá vi phạm tiêu chuẩn', 'Hệ thống & Cấu hình'),
(65, 'admin.notification.manage', 'Quản lý và gửi thông báo hệ thống', 'Hệ thống & Cấu hình')
ON CONFLICT (id) DO UPDATE 
SET code = EXCLUDED.code,
    name = EXCLUDED.name,
    group_name = EXCLUDED.group_name;

-- Cập nhật sequence của bảng permissions để các lượt tạo sau không bị trùng ID
SELECT setval(pg_get_serial_sequence('permissions', 'id'), COALESCE(MAX(id), 1)) FROM permissions;

-- 2. Bảng role_permissions: Phân quyền mặc định cho từng vai trò
-- Role 1: taxpayer (Người nộp thuế)
-- Role 2: admin (Quản trị viên hệ thống)
-- Role 3: expert (Chuyên gia tư vấn thuế)

-- Role 1: taxpayer
INSERT INTO role_permissions (role_id, permission_id) VALUES
(1, 1),  -- settlement.view
(1, 2),  -- settlement.calculate
(1, 3),  -- settlement.confirm
(1, 4),  -- settlement.export_pdf
(1, 5),  -- settlement.export_package
(1, 8),  -- tax_period.view
(1, 9),  -- tax_period.manage
(1, 10), -- tax_period.submit
(1, 11), -- document.upload
(1, 12), -- document.view
(1, 13), -- document.delete
(1, 14), -- document.review
(1, 15), -- document.ocr
(1, 16), -- dependent.view
(1, 17), -- dependent.manage
(1, 18), -- dependent.document_manage
(1, 19), -- dependent.reminder_view
(1, 21), -- income.view
(1, 22), -- income.manage
(1, 23), -- income_source.manage
(1, 24), -- booking.create
(1, 25), -- booking.view
(1, 26), -- booking.cancel
(1, 27), -- consultation.join
(1, 28), -- consultation.complete
(1, 29), -- review.create
(1, 30), -- review.view
(1, 31), -- expert.apply
(1, 37), -- dispute.create
(1, 38), -- dispute.view
(1, 41), -- wallet.view
(1, 42), -- wallet.topup
(1, 43), -- wallet.user_withdraw
(1, 44), -- payment.pay
(1, 46), -- subscription.view
(1, 47), -- subscription.purchase
(1, 50)  -- tax_chat.use
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Role 3: expert (Chuyên gia tư vấn thuế)
INSERT INTO role_permissions (role_id, permission_id) VALUES
(3, 1),  -- settlement.view
(3, 2),  -- settlement.calculate
(3, 3),  -- settlement.confirm
(3, 4),  -- settlement.export_pdf
(3, 5),  -- settlement.export_package
(3, 8),  -- tax_period.view
(3, 12), -- document.view
(3, 14), -- document.review
(3, 15), -- document.ocr
(3, 16), -- dependent.view
(3, 21), -- income.view
(3, 25), -- booking.view
(3, 26), -- booking.cancel
(3, 27), -- consultation.join
(3, 28), -- consultation.complete
(3, 30), -- review.view
(3, 32), -- expert.slot_manage
(3, 33), -- expert.booking_manage
(3, 34), -- expert.review_reply
(3, 35), -- expert.wallet_view
(3, 36), -- expert.withdraw
(3, 38), -- dispute.view
(3, 39), -- dispute.expert_respond
(3, 41), -- wallet.view
(3, 42), -- wallet.topup
(3, 46), -- subscription.view
(3, 50)  -- tax_chat.use
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- Role 2: admin (Toàn quyền hệ thống - Toàn bộ 65 permissions)
INSERT INTO role_permissions (role_id, permission_id)
SELECT 2, p.id FROM permissions p
ON CONFLICT (role_id, permission_id) DO NOTHING;

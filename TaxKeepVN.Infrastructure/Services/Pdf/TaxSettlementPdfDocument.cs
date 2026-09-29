using System;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxKeepVN.Application.DTOs.TaxSettlement;

namespace TaxKeepVN.Infrastructure.Services.Pdf
{
    public class TaxSettlementPdfDocument : IDocument
    {
        private static readonly CultureInfo VnCulture = new("vi-VN");
        public TaxSettlementPdfModel Model { get; }

        public TaxSettlementPdfDocument(TaxSettlementPdfModel model)
        {
            Model = model;
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"Tờ khai Quyết toán Thuế TNCN {Model.TaxYear} - {Model.FullName}",
            Author = "TaxKeepVN System",
            Subject = "Hồ sơ quyết toán thuế TNCN Mẫu 02/QTT-TNCN",
            Keywords = "Quyết toán thuế, PIT, Mẫu 02/QTT-TNCN, QuestPDF"
        };

        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(25);
                page.MarginBottom(25);
                page.MarginLeft(30);
                page.MarginRight(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(Colors.Grey.Darken4));

                page.Header().Element(ComposeGlobalHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeGlobalFooter);
            });
        }

        private void ComposeGlobalHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("TỔNG CỤC THUẾ").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"HỆ THỐNG TAXKEEPVN — MÃ HỒ SƠ: {Model.DossierId.ToString()[..8].ToUpper()}").FontSize(7).FontColor(Colors.Grey.Medium);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("Mẫu số: 02/QTT-TNCN").Bold().FontSize(8);
                    col.Item().Text("(Ban hành kèm Thông tư 80/2021/TT-BTC & Luật 109/2025/QH15)").Italic().FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void ComposeGlobalFooter(IContainer container)
        {
            container.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Trích xuất từ CSDL TaxKeepVN — Ngày lập: ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    x.Span(DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm")).Bold().FontSize(7);
                });

                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Trang ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" / ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingTop(10).Column(col =>
            {
                // ── 1. TIÊU NGỮ VÀ TIÊU ĐỀ TỜ KHAI ──
                col.Item().AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(10.5f);
                    titleCol.Item().Text("Độc lập - Tự do - Hạnh phúc").Bold().FontSize(9.5f);
                    titleCol.Item().PaddingTop(2).Text("──────── * ────────").FontColor(Colors.Grey.Darken1);

                    titleCol.Item().PaddingTop(8).Text("TỜ KHAI QUYẾT TOÁN THUẾ THU NHẬP CÁ NHÂN").Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    titleCol.Item().Text($"(Áp dụng cho cá nhân cư trú có thu nhập từ tiền lương, tiền công — Kỳ tính thuế năm {Model.TaxYear})").Italic().FontSize(9);
                    titleCol.Item().Text($"Chế độ pháp lý: {Model.LawGroupLabel}").FontSize(8).FontColor(Colors.Green.Darken2);
                });

                col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // ── 2. THÔNG TIN NGƯỜI NỘP THUẾ ──
                col.Item().Text("[I] THÔNG TIN NGƯỜI NỘP THUẾ").Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                col.Item().PaddingTop(3).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(4);
                    });

                    table.Cell().Text("Họ và tên:").Bold();
                    table.Cell().Text(Model.FullName);
                    table.Cell().Text("Mã số thuế:").Bold();
                    table.Cell().Text(string.IsNullOrWhiteSpace(Model.TaxCode) ? "Chưa đăng ký" : Model.TaxCode);

                    table.Cell().Text("Số CCCD / Định danh:").Bold();
                    table.Cell().Text(Model.CitizenId);
                    table.Cell().Text("Số điện thoại:").Bold();
                    table.Cell().Text(Model.PhoneNumber);

                    table.Cell().Text("Email liên hệ:").Bold();
                    table.Cell().Text(Model.Email);
                    table.Cell().Text("Cơ quan thuế quyết toán:").Bold();
                    table.Cell().Text(string.IsNullOrWhiteSpace(Model.TaxOfficeName) ? "Theo địa bàn cư trú" : Model.TaxOfficeName);

                    table.Cell().Text("Tài khoản nhận hoàn thuế:").Bold();
                    table.Cell().Column(bankCol =>
                    {
                        if (!string.IsNullOrWhiteSpace(Model.BankAccountNumber))
                        {
                            bankCol.Item().Text($"{Model.BankAccountNumber} ({Model.BankName})");
                        }
                        else
                        {
                            bankCol.Item().Text("Chưa cung cấp").Italic().FontColor(Colors.Grey.Darken1);
                        }
                    });
                    table.Cell().Text("Ngày chốt số liệu:").Bold();
                    table.Cell().Text(Model.CutoffDate.ToString("dd/MM/yyyy"));
                });

                col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // ── 3. BẢNG CÁC CHỈ TIÊU NGHĨA VỤ THUẾ ──
                col.Item().Text("[II] CÁC CHỈ TIÊU TÍNH THUẾ").Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(35);
                        columns.RelativeColumn(9);
                        columns.ConstantColumn(40);
                        columns.RelativeColumn(4);
                    });

                    // Header
                    table.Cell().RowSpan(1).Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("STT").Bold();
                    table.Cell().RowSpan(1).Background(Colors.Grey.Lighten3).Padding(3).Text("Chỉ tiêu nghĩa vụ thuế").Bold();
                    table.Cell().RowSpan(1).Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("Mã số").Bold();
                    table.Cell().RowSpan(1).Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Số tiền (VNĐ)").Bold();

                    // Rows
                    AddTaxRow(table, "1", "Tổng thu nhập chịu thuế (TNCT) phát sinh trong kỳ", "[20]", Model.TotalGrossIncome, true);
                    AddTaxRow(table, "2", "Tổng các khoản giảm trừ nghĩa vụ thuế", "[23]", Model.TotalDeductions, true);
                    AddTaxRow(table, "2.1", $"- Giảm trừ cho bản thân ({Model.PersonalDeductionMonths} tháng)", "[24]", Model.PersonalDeductionAmount);
                    AddTaxRow(table, "2.2", $"- Giảm trừ người phụ thuộc ({Model.DependentDeductionPersonMonths} người-tháng)", "[25]", Model.DependentDeductionAmount);
                    AddTaxRow(table, "2.3", "- Các khoản đóng bảo hiểm bắt buộc (BHXH, BHYT, BHTN)", "[26]", Model.TotalInsuranceDeduction);
                    AddTaxRow(table, "2.4", "- Chi phí y tế hợp lệ (Áp trần tối đa 23.000.000đ từ 2026)", "[27]", Model.MedicalDeduction);
                    AddTaxRow(table, "2.5", "- Chi phí giáo dục hợp lệ (Áp trần tối đa 24.000.000đ từ 2026)", "[28]", Model.EducationDeduction);
                    AddTaxRow(table, "2.6", "- Các khoản đóng góp từ thiện, nhân đạo, khuyến học", "[29]", Model.CharityDeduction);
                    AddTaxRow(table, "3", "Tổng thu nhập tính thuế năm ([20] - [23])", "[30]", Model.TaxableIncomeYearly, true);
                    AddTaxRow(table, "4", "Thu nhập tính thuế bình quân tháng áp biểu lũy tiến", "[31]", Model.TaxableIncomeMonthly);
                    AddTaxRow(table, "5", $"Tổng số thuế TNCN phát sinh trong kỳ (Bậc {Model.AppliedBracketNo})", "[32]", Model.TaxPayable, true);
                    AddTaxRow(table, "6", "Tổng số thuế TNCN đã được tổ chức chi trả khấu trừ", "[33]", Model.TotalTaxWithheld, true);
                    AddTaxRow(table, "7", "Số thuế nộp thừa — ĐỀ NGHỊ HOÀN THUẾ", "[34]", Model.RefundAmount, true, Colors.Green.Darken2);
                    AddTaxRow(table, "8", "Số thuế nộp thiếu — PHẢI NỘP THÊM VÀO NGÂN SÁCH", "[35]", Model.DueAmount, true, Colors.Red.Darken2);
                });

                // ── 4. BẢNG PHÂN RÃ BẬC THUẾ ÁP DỤNG ──
                if (Model.BracketDetails.Any())
                {
                    col.Item().PaddingTop(8).Text("[III] BẢNG PHÂN BỔ THUẾ LŨY TIẾN ĐỘNG (THEO CẤU HÌNH HỆ THỐNG)").Bold().FontSize(8.5f).FontColor(Colors.Grey.Darken3);
                    col.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(40);
                        });

                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text("Bậc").Bold().FontSize(7.5f);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).Text("Khung thu nhập tháng").Bold().FontSize(7.5f);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text("Thuế suất").Bold().FontSize(7.5f);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).AlignRight().Text("Khấu trừ nhanh").Bold().FontSize(7.5f);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).AlignRight().Text("Thuế tháng").Bold().FontSize(7.5f);
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text("Áp dụng").Bold().FontSize(7.5f);

                        foreach (var b in Model.BracketDetails)
                        {
                            var bg = b.IsApplied ? Colors.Yellow.Lighten4 : Colors.White;
                            var range = b.ToMonthly.HasValue
                                ? $"{FormatVn(b.FromMonthly)} - {FormatVn(b.ToMonthly.Value)}"
                                : $"> {FormatVn(b.FromMonthly)}";

                            table.Cell().Background(bg).Padding(2).AlignCenter().Text(b.BracketNo.ToString()).FontSize(7.5f);
                            table.Cell().Background(bg).Padding(2).Text(range).FontSize(7.5f);
                            table.Cell().Background(bg).Padding(2).AlignCenter().Text($"{b.Rate * 100:0.#}%").FontSize(7.5f);
                            table.Cell().Background(bg).Padding(2).AlignRight().Text(FormatVn(b.QuickDeduct)).FontSize(7.5f);
                            table.Cell().Background(bg).Padding(2).AlignRight().Text(FormatVn(b.TaxMonthly)).FontSize(7.5f);
                            table.Cell().Background(bg).Padding(2).AlignCenter().Text(b.IsApplied ? "[X]" : "[ ]").Bold().FontSize(7.5f);
                        }
                    });
                }

                // ── 5. PHẦN CAM ĐOAN VÀ KÝ TÊN ──
                col.Item().PaddingTop(12).Row(signRow =>
                {
                    signRow.RelativeItem().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text("NHÂN VIÊN ĐỐI SOÁT").Italic().FontSize(8);
                        c.Item().Text("(Ký, ghi rõ họ tên)").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(35).Text("Hệ thống TaxKeepVN tự động").FontSize(7.5f).Italic();
                    });

                    signRow.RelativeItem().AlignRight().PaddingRight(10).Column(c =>
                    {
                        c.Item().Text($"Ngày {DateTime.Now.Day} tháng {DateTime.Now.Month} năm {DateTime.Now.Year}").Italic().FontSize(8);
                        c.Item().Text("NGƯỜI NỘP THUẾ HOẶC ĐẠI DIỆN HỢP PHÁP").Bold().FontSize(8);
                        c.Item().Text("(Ký, ghi rõ họ tên)").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(35).Text(Model.FullName).Bold().FontSize(8.5f);
                    });
                });

                // ═════════════════════════════════════════════════════════════════════
                // ── TRANG PHỤ LỤC 1: BẢNG KÊ NGƯỜI PHỤ THUỘC (02-1/BK-QTT) ──
                // ═════════════════════════════════════════════════════════════════════
                col.Item().PageBreak();

                col.Item().AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().Text("PHỤ LỤC: BẢNG KÊ NGƯỜI PHỤ THUỘC GIẢM TRỪ GIA CẢNH").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    titleCol.Item().Text("(Mẫu số: 02-1/BK-QTT kèm theo Tờ khai Quyết toán thuế TNCN số 02/QTT-TNCN)").Italic().FontSize(8.5f);
                    titleCol.Item().Text($"Kỳ tính thuế: Năm {Model.TaxYear} — Người nộp thuế: {Model.FullName}").FontSize(8.5f);
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(25);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(3.5f);
                    });

                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("STT").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Họ và tên NPT").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("Ngày sinh").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Số CCCD / Định danh").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Quan hệ với NNT").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("Số tháng").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Tổng giảm trừ").Bold();

                    if (Model.Dependents.Any())
                    {
                        foreach (var dep in Model.Dependents)
                        {
                            table.Cell().Padding(3).AlignCenter().Text(dep.Index.ToString());
                            table.Cell().Padding(3).Text(dep.FullName).Bold();
                            table.Cell().Padding(3).AlignCenter().Text(dep.DateOfBirth);
                            table.Cell().Padding(3).Text(dep.IdNumber);
                            table.Cell().Padding(3).Text(dep.Relationship);
                            table.Cell().Padding(3).AlignCenter().Text(dep.EligibleMonths.ToString());
                            table.Cell().Padding(3).AlignRight().Text(FormatVn(dep.DeductionAmount));
                        }
                    }
                    else
                    {
                        table.Cell().ColumnSpan(7).Padding(8).AlignCenter().Text("Không phát sinh giảm trừ người phụ thuộc trong năm tính thuế.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                });

                // ═════════════════════════════════════════════════════════════════════
                // ── TRANG PHỤ LỤC 2: BẢNG KÊ CHI PHÍ Y TẾ VÀ GIÁO DỤC ──
                // ═════════════════════════════════════════════════════════════════════
                col.Item().PageBreak();

                col.Item().AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().Text("PHỤ LỤC: BẢNG KÊ CHI PHÍ Y TẾ VÀ GIÁO DỤC ĐƯỢC GIẢM TRỪ").Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                    titleCol.Item().Text("(Căn cứ Luật sửa đổi số 109/2025/QH15 — Áp dụng từ kỳ tính thuế năm 2026)").Italic().FontSize(8.5f);
                    titleCol.Item().Text($"Tổng chi phí Y tế: {FormatVn(Model.MedicalDeduction)} (Trần 23tr) | Tổng chi phí Giáo dục: {FormatVn(Model.EducationDeduction)} (Trần 24tr)").FontSize(8.5f).Bold();
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(25);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(5);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                    });

                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("STT").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Loại").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Đơn vị phát hành / Trường / BV").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("Số HĐ / Biên lai").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignCenter().Text("Ngày lập").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Tổng tiền").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text("Tính giảm trừ").Bold();

                    if (Model.Receipts.Any())
                    {
                        foreach (var r in Model.Receipts)
                        {
                            table.Cell().Padding(3).AlignCenter().Text(r.Index.ToString());
                            table.Cell().Padding(3).Text(r.Category).Bold();
                            table.Cell().Padding(3).Text(r.IssuerName);
                            table.Cell().Padding(3).Text(r.DocumentNumber);
                            table.Cell().Padding(3).AlignCenter().Text(r.IssueDate);
                            table.Cell().Padding(3).AlignRight().Text(FormatVn(r.TotalAmount));
                            table.Cell().Padding(3).AlignRight().Text(FormatVn(r.TaxEligibleAmount));
                        }
                    }
                    else
                    {
                        table.Cell().ColumnSpan(7).Padding(8).AlignCenter().Text("Không phát sinh chứng từ y tế hoặc giáo dục được xác nhận trong năm tính thuế.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                });
            });
        }

        private static void AddTaxRow(TableDescriptor table, string stt, string title, string code, decimal amount, bool isBold = false, string? customColor = null)
        {
            var cell1 = table.Cell().PaddingVertical(2).PaddingHorizontal(3).AlignCenter().Text(stt);
            var cell2 = table.Cell().PaddingVertical(2).PaddingHorizontal(3).Text(title);
            var cell3 = table.Cell().PaddingVertical(2).PaddingHorizontal(3).AlignCenter().Text(code);
            var cell4 = table.Cell().PaddingVertical(2).PaddingHorizontal(3).AlignRight().Text(FormatVn(amount));

            if (isBold)
            {
                cell1.Bold();
                cell2.Bold();
                cell3.Bold();
                cell4.Bold();
            }

            if (customColor != null)
            {
                var clr = Colors.Blue.Darken2;
                cell1.FontColor(clr);
                cell2.FontColor(clr);
                cell3.FontColor(clr);
                cell4.FontColor(clr);
            }
        }

        private static string FormatVn(decimal amount) => amount.ToString("N0", VnCulture);
    }
}

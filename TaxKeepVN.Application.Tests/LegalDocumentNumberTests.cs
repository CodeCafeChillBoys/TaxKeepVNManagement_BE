using FluentAssertions;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Domain.Constants;
using Xunit;

namespace TaxKeepVN.Application.Tests
{
    public class LegalDocumentNumberTests
    {
        [Theory]
        [InlineData("253/2026/NĐ-CP", "253/2026/ND-CP")]
        [InlineData("65 / 2013 / NĐ - CP", "65/2013/ND-CP")]
        [InlineData(" 111/2013/TT-BTC ", "111/2013/TT-BTC")]
        [InlineData("04/2007/QH12", "04/2007/QH12")]
        [InlineData("954/2020/UBTVQH14", "954/2020/UBTVQH14")]
        [InlineData("103/VBHN-VPQH", "103/VBHN-VPQH")]
        public void P1_Normalize_DocumentNumbers_Correctly(string input, string expected)
        {
            var result = LegalDocumentNumber.Normalize(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("04/2007/QH12", LawConstants.DocumentType.LUAT)]
        [InlineData("954/2020/UBTVQH14", LawConstants.DocumentType.NGHI_QUYET)]
        [InlineData("253/2026/ND-CP", LawConstants.DocumentType.NGHI_DINH)]
        [InlineData("253/2026/NĐ-CP", LawConstants.DocumentType.NGHI_DINH)]
        [InlineData("111/2013/TT-BTC", LawConstants.DocumentType.THONG_TU)]
        [InlineData("103/VBHN-VPQH", LawConstants.DocumentType.VBHN)]
        [InlineData("123/QD-BTC", LawConstants.DocumentType.QUYET_DINH)]
        [InlineData("SOMETHING_ELSE", LawConstants.DocumentType.KHAC)]
        public void P2_InferType_DocumentNumbers_Correctly(string input, string expected)
        {
            var result = LegalDocumentNumber.InferType(input);
            result.Should().Be(expected);
        }

        [Fact]
        public void P3_Level_Hierarchy_Respects_Order()
        {
            int luat = LegalDocumentNumber.Level(LawConstants.DocumentType.LUAT);
            int nq = LegalDocumentNumber.Level(LawConstants.DocumentType.NGHI_QUYET);
            int nd = LegalDocumentNumber.Level(LawConstants.DocumentType.NGHI_DINH);
            int qd = LegalDocumentNumber.Level(LawConstants.DocumentType.QUYET_DINH);
            int tt = LegalDocumentNumber.Level(LawConstants.DocumentType.THONG_TU);
            int khac = LegalDocumentNumber.Level(LawConstants.DocumentType.KHAC);

            luat.Should().Be(1);
            nq.Should().Be(2);
            nd.Should().Be(3);
            qd.Should().Be(4);
            tt.Should().Be(5);
            khac.Should().Be(6);

            // VBHN from VPQH has level 1, other VBHN has level 3
            LegalDocumentNumber.Level(LawConstants.DocumentType.VBHN, "103/VBHN-VPQH").Should().Be(1);
            LegalDocumentNumber.Level(LawConstants.DocumentType.VBHN, "05/VBHN-BTC").Should().Be(3);
        }
    }
}

namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Nhóm quan hệ người phụ thuộc theo Điều 9 Thông tư 111/2013/TT-BTC.
    /// </summary>
    public enum DependentRelationship
    {
        /// <summary>
        /// Nhóm 1 — Con: Con đẻ, con nuôi hợp pháp, con ngoài giá thú,
        /// con riêng của vợ hoặc chồng.
        /// </summary>
        CHILD,

        /// <summary>
        /// Nhóm 2 — Vợ hoặc Chồng.
        /// </summary>
        SPOUSE,

        /// <summary>
        /// Nhóm 3 — Cha, Mẹ: Cha mẹ đẻ, cha mẹ vợ/chồng,
        /// cha dượng, mẹ kế, cha mẹ nuôi hợp pháp.
        /// </summary>
        PARENT,

        /// <summary>
        /// Nhóm 4 — Cá nhân khác không nơi nương tựa:
        /// Anh chị em ruột; Ông bà nội/ngoại; Cô, dì, chú, bác, cậu ruột; Cháu ruột.
        /// </summary>
        OTHER_DEPENDENT
    }
}

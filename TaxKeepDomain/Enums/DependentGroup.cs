namespace TaxKeepVN.Domain.Enums
{
    public enum DependentGroup
    {
        // Nhóm 1: Con
        CHILD_UNDER_18,
        CHILD_OVER_18_DISABLED,
        CHILD_OVER_18_STUDYING,
        
        // Nhóm 2: Vợ hoặc chồng
        SPOUSE_DISABLED, // Trong độ tuổi lao động nhưng khuyết tật
        SPOUSE_RETIRED,  // Ngoài độ tuổi lao động
        
        // Nhóm 3: Cha, Mẹ
        PARENT_DISABLED, // Trong độ tuổi lao động nhưng khuyết tật
        PARENT_RETIRED,  // Ngoài độ tuổi lao động
        
        // Nhóm 4: Cá nhân khác không nơi nương tựa
        OTHER_HELPLESS
    }
}

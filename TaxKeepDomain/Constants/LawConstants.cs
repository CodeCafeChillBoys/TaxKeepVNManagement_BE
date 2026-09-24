namespace TaxKeepVN.Domain.Constants
{
    public static class LawConstants
    {
        public static class DocumentType
        {
            public const string LUAT = "LUAT";
            public const string NGHI_QUYET = "NGHI_QUYET";
            public const string NGHI_DINH = "NGHI_DINH";
            public const string THONG_TU = "THONG_TU";
            public const string VBHN = "VBHN";
            public const string QUYET_DINH = "QUYET_DINH";
            public const string KHAC = "KHAC";
        }

        public static class RelationType
        {
            public const string REPLACES = "REPLACES";
            public const string AMENDS = "AMENDS";
            public const string REPEALS = "REPEALS";
            public const string BASED_ON = "BASED_ON";
            public const string CONSOLIDATES = "CONSOLIDATES";
        }

        public static class RuleGroup
        {
            public const string SCHEDULE = "SCHEDULE";
            public const string DEDUCTION = "DEDUCTION";
            public const string DEPENDENT = "DEPENDENT";
            public const string SETTLEMENT = "SETTLEMENT";
            public const string WITHHOLDING = "WITHHOLDING";
            public const string RATE = "RATE";
            public const string EXEMPTION = "EXEMPTION";
        }

        public static class ValueKind
        {
            public const string AMOUNT = "AMOUNT";
            public const string RATE = "RATE";
            public const string SCHEDULE = "SCHEDULE";
            public const string JSON = "JSON";
            public const string FLAG = "FLAG";
            public const string TEXT = "TEXT";
        }

        public static class LegalStatus
        {
            public const string CON_HIEU_LUC = "CON_HIEU_LUC";
            public const string HET_HIEU_LUC_MOT_PHAN = "HET_HIEU_LUC_MOT_PHAN";
            public const string HET_HIEU_LUC = "HET_HIEU_LUC";
            public const string CHUA_RO = "CHUA_RO";
        }

        public static class ChangesetStatus
        {
            public const string EXTRACTING = "EXTRACTING";
            public const string READY = "READY";
            public const string STALE = "STALE";
            public const string MERGED = "MERGED";
            public const string REJECTED = "REJECTED";
            public const string FAILED = "FAILED";
        }

        public static class ChangesetOrigin
        {
            public const string AI = "AI";
            public const string MANUAL = "MANUAL";
            public const string IMPORT = "IMPORT";
        }

        public static class OpType
        {
            public const string ADD = "ADD";
            public const string UPDATE = "UPDATE";
            public const string END = "END";
            public const string RECITE = "RECITE";
        }

        public static class OpOrigin
        {
            public const string AI = "AI";
            public const string ADMIN = "ADMIN";
        }

        public static class Decision
        {
            public const string PENDING = "PENDING";
            public const string ACCEPTED = "ACCEPTED";
            public const string REJECTED = "REJECTED";
        }

        public static class ConflictState
        {
            public const string NONE = "NONE";
            public const string CONFLICT = "CONFLICT";
            public const string RESOLVED = "RESOLVED";
        }

        public static class OrphanAction
        {
            public const string RECITE = "RECITE";
            public const string END = "END";
            public const string KEEP = "KEEP";
        }

        public static class OpFlag
        {
            public const string NO_APPLY_FROM = "NO_APPLY_FROM";
            public const string NEW_CODE = "NEW_CODE";
            public const string INVALID_VALUE = "INVALID_VALUE";
            public const string NO_EVIDENCE = "NO_EVIDENCE";
            public const string ADD_TO_UPDATE = "ADD_TO_UPDATE";
            public const string ADD_TO_RECITE = "ADD_TO_RECITE";
            public const string UPDATE_TO_ADD = "UPDATE_TO_ADD";
            public const string SAME_AS_CURRENT = "SAME_AS_CURRENT";
            public const string UPDATE_TO_RECITE = "UPDATE_TO_RECITE";
            public const string RECITE_TO_ADD = "RECITE_TO_ADD";
            public const string RECITE_TO_UPDATE = "RECITE_TO_UPDATE";
            public const string LOWER_LEVEL_RECITE = "LOWER_LEVEL_RECITE";
            public const string NOTHING_TO_END = "NOTHING_TO_END";
            public const string DUPLICATE_OP = "DUPLICATE_OP";
        }

        public static class RuleCodes
        {
            public const string PIT_TAX_SCHEDULE = "PIT_TAX_SCHEDULE";
            public const string PIT_DEDUCTION_PERSONAL = "PIT_DEDUCTION_PERSONAL";
            public const string PIT_DEDUCTION_DEPENDENT = "PIT_DEDUCTION_DEPENDENT";
            public const string PIT_DEPENDENT_GROUPS = "PIT_DEPENDENT_GROUPS";
            public const string PIT_DEPENDENT_MAX_MONTHLY_INCOME = "PIT_DEPENDENT_MAX_MONTHLY_INCOME";
            public const string PIT_DEDUCTION_MEDICAL = "PIT_DEDUCTION_MEDICAL";
            public const string PIT_DEDUCTION_EDUCATION = "PIT_DEDUCTION_EDUCATION";
            public const string PIT_DEDUCTION_MANDATORY_INSURANCE = "PIT_DEDUCTION_MANDATORY_INSURANCE";
            public const string PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP = "PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP";
            public const string PIT_DEDUCTION_CHARITY = "PIT_DEDUCTION_CHARITY";
            public const string PIT_SETTLEMENT_SELF_REQUIRED_IF_MED_EDU = "PIT_SETTLEMENT_SELF_REQUIRED_IF_MED_EDU";
            public const string PIT_WITHHOLD_CASUAL_RATE = "PIT_WITHHOLD_CASUAL_RATE";
            public const string PIT_WITHHOLD_CASUAL_MIN_PAYMENT = "PIT_WITHHOLD_CASUAL_MIN_PAYMENT";
            public const string PIT_RATE_NON_RESIDENT_SALARY = "PIT_RATE_NON_RESIDENT_SALARY";
            public const string PIT_EXEMPTION_OVERTIME = "PIT_EXEMPTION_OVERTIME";
            public const string PIT_EXEMPTION_RETIREMENT_PENSION = "PIT_EXEMPTION_RETIREMENT_PENSION";
            public const string PIT_EXEMPTION_INSURANCE_COMPENSATION = "PIT_EXEMPTION_INSURANCE_COMPENSATION";
        }
    }
}

using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxAIProducerService
    {
        void PublishExtractionTask(TaxRuleExtractRequestMessage message);
    }
}

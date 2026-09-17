using TaxKeepVN.Application.DTOs.OcrAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IOcrAIProducerService
    {
        void PublishOcrTask(OcrExtractRequestMessage message);
    }
}

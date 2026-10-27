using TaxKeepVN.Application.DTOs.OcrAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ICertificateOcrProducerService
    {
        void PublishCertificateOcrTask(CertificateOcrRequestMessage message);
    }
}

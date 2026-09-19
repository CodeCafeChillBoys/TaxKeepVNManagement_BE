using System.Collections.Generic;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDocumentOcrProducerService
    {
        void PublishBatchOcrTasks(IEnumerable<DocumentOcrExtractRequestMessage> messages);
    }
}

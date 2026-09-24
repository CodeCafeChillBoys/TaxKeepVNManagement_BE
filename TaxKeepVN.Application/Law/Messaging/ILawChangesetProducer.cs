using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Law.Contract;

namespace TaxKeepVN.Application.Law.Messaging
{
    public interface ILawChangesetProducer
    {
        Task PublishExtractRequestAsync(LawChangesetExtractRequest request);
    }
}

using System.ServiceModel;
using System.Threading.Tasks;

namespace RaynorJdeApi.Models
{
    [ServiceContract(Namespace = "http://ws.configureone.com")]
    public interface IConceptService
    {
        [OperationContract]
        [return: MessageParameter(Name = "fireOrderReturn")]
        Task<FireOrderReturn> fireOrder(string in0);
    }
}

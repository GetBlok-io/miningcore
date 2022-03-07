using System.Data;
using System.Threading.Tasks;
using Miningcore.Persistence.Model;

namespace Miningcore.Persistence.Repositories
{
    public interface IMinerRepository
    {
        Task<MinerSettings> GetSettings(IDbConnection con, IDbTransaction tx, string poolId, string address);
        Task UpdatePaymentSettings(IDbConnection con, IDbTransaction tx, MinerSettings settings);
        Task UpdateDonationSettings(IDbConnection con, IDbTransaction tx, MinerSettings settings);
    }
}

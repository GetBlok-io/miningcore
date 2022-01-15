using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Model.Projections;


namespace Miningcore.Persistence.Repositories
{
    public interface ISmartPoolRepository
    {
        Task<SmartPool> GetSmartPoolEntryByEpochAsync(IDbConnection con, string poolId, long epoch);
        Task<SmartPool> GetSmartPoolEntryByEpochAndSubpoolAsync(IDbConnection con, string poolId, long epoch, string subpoolId);
        Task<SmartPool> GetSmartPoolEntryByHeightAsync(IDbConnection con, string poolId, long height);
        Task<SmartPool> GetSmartPoolEntryByTxAsync(IDbConnection con, string poolId, string transactionHash);
        Task<SmartPool> GetLastSmartPoolEntryAsync(IDbConnection con, string poolId);
        Task<SmartPool> GetLastSmartPoolEntryBySubpoolAsync(IDbConnection con, string poolId, string subpoolId);
        Task<SmartPool> GetLastSmartPoolEntryWithMinerAsync(IDbConnection con, string poolId, string miner);

    }
}

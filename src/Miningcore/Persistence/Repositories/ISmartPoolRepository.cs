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
        Task<SmartPool> GetSmartPoolEntryByHeightAsync(IDbConnection con, string poolId, long height);
        Task<SmartPool> GetSmartPoolEntryByTxAsync(IDbConnection con, string poolId, string transactionHash);
        Task<SmartPool> GetLastSmartPoolEntry(IDbConnection con, string poolId);

    }
}

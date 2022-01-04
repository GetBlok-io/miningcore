using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Model.Projections;


namespace Miningcore.Persistence.Repositories
{
    public interface IConsensusRepository
    {
        Task<Consensus[]> GetConsensusEntriesByEpochAsync(IDbConnection con, string poolId, long epoch);
        Task<Consensus[]> GetConsensusEntriesByHeightAsync(IDbConnection con, string poolId, long height);
        Task<Consensus[]> GetConsensusEntriesByTxAsync(IDbConnection con, string poolId, string transactionhash);
        Task<Consensus[]> GetConsensusEntriesByMiner(IDbConnection con, string poolId, string miner);


    }
}

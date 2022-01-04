using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Dapper;
using Miningcore.Extensions;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Model.Projections;

using Miningcore.Persistence.Repositories;
using Miningcore.Util;
using NLog;
using Npgsql;
using NpgsqlTypes;

namespace Miningcore.Persistence.Postgres.Repositories
{
    public class ConsensusRepository : IConsensusRepository
    {
        public ConsensusRepository(IMapper mapper)
        {
            this.mapper = mapper;
        }

        private readonly IMapper mapper;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public async Task<Consensus[]> GetConsensusEntriesByEpochAsync(IDbConnection con, string poolId, long epoch)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND epoch = @epoch";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, epoch }))
                .Select(mapper.Map<Consensus>)
                .ToArray(); 
        }

        public async Task<Consensus[]> GetConsensusEntriesByHeightAsync(IDbConnection con, string poolId, long height)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND height = @height";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, height }))
                .Select(mapper.Map<Consensus>)
                .ToArray();
        }

        public async Task<Consensus[]> GetConsensusEntriesByTxAsync(IDbConnection con, string poolId, string transactionhash)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND transactionhash = @transactionhash";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, transactionhash }))
                .Select(mapper.Map<Consensus>)
                .ToArray();
        }

        public async Task<Consensus[]> GetConsensusEntriesByMinerAsync(IDbConnection con, string poolId, string miner)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND miner = @miner";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, miner }))
                .Select(mapper.Map<Consensus>)
                .ToArray();
        }

        public async Task<Consensus[]> GetConsensusEntriesByMinerAndEpochAsync(IDbConnection con, string poolId, long epoch, string miner)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND epoch = @epoch AND miner = @miner";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, epoch,  miner }))
                .Select(mapper.Map<Consensus>)
                .ToArray();
        }

        public async Task<Consensus[]> GetConsensusEntriesByMinerAndHeightAsync(IDbConnection con, string poolId, long height, string miner)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM consensus WHERE poolid = @poolId AND height = @height AND miner = @miner";

            return (await con.QueryAsync<Entities.Consensus>(query, new { poolId, height, miner }))
                .Select(mapper.Map<Consensus>)
                .ToArray();
        }

    }
}


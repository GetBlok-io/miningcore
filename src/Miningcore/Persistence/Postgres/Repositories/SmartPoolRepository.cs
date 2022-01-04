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
    public class SmartPoolRepository : ISmartPoolRepository
    {
        public SmartPoolRepository(IMapper mapper)
        {
            this.mapper = mapper;
        }

        private readonly IMapper mapper;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public async Task<SmartPool> GetSmartPoolEntryByEpochAsync(IDbConnection con, string poolId, long epoch)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM smartpool_data WHERE poolid = @poolId AND epoch = @epoch";

            return (await con.QueryAsync<Entities.SmartPool>(query, new { poolId, epoch }))
              .Select(mapper.Map<SmartPool>)
              .FirstOrDefault();

        }

        public async Task<SmartPool> GetSmartPoolEntryByHeightAsync(IDbConnection con, string poolId, long height)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM smartpool_data WHERE poolid = @poolId AND height = @height";

            return (await con.QueryAsync<Entities.SmartPool>(query, new { poolId, height }))
                .Select(mapper.Map<SmartPool>)
                .FirstOrDefault();
        }

        public async Task<SmartPool> GetSmartPoolEntryByTxAsync(IDbConnection con, string poolId, string transactionhash)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM smartpool_data WHERE poolid = @poolId AND transactionhash = @transactionhash";

            return (await con.QueryAsync<Entities.SmartPool>(query, new { poolId, transactionhash }))
              .Select(mapper.Map<SmartPool>)
              .FirstOrDefault();
        }

        public async Task<SmartPool> GetLastSmartPoolEntryAsync(IDbConnection con, string poolId)
        {
            logger.LogInvoke(new object[] { poolId });

            var query = $"SELECT * FROM smartpool_data WHERE poolid = @poolId ORDER BY created DESC FETCH NEXT 1 ROW ONLY";

            return (await con.QueryAsync<Entities.SmartPool>(query, new { poolId }))
              .Select(mapper.Map<SmartPool>)
              .FirstOrDefault();
        }
    }
}


using System;

namespace Miningcore.Persistence.Postgres.Entities
{
    public class SmartPool
    {

        public string PoolId { get; set; }
        public string TransactionHash { get; set; }
        public long Epoch { get; set; }
        public long Height { get; set; }
        public string[] Members { get; set; }
        public long[] Fees { get; set; }
        public long[] Info { get; set; }
        public string[] Operators { get; set; }
        public string SmartPoolNFT { get; set; }
        public DateTime Created { get; set; }
        public long[] Blocks { get; set; }

        public string Subpool_Id { get; set; }
    }
}

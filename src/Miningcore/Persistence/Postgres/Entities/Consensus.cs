using System;

namespace Miningcore.Persistence.Postgres.Entities
{
    public class Consensus
    {

        public string PoolId { get; set; }
        public string TransactionHash { get; set; }
        public long Epoch { get; set; }
        public long Height { get; set; }
        public string SmartPoolNFT { get; set; }
        public string Miner { get; set; }
        public long Shares { get; set; }
        public long MinPayout { get; set; }
        public long StoredPayout { get; set; }
        public DateTime Created { get; set; }
        public long Paid { get; set; }
    }
}

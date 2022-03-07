using System;

namespace Miningcore.Persistence.Model
{
    public class MinerSettings
    {
        public string PoolId { get; set; }
        public string Address { get; set; }
        public decimal PaymentThreshold { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public String Donation { get; set; }
    }
}

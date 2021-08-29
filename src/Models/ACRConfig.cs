using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACR_SyncTool.Models
{
    public class ACRConfig
    {
        public string Host { get; set; } = String.Empty;
        public string TenantId { get; set; } = String.Empty;
        public string ClientId { get; set; } = String.Empty;
        public string Secret { get; set; } = String.Empty;
    }
}

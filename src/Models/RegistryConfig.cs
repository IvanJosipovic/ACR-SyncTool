using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACR_SyncTool.Models
{
    public class RegistryConfig
    {
        public string Host { get; set; } = String.Empty;
        public string? AuthType { get; set; }
        public string Username { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
    }
}

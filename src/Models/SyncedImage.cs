using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACR_SyncTool.Models
{
    public class SyncedImage
    {
        public string Image { get; set; }

        public string Semver { get; set; }

        public string Regex { get; set; }
    }
}

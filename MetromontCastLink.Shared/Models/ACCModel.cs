using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetromontCastLink.Shared.Models
{
    public class ACCModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Urn { get; set; } = "";
        public string Version { get; set; } = "";
        public long Size { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime LastModified { get; set; }
    }
}
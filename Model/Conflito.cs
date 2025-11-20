using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificadorGit.Model
{
    internal class Conflito
    {
        public string Arquivo { get; set; }
        public string DiffLocal { get; internal set; }
        public string DiffRemoto { get; internal set; }
    }
}

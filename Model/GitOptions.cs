using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificadorGit.Model
{
    internal class GitOptions
    {
        public string CaminhoRepositorio { get; set; } = string.Empty;
        public string Branch { get; set; }
        public string Remota { get; set; }
    }
}

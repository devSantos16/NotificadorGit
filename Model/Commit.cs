using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NotificadorGit.Model
{
    internal class Commit
    {
        public string Sha { get; set; }
        public string Mensagem { get; set; }
        public string Autor { get; set; }
        public string Email { get; set; }
        public DateTimeOffset Data { get; set; }
        public List<PossivelConflito> Conflitos { get; set; }
    }
}

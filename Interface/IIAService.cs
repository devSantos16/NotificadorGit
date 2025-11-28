using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotificadorGit.Interface
{
    internal interface IIAService
    {
        public Task<(bool IsTrue, string message)> GerarPrompt(string prompt, CancellationToken cancellationToken = default);
    }
}

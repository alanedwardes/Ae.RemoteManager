using System;
using System.Collections.Generic;

namespace Ae.Dns.Console
{
    public sealed class RemoteManagerConfiguration
    {
        public IDictionary<string, RemoteInstructions> Instructions { get; set; } = new Dictionary<string, RemoteInstructions>();
    }

    public sealed class RemoteInstructions
    {
        public bool Enabled { get; set; } = true;
        public bool Testing { get; set; }
        public string Cron { get; set; }
        public string Endpoint { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string PrivateKey { get; set; }
        public IList<string> Commands { get; set;} = new List<string>();
    }
}

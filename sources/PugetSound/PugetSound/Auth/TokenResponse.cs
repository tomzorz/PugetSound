using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PugetSound.Auth
{
    public class TokenResponse
    {
        public string access_token { get; set; }

        public string token_type { get; set; }

        public string scope { get; set; }

        public int expires_in { get; set; }

        public string refresh_token { get; set; }
    }

}

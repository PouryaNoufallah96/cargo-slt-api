using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using Utilities.Extension;
using Utilities.Constants;
using Utilities.Exceptions.Common;

namespace Utilities.Middlewares
{
    public class FirewallMiddleware(RequestDelegate next, FirewallSettings firewallSettings)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            foreach (var rule in firewallSettings.Rules)
            {
                if (Regex.Match(context.Request.Path, rule.Regex, RegexOptions.IgnoreCase).Success)
                {
                    var ipAddress = context.GetRequestIpv4();

                    if (rule.IPAddresses == null)
                        rule.IPAddresses = new string[0];

                    if (rule.IPAddresses.Contains(ipAddress) || rule.IPAddresses.Contains("*"))
                    {
                        if (rule.Policy == FirewallRulePolicy.Allow)
                        {
                            await next(context);
                            return;
                        }
                        else if (rule.Policy == FirewallRulePolicy.Deny)
                            throw new BaseException("Do not play around here");
                    }

                }
            }

            throw new BaseException("f");
        }
    }
}

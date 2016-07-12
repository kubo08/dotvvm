using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.ResourceManagement;
using System.Collections.Concurrent;
using System.Threading;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.ViewModel;
using DotVVM.Framework.ViewModel.Serialization;
using Microsoft.AspNetCore.Http;

namespace DotVVM.Framework.Hosting
{
    /// <summary>
    /// A middleware that handles DotVVM HTTP requests.
    /// </summary>
    public class DotvvmMiddleware
    {
        public readonly DotvvmConfiguration Configuration;

        private const string GooglebotHashbangEscapedFragment = "_escaped_fragment_=";


        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmMiddleware"/> class.
        /// </summary>
        public DotvvmMiddleware(RequestDelegate next, DotvvmConfiguration configuration)
        {
            this.next = next;
            Configuration = configuration;
        }
        private int configurationSaved = 0;
        private readonly RequestDelegate next;

        /// <summary>
        /// Process an individual request.
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            if (Interlocked.Exchange(ref configurationSaved, 1) == 0) {
                VisualStudioHelper.DumpConfiguration(Configuration, Configuration.ApplicationPhysicalPath);
            }
            // create the context
            var dotvvmContext = new DotvvmRequestContext()
            {
                HttpContext = context,
                Configuration = Configuration,
                ResourceManager = new ResourceManager(Configuration),
                ViewModelSerializer = Configuration.ServiceLocator.GetService<IViewModelSerializer>()
            };
            context.Items.Add(HostingConstants.DotvvmRequestContextOwinKey, dotvvmContext);

            // attempt to translate Googlebot hashbang espaced fragment URL to a plain URL string.
            string url;
            if (!TryParseGooglebotHashbangEscapedFragment(context.Request.QueryString, out url))
            {
                url = context.Request.Path.Value;
            }
            url = url.Trim('/');

            // find the route
            IDictionary<string, object> parameters = null;
            var route = Configuration.RouteTable.FirstOrDefault(r => r.IsMatch(url, out parameters));

            if (route != null)
            {
                // handle the request
                dotvvmContext.Route = route;
                dotvvmContext.Parameters = parameters;
                dotvvmContext.Query = context.Request.Query
                    .ToDictionary(d => d.Key, d => d.Value.Count == 1 ? (object) d.Value[0] : d.Value.ToArray());

                try
                {
                    await route.ProcessRequest(dotvvmContext);
                    return;
                }
                catch (DotvvmInterruptRequestExecutionException)
                {
                    // the response has already been generated, do nothing
                    return;
                }
            }
            
            // we cannot handle the request, pass it to another component
            await next(context);
        }

        /// <summary>
        /// Attempts to recognize request made by Googlebot in its effort to crawl links for AJAX SPAs.
        /// </summary>
        /// <param name="queryString">
        /// The query string of the request to try to match the Googlebot hashbang escaped fragment on.
        /// </param>
        /// <param name="url">
        /// The plain URL string that the hasbang escaped fragment represents.
        /// </param>
        /// <returns>
        /// <code>true</code>, if the URL contains valid Googlebot hashbang escaped fragment; otherwise <code>false</code>.
        /// </returns>
        /// <seealso cref="https://developers.google.com/webmasters/ajax-crawling/docs/getting-started"/>
        private bool TryParseGooglebotHashbangEscapedFragment(QueryString queryString, out string url)
        {
            if (queryString.Value.StartsWith(GooglebotHashbangEscapedFragment, StringComparison.Ordinal))
            {
                url = queryString.Value.Substring(GooglebotHashbangEscapedFragment.Length);
                return true;
            }

            url = null;
            return false;
        }

        public static bool IsInCurrentVirtualDirectory(HttpContext context, ref string url)
        {
            var virtualDirectory = GetVirtualDirectory(context);
            if (url.StartsWith(virtualDirectory, StringComparison.Ordinal))
            {
                url = url.Substring(virtualDirectory.Length).TrimStart('/');
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Determines the current OWIN virtual directory.
        /// </summary>
        public static string GetVirtualDirectory(HttpContext context)
        {
            return context.Request.PathBase.Value.Trim('/');
        }

        public static string GetCleanRequestUrl(HttpContext context)
        {
            return context.Request.Path.Value.TrimStart('/').TrimEnd('/');
        }
    }
}
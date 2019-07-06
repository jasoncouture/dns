using System;
using System.Threading;
using System.Threading.Tasks;
using DNS.Protocol;
using DNS.Protocol.Utils;
using System.Collections.Generic;
using System.Linq;

namespace DNS.Client.RequestResolver
{
    /// <summary>
    /// Resolve requests using multiple IRequestResolvers, taking the first result.
    /// </summary>
    public class ParallelRequestResolver : IRequestResolver
    {
        private List<IRequestResolver> resolvers;
        /// <summary>
        /// Create a new instance of ParallelRequestResolver
        /// </summary>
        /// <param name="innerResolvers"></param>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="innerResolvers">innerResolvers</paramref> does not contain at least 1 resolver.</exception>
        public ParallelRequestResolver(IEnumerable<IRequestResolver> innerResolvers)
        {
            resolvers = innerResolvers.ToList();
            if (resolvers.Count == 0) throw new ArgumentException("No inner DNS resolvers were provided!", nameof(innerResolvers));
        }

        public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
        {
            CancellationTokenSource requestCompletedCancellationSource = new CancellationTokenSource();
            var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(requestCompletedCancellationSource.Token, cancellationToken);
            List<Exception> exceptions = new List<Exception>();
            var tasks = resolvers.Select(i => i.Resolve(request, linkedSource.Token)).ToList();
            bool done = false;
            IResponse response = null;
            while (response == null)
            {
                if (tasks.Count == 0)
                    break;
                var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                try
                {
                    // We could check the task manually, but this way will handle edge cases.
                    response = await completedTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                tasks.Remove(completedTask);
            }
            
            if (tasks.Any())
            {
                tasks = tasks.Select(i => i.SwallowExceptions(null)).ToList();
            }

            requestCompletedCancellationSource.Cancel();
            if (response == null)
            {
                throw new AggregateException(exceptions);
            }
            
            // Should response be wrapped with something that exposes exceptions?
            // IE: public class ResponseWithExceptions : IResponse
            // new ResponseWithExceptions(response, exceptions)
            return response;
        }
    }
}

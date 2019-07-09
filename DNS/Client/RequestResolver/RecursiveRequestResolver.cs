using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;

namespace DNS.Client.RequestResolver
{
    public class RecursiveRequestResolver : IRequestResolver {
        private IRequestResolver innerResolver;

        public RecursiveRequestResolver(IRequestResolver innerResolver) {
            if (innerResolver == null) throw new ArgumentNullException(nameof(innerResolver));
            this.innerResolver = innerResolver;
        }
        public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken)) {
            bool done = false;
            var resolver = innerResolver;
            while (true) {
                var result = await resolver.Resolve(request, cancellationToken);
                if (!request.RecursionDesired || result.AnswerRecords.Count != 0 ||
                    result.AdditionalRecords.Count <= 0) {
                    return result;
                }

                var nextServers = result.AdditionalRecords.Cast<IPAddressResourceRecord>();
                // TODO: This should probably get cached. I wonder if we can steal ASP.NET's MemoryCache for this?
                resolver = new ParallelRequestResolver(nextServers.Select(i => UdpRequestResolver.Create(i.IPAddress)).Cast<IRequestResolver>().ToArray());
            }
        }
    }
}

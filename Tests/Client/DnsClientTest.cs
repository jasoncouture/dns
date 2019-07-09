using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DNS.Client;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;

namespace DNS.Tests.Client
{

    public class DnsClientTest
    {
        [Fact]
        public async Task ClientLookup()
        {
            DnsClient client = new DnsClient(new IPAddressRequestResolver());
            IList<IPAddress> ips = await client.Lookup("google.com");

            Assert.Equal(1, ips.Count);
            Assert.Equal("192.168.0.1", ips[0].ToString());
        }

        [Fact]
        public async Task ClientReverse()
        {
            DnsClient client = new DnsClient(new PointerRequestResolver());
            string domain = await client.Reverse("192.168.0.1");

            Assert.Equal("google.com", domain);
        }

        [Fact]
        public async Task ParallelLookupTest()
        {
            DnsClient client = new DnsClient(new ParallelRequestResolver(new NameErrorRequestResolver(), new IPAddressRequestResolver()));
            IList<IPAddress> ips = await client.Lookup("google.com");

            Assert.Equal(1, ips.Count);
            Assert.Equal("192.168.0.1", ips[0].ToString());
        }

        [Fact]
        public async Task ParallelLookupError()
        {
            DnsClient client = new DnsClient(new ParallelRequestResolver(new NameErrorRequestResolver(), new NameErrorRequestResolver(), new ThrowsExceptionRequestResolver(), new ThrowsExceptionRequestResolver()));
            var responseException = await Assert.ThrowsAsync<ResponseException>(() =>
            {
                return client.Lookup("google.com");
            });


            client = new DnsClient(new ParallelRequestResolver(new ThrowsExceptionRequestResolver(), new ThrowsExceptionRequestResolver()));
            var aggregateException = await Assert.ThrowsAsync<AggregateException>(() =>
            {
                return client.Lookup("google.com");
            });

            Assert.Equal(2, aggregateException.InnerExceptions.Count);
            Assert.All(aggregateException.InnerExceptions, i => Assert.IsType<NotImplementedException>(i));
        }

        [Fact]
        public void ParallelResolverConstructorThrows()
        {
            Assert.Throws<ArgumentException>(() => new ParallelRequestResolver());
            Assert.Throws<ArgumentNullException>(() => new ParallelRequestResolver(null));
        }

        [Fact]
        public async Task RecursiveResolverTest() {
            var resolver = new RecursiveRequestResolver(new ParallelRequestResolver(
                UdpRequestResolver.Create(IPAddress.Parse("192.33.4.12")),
                UdpRequestResolver.Create(IPAddress.Parse("192.41.162.30"))));
            DnsClient client = new DnsClient(resolver);
            var response = await client.Resolve("google.com", RecordType.A);
            Assert.Equal(ResponseCode.NoError, response.ResponseCode);
            Assert.True(response.AnswerRecords.Count > 0);
        }

        [Fact]
        public async Task ClientNameError()
        {
            DnsClient client = new DnsClient(new NameErrorRequestResolver());

            await Assert.ThrowsAsync<ResponseException>(() =>
            {
                return client.Lookup("google.com");
            });
        }

        private class ThrowsExceptionRequestResolver : IRequestResolver
        {
            public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class IPAddressRequestResolver : IRequestResolver
        {
            public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                IResponse response = Response.FromRequest(request);
                IResourceRecord record = new IPAddressResourceRecord(
                    new Domain("google.com"),
                    IPAddress.Parse("192.168.0.1"));

                response.AnswerRecords.Add(record);
                return Task.FromResult(response);
            }
        }

        private class PointerRequestResolver : IRequestResolver
        {
            public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                IResponse response = Response.FromRequest(request);
                IResourceRecord record = new PointerResourceRecord(
                    IPAddress.Parse("192.168.0.1"),
                    new Domain("google.com"));

                response.AnswerRecords.Add(record);
                return Task.FromResult(response);
            }
        }

        private class NameErrorRequestResolver : IRequestResolver
        {
            public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                IResponse response = Response.FromRequest(request);
                response.ResponseCode = ResponseCode.NameError;
                return Task.FromResult(response);
            }
        }
    }
}

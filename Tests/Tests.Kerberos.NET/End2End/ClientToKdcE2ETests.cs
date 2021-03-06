﻿using Kerberos.NET;
using Kerberos.NET.Client;
using Kerberos.NET.Credentials;
using Kerberos.NET.Crypto;
using Kerberos.NET.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using static Tests.Kerberos.NET.KdcListener;

namespace Tests.Kerberos.NET

{
    [TestClass]
    public class ClientToKdcE2ETests : KdcListenerTestBase
    {
        private const int ConcurrentThreads = 2;
        private const int RequestsPerThread = 5;

        [TestMethod]
        public async Task E2E()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}"
                );

                listener.Stop();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(KerberosProtocolException))]
        public async Task E2E_SName_Not_Found()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    spn: "host/not.found",
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_PKINIT()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    TestAtCorpUserName,
                    overrideKdc: $"127.0.0.1:{port}",
                    cert: cert
                );

                listener.Stop();
            }
        }

        [TestMethod, ExpectedException(typeof(NotSupportedException))]
        public async Task PKINIT_Unsupported_KeyAgreement_None()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            using (var client = CreateClient(listener))
            {
                var kerbCred = new TrustedAsymmetricCredential(cert, AdminAtCorpUserName)
                {
                    KeyAgreement = KeyAgreementAlgorithm.None,
                    SupportsDiffieHellman = false,
                    SupportsEllipticCurveDiffieHellman = false
                };

                await client.Authenticate(kerbCred);
            }
        }

        [TestMethod, ExpectedException(typeof(PlatformNotSupportedException))]
        public async Task PKINIT_Unsupported_KeyAgreement_EC()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            using (var client = CreateClient(listener))
            {
                var kerbCred = new TrustedAsymmetricCredential(cert, AdminAtCorpUserName)
                {
                    KeyAgreement = KeyAgreementAlgorithm.None,
                    SupportsDiffieHellman = false,
                    SupportsEllipticCurveDiffieHellman = true
                };

                await client.Authenticate(kerbCred);
            }
        }

        [TestMethod, ExpectedException(typeof(PlatformNotSupportedException))]
        public async Task PKINIT_Unsupported_KeyAgreement_P256()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            using (var client = CreateClient(listener))
            {
                var kerbCred = new TrustedAsymmetricCredential(cert, AdminAtCorpUserName)
                {
                    KeyAgreement = KeyAgreementAlgorithm.EllipticCurveDiffieHellmanP256,
                    SupportsDiffieHellman = false,
                    SupportsEllipticCurveDiffieHellman = true
                };

                await client.Authenticate(kerbCred);
            }
        }

        [TestMethod, ExpectedException(typeof(PlatformNotSupportedException))]
        public async Task PKINIT_Unsupported_KeyAgreement_P384()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            using (var client = CreateClient(listener))
            {
                var kerbCred = new TrustedAsymmetricCredential(cert, AdminAtCorpUserName)
                {
                    KeyAgreement = KeyAgreementAlgorithm.EllipticCurveDiffieHellmanP384,
                    SupportsDiffieHellman = false,
                    SupportsEllipticCurveDiffieHellman = true
                };

                await client.Authenticate(kerbCred);
            }
        }

        [TestMethod, ExpectedException(typeof(PlatformNotSupportedException))]
        public async Task PKINIT_Unsupported_KeyAgreement_P521()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            using (var client = CreateClient(listener))
            {
                var kerbCred = new TrustedAsymmetricCredential(cert, AdminAtCorpUserName)
                {
                    KeyAgreement = KeyAgreementAlgorithm.EllipticCurveDiffieHellmanP521,
                    SupportsDiffieHellman = false,
                    SupportsEllipticCurveDiffieHellman = true
                };

                await client.Authenticate(kerbCred);
            }
        }

        [TestMethod, ExpectedException(typeof(KerberosProtocolException))]
        public async Task E2E_PKINIT_Modp2_Fails()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            using (var listener = StartListener(port))
            {
                try
                {
                    await RequestAndValidateTickets(
                        listener,
                        TestAtCorpUserName,
                        overrideKdc: $"127.0.0.1:{port}",
                        cert: cert,
                        keyAgreement: KeyAgreementAlgorithm.DiffieHellmanModp2
                    );
                }
                catch (KerberosProtocolException kex)
                {
                    Assert.IsTrue(kex.Message.Contains("Unsupported Diffie Hellman"));
                    throw;
                }
                finally
                {
                    listener.Stop();
                }
            }
        }

        [TestMethod]
        public async Task E2E_PKINIT_Synchronous()
        {
            var port = NextPort();

            var cert = new X509Certificate2(ReadDataFile("testuser.pfx"), "p");

            var requests = RequestsPerThread;

            using (var listener = StartListener(port))
            {
                for (var i = 0; i < requests; i++)
                {
                    await RequestAndValidateTickets(
                        listener,
                        TestAtCorpUserName,
                        overrideKdc: $"127.0.0.1:{port}",
                        cert: cert
                    );
                }

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_NoPac()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithCaching()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    caching: true
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithCaching_NoPac()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    caching: true,
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithNegotiate()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    encodeNego: true
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithNegotiate_NoCache()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    encodeNego: true,
                    caching: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithNegotiate_NoCache_NoPac()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    encodeNego: true,
                    caching: false,
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_WithNegotiate_NoPac()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    encodeNego: true,
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_S4U()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    s4u: "blah@corp.identityintervention.com"
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_S4U_NoPac()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                await RequestAndValidateTickets(
                    listener,
                    AdminAtCorpUserName,
                    FakeAdminAtCorpPassword,
                    $"127.0.0.1:{port}",
                    s4u: "blah@corp.identityintervention.com",
                    includePac: false
                );

                listener.Stop();
            }
        }

        [TestMethod]
        public async Task E2E_U2U()
        {
            var port = NextPort();

            using (var listener = StartListener(port))
            {
                var kerbClientCred = new KerberosPasswordCredential(AdminAtCorpUserName, FakeAdminAtCorpPassword);
                var kerbServerCred = new KerberosPasswordCredential("u2u@corp.identityintervention.com", FakeAdminAtCorpPassword);

                using (var client = CreateClient(listener))
                using (var server = CreateClient(listener))
                {
                    await client.Authenticate(kerbClientCred);

                    await server.Authenticate(kerbClientCred);

                    var serverEntry = await server.Cache.Get<KerberosClientCacheEntry>($"krbtgt/{server.DefaultDomain}");

                    var serverTgt = serverEntry.KdcResponse.Ticket;

                    var apReq = await client.GetServiceTicket("host/u2u.corp.identityintervention.com",
                        ApOptions.MutualRequired | ApOptions.UseSessionKey,
                        u2uServerTicket: serverTgt
                    );

                    Assert.IsNotNull(apReq);

                    var decrypted = new DecryptedKrbApReq(apReq);

                    Assert.IsNull(decrypted.Ticket);

                    decrypted.Decrypt(serverEntry.SessionKey.AsKey());

                    decrypted.Validate(ValidationActions.All);

                    Assert.IsNotNull(decrypted.Ticket);

                    Assert.AreEqual("host/u2u.corp.identityintervention.com", decrypted.SName.FullyQualifiedName);
                }

                listener.Stop();
            }
        }

        //[TestMethod, ExpectedException(typeof(TimeoutException))]
        //public async Task ReceiveTimeout()
        //{
        //    var port = NextPort();
        //    var log = new FakeExceptionLoggerFactory();

        //    var options = new ListenerOptions
        //    {
        //        ListeningOn = new IPEndPoint(IPAddress.Loopback, port),
        //        DefaultRealm = "corp2.identityintervention.com".ToUpper(),
        //        IsDebug = true,
        //        RealmLocator = realm => LocateRealm(realm, slow: true),
        //        ReceiveTimeout = TimeSpan.FromMilliseconds(1),
        //        Log = log
        //    };

        //    KdcServiceListener listener = new KdcServiceListener(options);

        //    _ = listener.Start();

        //    try
        //    {
        //        await RequestAndValidateTickets(null, AdminAtCorpUserName, FakeAdminAtCorpPassword, $"127.0.0.1:{port}");
        //    }
        //    catch
        //    {
        //    }

        //    listener.Stop();

        //    var timeout = log.Exceptions.FirstOrDefault(e => e is TimeoutException);

        //    Assert.IsNotNull(timeout);

        //    throw timeout;
        //}

        [TestMethod]
        public async Task E2E_MultithreadedClient()
        {
            var port = NextPort();

            var threads = ConcurrentThreads;
            var requests = RequestsPerThread;

            var cacheTickets = false;
            var encodeNego = false;
            var includePac = false;

            string kdc = $"127.0.0.1:{port}";
            //string kdc = "10.0.0.21:88";

            await MultithreadedRequests(port, threads, requests, cacheTickets, encodeNego, includePac, kdc);
        }

        [TestMethod]
        public async Task E2E_MultithreadedClient_Cache()
        {
            var port = NextPort();

            var threads = ConcurrentThreads;
            var requests = RequestsPerThread;
            var cacheTickets = true;
            var encodeNego = false;
            var includePac = false;

            string kdc = $"127.0.0.1:{port}";
            //string kdc = "10.0.0.21:88";

            await MultithreadedRequests(port, threads, requests, cacheTickets, encodeNego, includePac, kdc);
        }

        [TestMethod]
        public async Task E2E_MultithreadedClient_Cache_Nego()
        {
            var port = NextPort();

            var threads = ConcurrentThreads;
            var requests = RequestsPerThread;
            var cacheTickets = true;
            var encodeNego = true;
            var includePac = false;

            string kdc = $"127.0.0.1:{port}";
            //string kdc = "10.0.0.21:88";

            await MultithreadedRequests(port, threads, requests, cacheTickets, encodeNego, includePac, kdc);
        }

        [TestMethod]
        public async Task E2E_MultithreadedClient_Cache_Nego_Pac()
        {
            var port = NextPort();

            var threads = ConcurrentThreads;
            var requests = RequestsPerThread;
            var cacheTickets = true;
            var encodeNego = true;
            var includePac = true;

            string kdc = $"127.0.0.1:{port}";
            //string kdc = "10.0.0.21:88";

            await MultithreadedRequests(port, threads, requests, cacheTickets, encodeNego, includePac, kdc);
        }
    }
}

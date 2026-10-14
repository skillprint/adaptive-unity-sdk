using System;
using System.Collections;
using NUnit.Framework;
using Skillprint.SDK;
using Skillprint.SDK.API;
using UnityEngine.Networking;

namespace Skillprint.SDK.Tests
{
    /// <summary>
    /// Tests for SkillprintAuth — which credential each auth mode sends, and how the SDK
    /// token is fetched, cached, refreshed and invalidated.
    /// </summary>
    [TestFixture]
    public class SkillprintAuthTests
    {
        private sealed class CountingProvider : ISkillprintTokenProvider
        {
            public int Calls;
            public string LastPlayerId;
            public Func<int, SkillprintTokenResult> Respond;

            public void FetchToken(string playerId, Action<SkillprintTokenResult> onComplete)
            {
                Calls++;
                LastPlayerId = playerId;
                onComplete(Respond(Calls));
            }
        }

        /// <summary>Drive a coroutine to completion synchronously (providers here answer immediately).</summary>
        private static void Run(IEnumerator routine)
        {
            int guard = 0;
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    Run(nested);
                }
                Assert.Less(++guard, 10000, "Coroutine did not finish.");
            }
        }

        private static (bool ok, string error) Ensure(SkillprintAuth auth, bool force = false)
        {
            bool ok = false;
            string error = null;
            Run(auth.EnsureReady(force, (o, e) =>
            {
                ok = o;
                error = e;
            }));
            return (ok, error);
        }

        private static string AuthorizationHeader(SkillprintAuth auth)
        {
            using (var request = new UnityWebRequest("https://example.invalid/", "GET"))
            {
                auth.Apply(request);
                return request.GetRequestHeader("Authorization");
            }
        }

        // ───────────── PartnerApiKey mode ─────────────────

        [Test]
        public void ApiKeyMode_SendsApiKey()
        {
            var auth = new SkillprintAuth(SkillprintAuthMode.PartnerApiKey, "pre.secret", null);
            Assert.IsTrue(Ensure(auth).ok);
            Assert.AreEqual("Api-Key pre.secret", AuthorizationHeader(auth));
        }

        // ───────────── TokenProvider mode ─────────────────

        [Test]
        public void TokenMode_WithoutProviderOrToken_IsNotReady()
        {
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            var result = Ensure(auth);
            Assert.IsFalse(result.ok);
            StringAssert.Contains("SetTokenProvider", result.error);
        }

        [Test]
        public void TokenMode_NeverSendsApiKey()
        {
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, "pre.secret", null);
            auth.SetToken("spt_abc", null);
            Assert.AreEqual("Bearer spt_abc", AuthorizationHeader(auth));
        }

        [Test]
        public void TokenMode_FetchesOnceAndCaches()
        {
            var provider = new CountingProvider
            {
                Respond = n => SkillprintTokenResult.FromExpiresIn("spt_" + n, 3600),
            };
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(provider);
            auth.SetPlayer("player-1");

            Assert.IsTrue(Ensure(auth).ok);
            Assert.IsTrue(Ensure(auth).ok);

            Assert.AreEqual(1, provider.Calls);
            Assert.AreEqual("player-1", provider.LastPlayerId);
            Assert.AreEqual("Bearer spt_1", AuthorizationHeader(auth));
        }

        [Test]
        public void TokenMode_RefreshesTokenCloseToExpiry()
        {
            var provider = new CountingProvider
            {
                // First token expires inside the refresh window; the second is long-lived.
                Respond = n => SkillprintTokenResult.FromExpiresIn("spt_" + n, n == 1 ? 10 : 3600),
            };
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(provider);

            Ensure(auth);
            Ensure(auth);

            Assert.AreEqual(2, provider.Calls);
            Assert.AreEqual("Bearer spt_2", AuthorizationHeader(auth));
        }

        [Test]
        public void TokenMode_ForceRefreshAsksProviderAgain()
        {
            var provider = new CountingProvider
            {
                Respond = n => SkillprintTokenResult.FromExpiresIn("spt_" + n, 3600),
            };
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(provider);

            Ensure(auth);
            Ensure(auth, force: true);

            Assert.AreEqual(2, provider.Calls);
        }

        [Test]
        public void TokenMode_ProviderFailure_IsReported()
        {
            var provider = new CountingProvider
            {
                Respond = n => SkillprintTokenResult.Failure("backend says no"),
            };
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(provider);

            var result = Ensure(auth);
            Assert.IsFalse(result.ok);
            Assert.AreEqual("backend says no", result.error);
            Assert.IsNull(AuthorizationHeader(auth));
        }

        [Test]
        public void TokenMode_ProviderThrowing_IsReported()
        {
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(new DelegateThrowingProvider());

            var result = Ensure(auth);
            Assert.IsFalse(result.ok);
            StringAssert.Contains("boom", result.error);
        }

        private sealed class DelegateThrowingProvider : ISkillprintTokenProvider
        {
            public void FetchToken(string playerId, Action<SkillprintTokenResult> onComplete)
            {
                throw new InvalidOperationException("boom");
            }
        }

        [Test]
        public void TokenMode_SwitchingPlayer_DropsTheOtherPlayersToken()
        {
            var provider = new CountingProvider
            {
                Respond = n => SkillprintTokenResult.FromExpiresIn("spt_" + n, 3600),
            };
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);
            auth.SetProvider(provider);

            auth.SetPlayer("player-1");
            Ensure(auth);
            auth.SetPlayer("player-2");
            Ensure(auth);

            Assert.AreEqual(2, provider.Calls);
            Assert.AreEqual("player-2", provider.LastPlayerId);
        }

        [Test]
        public void TokenMode_PushedTokenWithoutProvider_IsUsedUntilExpiry()
        {
            var auth = new SkillprintAuth(SkillprintAuthMode.TokenProvider, null, null);

            auth.SetToken("spt_pushed", DateTime.UtcNow.AddSeconds(30));
            Assert.IsTrue(Ensure(auth).ok, "Inside the refresh window but unexpired: still usable.");

            auth.SetToken("spt_pushed", DateTime.UtcNow.AddSeconds(-1));
            Assert.IsFalse(Ensure(auth).ok, "Expired and nothing to refresh it with.");
        }

        [Test]
        public void TokenResult_EmptyToken_IsFailure()
        {
            Assert.IsFalse(SkillprintTokenResult.FromToken("").Success);
            Assert.IsFalse(SkillprintTokenResult.FromToken(null).Success);
        }

        // ───────────── Config ─────────────────

        [Test]
        public void Config_DefaultsToApiKeyMode_ForExistingProjects()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SkillprintConfig>();
            Assert.AreEqual(SkillprintAuthMode.PartnerApiKey, config.authMode);
            Assert.IsTrue(config.RequiresPartnerApiKey);
            config.authMode = SkillprintAuthMode.TokenProvider;
            Assert.IsFalse(config.RequiresPartnerApiKey);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}

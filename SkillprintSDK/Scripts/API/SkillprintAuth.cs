using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Skillprint.SDK
{
    /// <summary>
    /// How the SDK authenticates its requests to Skillprint.
    /// </summary>
    public enum SkillprintAuthMode
    {
        /// <summary>
        /// The partner API key from <see cref="SkillprintConfig"/> is sent on every request.
        /// Anyone who extracts it from a build gets your organization's full API access,
        /// so use this only for development and internal builds.
        /// </summary>
        PartnerApiKey = 0,

        /// <summary>
        /// No API key ships in the build. Your backend exchanges its API key for a
        /// short-lived SDK token (POST /partners/api/sdk/token/) and the game supplies it
        /// through <see cref="ISkillprintTokenProvider"/> or
        /// <see cref="SkillprintManager.SetSdkToken"/>. Recommended for shipped games.
        /// </summary>
        TokenProvider = 1,
    }

    /// <summary>
    /// Outcome of a token fetch. Build with <see cref="FromToken"/>,
    /// <see cref="FromExpiresIn"/> or <see cref="Failure"/>.
    /// </summary>
    public sealed class SkillprintTokenResult
    {
        public bool Success { get; private set; }
        public string Token { get; private set; }
        public DateTime? ExpiresAtUtc { get; private set; }
        public string Error { get; private set; }

        /// <param name="token">The SDK token (starts with "spt_").</param>
        /// <param name="expiresAtUtc">When it expires; null if unknown (the SDK then refreshes only on a 401).</param>
        public static SkillprintTokenResult FromToken(string token, DateTime? expiresAtUtc = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Failure("Token provider returned an empty token.");
            }
            return new SkillprintTokenResult
            {
                Success = true,
                Token = token,
                ExpiresAtUtc = expiresAtUtc.HasValue ? expiresAtUtc.Value.ToUniversalTime() : (DateTime?)null,
            };
        }

        /// <param name="token">The SDK token.</param>
        /// <param name="expiresInSeconds">Seconds until expiry, as returned by the mint endpoint's "expiresIn".</param>
        public static SkillprintTokenResult FromExpiresIn(string token, double expiresInSeconds)
        {
            return FromToken(
                token,
                expiresInSeconds > 0 ? DateTime.UtcNow.AddSeconds(expiresInSeconds) : (DateTime?)null
            );
        }

        public static SkillprintTokenResult Failure(string error)
        {
            return new SkillprintTokenResult
            {
                Success = false,
                Error = string.IsNullOrEmpty(error) ? "Token fetch failed." : error,
            };
        }
    }

    /// <summary>
    /// Supplies SDK tokens from your backend. The SDK calls <see cref="FetchToken"/> before
    /// its first request, shortly before the current token expires, and once after a 401.
    /// </summary>
    public interface ISkillprintTokenProvider
    {
        /// <summary>
        /// Fetch a fresh SDK token. Call <paramref name="onComplete"/> exactly once, from any thread.
        /// </summary>
        /// <param name="playerId">
        /// The player id the game passed to the SDK (may be null). Treat it as a hint only:
        /// your backend must decide which player to mint for from its own authenticated
        /// session, never from a value the client sends.
        /// </param>
        void FetchToken(string playerId, Action<SkillprintTokenResult> onComplete);
    }

    /// <summary>
    /// A ready-made <see cref="ISkillprintTokenProvider"/> that POSTs {"playerId": ...} to an
    /// endpoint on your backend and expects the Skillprint mint response back
    /// ({"token": "...", "expiresIn": 3600} — passing Skillprint's response through as-is works).
    /// Use <c>configureRequest</c> to attach your own player auth (cookie, bearer, etc.).
    /// </summary>
    public sealed class HttpTokenProvider : ISkillprintTokenProvider
    {
        private readonly string _endpointUrl;
        private readonly Action<UnityWebRequest> _configureRequest;
        private readonly MonoBehaviour _runner;
        private readonly int _timeoutSeconds;

        /// <param name="runner">A MonoBehaviour to run the request coroutine on (e.g. SkillprintManager.Instance).</param>
        /// <param name="endpointUrl">Your backend's token endpoint.</param>
        /// <param name="configureRequest">Optional: add headers such as your own session token.</param>
        public HttpTokenProvider(
            MonoBehaviour runner,
            string endpointUrl,
            Action<UnityWebRequest> configureRequest = null,
            int timeoutSeconds = 10
        )
        {
            _runner = runner;
            _endpointUrl = endpointUrl;
            _configureRequest = configureRequest;
            _timeoutSeconds = timeoutSeconds;
        }

        public void FetchToken(string playerId, Action<SkillprintTokenResult> onComplete)
        {
            if (_runner == null || !_runner.isActiveAndEnabled)
            {
                onComplete(SkillprintTokenResult.Failure("HttpTokenProvider has no active runner."));
                return;
            }
            _runner.StartCoroutine(Fetch(playerId, onComplete));
        }

        private IEnumerator Fetch(string playerId, Action<SkillprintTokenResult> onComplete)
        {
            string body = JsonUtility.ToJson(new TokenRequest { playerId = playerId ?? "" });
            using (UnityWebRequest request = new UnityWebRequest(_endpointUrl, "POST"))
            {
                request.timeout = _timeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");
                _configureRequest?.Invoke(request);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onComplete(SkillprintTokenResult.Failure(
                        $"Token endpoint error: {request.error} ({request.responseCode})"));
                    yield break;
                }

                TokenResponse parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    onComplete(SkillprintTokenResult.Failure($"Could not parse token response: {e.Message}"));
                    yield break;
                }

                if (parsed == null || string.IsNullOrEmpty(parsed.token))
                {
                    onComplete(SkillprintTokenResult.Failure("Token response had no 'token'."));
                    yield break;
                }

                double expiresIn = parsed.expiresIn > 0 ? parsed.expiresIn : parsed.expires_in;
                if (expiresIn > 0)
                {
                    onComplete(SkillprintTokenResult.FromExpiresIn(parsed.token, expiresIn));
                }
                else
                {
                    DateTime expiresAt = default(DateTime);
                    string raw = !string.IsNullOrEmpty(parsed.expiresAt) ? parsed.expiresAt : parsed.expires_at;
                    bool hasExpiry = !string.IsNullOrEmpty(raw) && DateTime.TryParse(
                        raw, null, System.Globalization.DateTimeStyles.AdjustToUniversal
                            | System.Globalization.DateTimeStyles.AssumeUniversal, out expiresAt);
                    onComplete(SkillprintTokenResult.FromToken(
                        parsed.token, hasExpiry ? expiresAt : (DateTime?)null));
                }
            }
        }

        [Serializable]
        private class TokenRequest
        {
            public string playerId;
        }

        [Serializable]
        private class TokenResponse
        {
            public string token;
            public double expiresIn;
            public double expires_in;
            public string expiresAt;
            public string expires_at;
        }
    }

    /// <summary>Wraps a delegate as an <see cref="ISkillprintTokenProvider"/>.</summary>
    internal sealed class DelegateTokenProvider : ISkillprintTokenProvider
    {
        private readonly Action<string, Action<SkillprintTokenResult>> _fetch;

        public DelegateTokenProvider(Action<string, Action<SkillprintTokenResult>> fetch)
        {
            _fetch = fetch;
        }

        public void FetchToken(string playerId, Action<SkillprintTokenResult> onComplete)
        {
            _fetch(playerId, onComplete);
        }
    }
}

namespace Skillprint.SDK.API
{
    /// <summary>
    /// Holds the SDK's credential and applies it to requests: the partner API key in
    /// <see cref="SkillprintAuthMode.PartnerApiKey"/> mode, or a cached SDK token that it
    /// fetches and refreshes through the token provider in
    /// <see cref="SkillprintAuthMode.TokenProvider"/> mode.
    /// </summary>
    public sealed class SkillprintAuth
    {
        /// <summary>Refresh this long before the reported expiry.</summary>
        public const double RefreshSkewSeconds = 60;
        /// <summary>Give up on a provider that never calls back.</summary>
        public const float FetchTimeoutSeconds = 20f;

        private readonly string _partnerApiKey;
        private readonly Action<string, SkillprintManager.LogLevel> _logger;
        private ISkillprintTokenProvider _provider;

        private string _token;
        private DateTime? _expiresAtUtc;
        private string _tokenPlayerId;
        private string _currentPlayerId;
        private bool _fetching;
        private string _lastError;

        public SkillprintAuthMode Mode { get; private set; }
        public bool UsesToken => Mode == SkillprintAuthMode.TokenProvider;
        public bool HasProvider => _provider != null;
        public string LastError => _lastError;

        public SkillprintAuth(
            SkillprintAuthMode mode,
            string partnerApiKey,
            Action<string, SkillprintManager.LogLevel> logger
        )
        {
            Mode = mode;
            _partnerApiKey = partnerApiKey;
            _logger = logger;
        }

        public void SetProvider(ISkillprintTokenProvider provider)
        {
            _provider = provider;
        }

        /// <summary>Push a token obtained elsewhere (e.g. passed into a WebGL page).</summary>
        public void SetToken(string token, DateTime? expiresAtUtc, string playerId = null)
        {
            _token = token;
            _expiresAtUtc = expiresAtUtc.HasValue ? expiresAtUtc.Value.ToUniversalTime() : (DateTime?)null;
            _tokenPlayerId = playerId;
            _lastError = null;
        }

        /// <summary>
        /// Tell the auth which player the next requests are for. A cached token minted for a
        /// different (known) player is dropped so the provider is asked again.
        /// </summary>
        public void SetPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }
            if (!string.IsNullOrEmpty(_tokenPlayerId) && _tokenPlayerId != playerId && _provider != null)
            {
                Invalidate();
            }
            _currentPlayerId = playerId;
        }

        public void Invalidate()
        {
            _token = null;
            _expiresAtUtc = null;
            _tokenPlayerId = null;
        }

        private bool HasUnexpiredToken =>
            !string.IsNullOrEmpty(_token)
            && (!_expiresAtUtc.HasValue || _expiresAtUtc.Value > DateTime.UtcNow);

        private bool HasFreshToken =>
            !string.IsNullOrEmpty(_token)
            && (!_expiresAtUtc.HasValue
                || (_expiresAtUtc.Value - DateTime.UtcNow).TotalSeconds > RefreshSkewSeconds);

        /// <summary>
        /// Make sure a usable credential is available. In token mode this fetches or refreshes
        /// through the provider (one fetch at a time; concurrent callers wait for it).
        /// </summary>
        public IEnumerator EnsureReady(bool forceRefresh, Action<bool, string> done)
        {
            if (!UsesToken)
            {
                done(true, null);
                yield break;
            }

            if (!forceRefresh && HasFreshToken)
            {
                done(true, null);
                yield break;
            }

            if (_provider == null)
            {
                // A pushed token with no provider is used until it expires.
                if (!forceRefresh && HasUnexpiredToken)
                {
                    done(true, null);
                    yield break;
                }
                _lastError =
                    "No SDK token available. Call SkillprintManager.SetTokenProvider(...) or SetSdkToken(...) "
                    + "before starting a session (AuthMode is TokenProvider).";
                done(false, _lastError);
                yield break;
            }

            if (_fetching)
            {
                float waited = 0f;
                while (_fetching && waited < FetchTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                bool ok = HasUnexpiredToken;
                done(ok, ok ? null : (_lastError ?? "Timed out waiting for the SDK token."));
                yield break;
            }

            _fetching = true;
            var pending = new PendingFetch();
            string requestedFor = _currentPlayerId;
            try
            {
                _provider.FetchToken(requestedFor, result =>
                {
                    if (pending.Completed)
                    {
                        return;
                    }
                    pending.Result = result;
                    pending.Completed = true;
                });
            }
            catch (Exception e)
            {
                pending.Result = SkillprintTokenResult.Failure("Token provider threw: " + e.Message);
                pending.Completed = true;
            }

            float elapsed = 0f;
            while (!pending.Completed && elapsed < FetchTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _fetching = false;

            if (!pending.Completed)
            {
                pending.Completed = true; // ignore a late callback
                _lastError = $"Token provider did not respond within {FetchTimeoutSeconds}s.";
                _logger?.Invoke(_lastError, SkillprintManager.LogLevel.Error);
                done(false, _lastError);
                yield break;
            }

            SkillprintTokenResult fetched = pending.Result;
            if (fetched != null && fetched.Success)
            {
                SetToken(fetched.Token, fetched.ExpiresAtUtc, requestedFor);
                _logger?.Invoke(
                    "SDK token obtained"
                        + (fetched.ExpiresAtUtc.HasValue ? $" (expires {fetched.ExpiresAtUtc.Value:u})." : "."),
                    SkillprintManager.LogLevel.Info
                );
                done(true, null);
            }
            else
            {
                _lastError = fetched != null ? fetched.Error : "Token provider returned no result.";
                _logger?.Invoke("SDK token fetch failed: " + _lastError, SkillprintManager.LogLevel.Error);
                done(false, _lastError);
            }
        }

        /// <summary>Set the Authorization header for the current credential.</summary>
        public void Apply(UnityWebRequest request)
        {
            if (UsesToken)
            {
                if (!string.IsNullOrEmpty(_token))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + _token);
                }
            }
            else if (!string.IsNullOrEmpty(_partnerApiKey))
            {
                request.SetRequestHeader("Authorization", "Api-Key " + _partnerApiKey);
            }
        }

        private sealed class PendingFetch
        {
            public volatile bool Completed;
            public SkillprintTokenResult Result;
        }
    }
}

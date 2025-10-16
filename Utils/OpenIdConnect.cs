using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace MyHome.Utils
{
    public static class OpenIdConnect
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly Random random = new();
        private static readonly Lock _lock = new();
        private static Dictionary<string, object> _metadata = null;
        private static List<Dictionary<string, object>> _jwks = null;


        public static bool HandleAuth(HttpContext context, string oidcAddress, string clientId)
        {
            if (string.IsNullOrEmpty(oidcAddress))
                return false;

            var metadata = GetMetadata(oidcAddress);
            if (metadata == null || !metadata.TryGetValue("authorization_endpoint", out var authEndpoint) ||
                string.IsNullOrEmpty((string)authEndpoint))
            {
                return false;
            }

            try
            {
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["response_type"] = "code";
                query["client_id"] = clientId;
                query["scope"] = "openid profile email";
                query["redirect_uri"] = GetRedirectUrl(context);
                query["state"] = GetRandomBase64String();
                query["nonce"] = GetRandomBase64String();
                var builder = new UriBuilder((string)authEndpoint)
                {
                    Query = query.ToString()
                };

                var url = builder.ToString();

                logger.Debug($"Redirect to OpenIdConnect authentication url: {url}");
                context.Session.SetString("state", query["state"]);
                context.Session.SetString("nonce", query["nonce"]);
                context.Response.Redirect(url);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to handle OpenIdConnect auth");
                logger.Debug(ex);
                return false;
            }
        }

        public static bool HandleCallback(HttpContext context, string oidcAddress, string clientId, string clientSecret)
        {
            try
            {
                var code = context.Request.Query["code"];
                var state = context.Request.Query["state"];
                var error = context.Request.Query["error"];
                var errorDesc = context.Request.Query["errorDesc"];

                if (!string.IsNullOrEmpty(error))
                {
                    logger.Error($"OpenIdConnect callback error: {error} - {errorDesc}");
                    return false;
                }

                if (context.Session.GetString("state") != state)
                {
                    logger.Error("Invalid OpenIdConnect state parameter");
                    return false;
                }

                var metadata = GetMetadata(oidcAddress);
                if (metadata == null || !metadata.TryGetValue("token_endpoint", out var tokenEndpoint) ||
                    string.IsNullOrEmpty((string)tokenEndpoint))
                {
                    return false;
                }

                using var client = Utils.GetHttpClient(skipCertVerification: true);
                var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);

                var parameters = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", GetRedirectUrl(context) },
                };
                var content = new FormUrlEncodedContent(parameters);
                var result = client.PostAsync((string)tokenEndpoint, content).Result;
                var json = JToken.Parse(result.Content.ReadAsStringAsync().Result);

                context.Session.Remove("state");
                context.Session.Remove("nonce");

                var idToken = json["id_token"].ToString();
                context.Session.SetString("id_token", idToken);
                var accessToken = json["access_token"].ToString();
                context.Session.SetString("access_token", accessToken);
                var refreshToken = json["refresh_token"].ToString();
                context.Session.SetString("refresh_token", refreshToken);

                var jwks = GetJwks((string)metadata["jwks_uri"]);
                var publicKey = CreateRsaPublicKey((string)jwks[0]["n"], (string)jwks[0]["e"]);
                if (!ValidateJwt(idToken, publicKey, clientId))
                    return false;

                var userInfo = GetUserInfo(oidcAddress, accessToken);
                logger.Info($"OpenIdConnect login successful for user: {userInfo["sub"]} ({userInfo["name"]})");
                context.Session.SetString("user_id", userInfo["sub"]);
                context.Session.SetString("user_name", userInfo["name"]);
                context.Session.SetString("user_email", userInfo["email"]);
                context.Session.SetString("time", DateTime.Now.ToString());
            }
            catch (Exception ex)
            {

                logger.Error($"Failed to handle OpenIdConnect callback");
                logger.Debug(ex);
                return false;
            }

            return true;
        }


        public static Dictionary<string, string> GetUserInfo(string address, string token)
        {
            var url = $"{address}/userinfo";
            try
            {
                logger.Debug($"Get OpenIdConnect userinfo from '{url}'");
                using var client = Utils.GetHttpClient(skipCertVerification: true);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = client.GetStringAsync(url).Result;
                var userInfo = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                return userInfo;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to get OpenIdConnect userinfo from '{url}'");
                logger.Debug(ex);
                return null;
            }
        }

        public static Dictionary<string, object> GetMetadata(string address)
        {
            lock (_lock)
            {
                if (_metadata != null)
                    return _metadata;

                var url = $"{address}/.well-known/openid-configuration";
                try
                {
                    logger.Debug($"Get OpenIdConnect metadata from '{url}'");
                    using var client = Utils.GetHttpClient(skipCertVerification: true);
                    var response = client.GetStringAsync(url).Result;
                    _metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                    return _metadata;
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to get OpenIdConnect metadata from '{url}'");
                    logger.Debug(ex);
                    _metadata = new Dictionary<string, object>();
                    return _metadata;
                }
            }
        }

        public static List<Dictionary<string, object>> GetJwks(string url)
        {
            lock (_lock)
            {
                if (_jwks != null)
                    return _jwks;

                try
                {
                    logger.Debug($"Get OpenIdConnect jwks from '{url}'");
                    using var client = Utils.GetHttpClient(skipCertVerification: true);
                    var response = client.GetStringAsync(url).Result;
                    var json = JObject.Parse(response);
                    _jwks = json["keys"].ToObject<List<Dictionary<string, object>>>();
                    return _jwks;
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to get OpenIdConnect jwks from '{url}'");
                    logger.Debug(ex);
                    return null;
                }
            }
        }


        public static bool IsAuthenticated(HttpContext context, string oidcAddress, string clientId)
        {
            if (string.IsNullOrEmpty(oidcAddress))
                return false;

            try
            {
                var metadata = GetMetadata(oidcAddress);
                if (metadata == null)
                    return false;

                var jwks = GetJwks((string)metadata["jwks_uri"]);
                var publicKey = CreateRsaPublicKey((string)jwks[0]["n"], (string)jwks[0]["e"]);
                return ValidateJwt(context.Session.GetString("id_token"), publicKey, clientId);
            }
            catch
            {
                return false;
            }
        }


        private static string GetRedirectUrl(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("X-Forwarded-Url", out var forwardedUrl) && forwardedUrl.Count > 0)
            {
                var idx = forwardedUrl[0].LastIndexOf(context.Request.Path);
                if (idx > 0)
                    return $"{forwardedUrl[0][..idx]}/api/oauth2/callback";
            }
            return $"{context.Request.Scheme}://{context.Request.Host}/api/oauth2/callback"; ;
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var output = input;
            output = output.Replace('-', '+'); // 62nd char of encoding
            output = output.Replace('_', '/'); // 63rd char of encoding
            switch (output.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: output += "=="; break; // Two pad chars
                case 3: output += "="; break; // One pad char
                default: throw new FormatException("Illegal base64url string!");
            }
            var converted = Convert.FromBase64String(output); // Standard base64 decode
            return converted;
        }

        private static RSA CreateRsaPublicKey(string n, string e)
        {
            var modulusBytes = Base64UrlDecode(n);
            var exponentBytes = Base64UrlDecode(e);

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = modulusBytes,
                Exponent = exponentBytes
            });
            return rsa;
        }

        private static bool ValidateJwt(string jwt, RSA publicKey, string clientId)
        {
            if (string.IsNullOrEmpty(jwt))
                return false;

            var token = JwtBuilder.Create()
                        .WithAlgorithm(new RS256Algorithm(publicKey))
                        .WithSecret(publicKey.ExportRSAPublicKey())
                        .MustVerifySignature()
                        .Decode(jwt);

            var json = JObject.Parse(token);

            if (json["aud"].ToString() != clientId)
                return false;

            long expTime = (long)json["exp"];
            DateTimeOffset expiration = DateTimeOffset.FromUnixTimeSeconds(expTime);
            if (expiration <= DateTimeOffset.UtcNow)
                return false;

            return true;
        }

        private static string GetRandomBase64String()
        {
            return Convert.ToBase64String(BitConverter.GetBytes(long.Parse(random.NextDouble().ToString()[2..])))[5..];
        }
    }
}

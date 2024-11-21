using System.Net.Http.Headers;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MauiApp1;

public partial class MainPage : ContentPage
{
    private string? _currentAccessToken;
    public MainPage()
    {
        InitializeComponent();
    }
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        try
        {
            editor.Text = "Login Clicked";

            _currentAccessToken = await GetTokenByWebAuthenticator();
            editor.Text = "Loggin successful";
        }
        catch (Exception ex)
        {
            editor.Text = "Error: " + ex.Message;
        }
    }
    private async Task<string> GetTokenByWebAuthenticator()
    {
        try
        {
            // Step 1: Generate an auth url
            var codeMethod = "S256";
            var pkce = Pkce.Generate();
            var codeVerifier = pkce.verifier;
            var shaVerifier = pkce.code_challenge;
            var state = Guid.NewGuid().ToString();
            var authorizationEndpoint = "http://192.168.86.243:8080/realms/XYZCompanyRealm/protocol/openid-connect/auth";
            var tokenEndpoint = "http://192.168.86.243:8080/realms/XYZCompanyRealm/protocol/openid-connect/token";
            var clientId = "application-abc";
            var redirectUri = "myapp://callback";
            var authUrl = $"{authorizationEndpoint}?client_id={clientId}&response_type=code&redirect_uri={redirectUri}" +
                $"&scope=openid&state={state}&code_challenge_method={codeMethod}&code_challenge={shaVerifier}";
            // Step 2: Redirect to Keycloak for Authentication and waiting for callback for code
            WebAuthenticatorResult authResult = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions()
                {
                    Url = new Uri(authUrl),
                    CallbackUrl = new Uri(redirectUri),
                    //PrefersEphemeralWebBrowserSession = true
                });
            string? code = null;
            authResult?.Properties.TryGetValue("code", out code);
            // Step 3: Exchange Authorization Code for Access Token
            var token = await ExchangeCodeForTokenAsync(tokenEndpoint, clientId, redirectUri, code, codeVerifier);
            return token;
        }
        catch
        {
            // Use stopped auth
            throw;
        }
    }
    private async Task<string> ExchangeCodeForTokenAsync(string tokenEndpoint, string clientId, string redirectUri, string? code, string codeVerifier)
    {
        using (var client = new HttpClient())
        {
            var requestData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("code_verifier", codeVerifier)
            };
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(requestData)
            };
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadAsStringAsync();
                var json = JsonSerializer.Deserialize<JsonNode>(tokenResponse);
                return json["access_token"].ToString(); // Return the access token
            }
            else
            {
                throw new Exception($"Failed to retrieve token. Status code: {response.StatusCode}");
            }
        }
    }

    private async void OnApiClicked(object sender, EventArgs e)
    {
        try
        {
            editor.Text = "API Clicked";
            if (_currentAccessToken != null)
            {
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        return true;
                    };
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentAccessToken);
                    var response = await client.GetAsync("https://192.168.86.243:7274/WeatherForecast");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var doc = JsonDocument.Parse(content).RootElement;
                        editor.Text = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    }
                    else
                    {
                        editor.Text = response.ReasonPhrase;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            editor.Text = ex.Message;
        }
    }
    public static class Pkce
    {
        /// <summary>
        /// Generates a code_verifier and the corresponding code_challenge, as specified in the rfc-7636.
        /// </summary>
        /// <remarks>See https://datatracker.ietf.org/doc/html/rfc7636#section-4.1 and https://datatracker.ietf.org/doc/html/rfc7636#section-4.2</remarks>
        public static (string code_challenge, string verifier) Generate(int size = 32)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[size];
            rng.GetBytes(randomBytes);
            var verifier = Base64UrlEncode(randomBytes);

            var buffer = Encoding.UTF8.GetBytes(verifier);
            var hash = SHA256.Create().ComputeHash(buffer);
            var challenge = Base64UrlEncode(hash);

            return (challenge, verifier);
        }

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
    }
}


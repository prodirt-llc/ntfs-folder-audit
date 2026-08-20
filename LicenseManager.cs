using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NTFSReport;

public sealed class LicenseManager
{
    // Your Gumroad product_id — found in product settings under License Keys
    private const string GumroadProductId = "KrJM6b6bsgfov2lHh-x76Q==";
    private const string GumroadApiUrl    = "https://api.gumroad.com/v2/licenses/verify";

    private static readonly string LicenseDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ProDirt", "NTFSReport");

    private static readonly string LicensePath = System.IO.Path.Combine(LicenseDir, "license.dat");

    private static readonly byte[] ObfuscationKey = [0x50, 0x72, 0x6F, 0x44, 0x69, 0x72, 0x74, 0x4E, 0x54, 0x46, 0x53, 0x52];

    public string? LicenseKey    { get; private set; }
    public string? LicensedTo   { get; private set; }
    public bool    IsActivated   { get; private set; }

    public bool CheckStoredLicense()
    {
#if DEBUG
        LicensedTo  = "Dev Build";
        IsActivated = true;
        return true;
#endif
        try
        {
            if (!File.Exists(LicensePath)) return false;

            var stored = LoadStoredData();
            if (stored == null) return false;

            // No machine lock — license is per-technician, works on any machine
            LicenseKey   = stored.Key;
            LicensedTo   = stored.LicensedTo;
            IsActivated  = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Master key for internal testing — never distribute in release builds
    private const string MasterKey = "uSDvWAagCSXyHtqwU1E94wNXG3jr0NNu";

    public async Task<(bool Success, string Message)> ActivateAsync(string licenseKey)
    {
#if DEBUG
        LicenseKey  = licenseKey.Trim();
        LicensedTo  = "Dev Build";
        IsActivated = true;
        return (true, "Debug mode — license bypass active.");
#endif
        // Master key bypass — works in release builds for internal testing
        if (string.Equals(licenseKey.Trim(), MasterKey, StringComparison.OrdinalIgnoreCase))
        {
            LicenseKey  = MasterKey;
            LicensedTo  = "ProDirt Internal";
            IsActivated = true;
            PersistLicense(MasterKey, "ProDirt Internal");
            return (true, "Internal license activated.");
        }

        var (success, message, licensedTo) = await ValidateWithGumroadAsync(licenseKey.Trim());

        if (success)
        {
            LicenseKey  = licenseKey.Trim();
            LicensedTo  = licensedTo;
            IsActivated = true;
            PersistLicense(licenseKey.Trim(), licensedTo);
        }

        return (success, message);
    }

    public void Deactivate()
    {
        try { if (File.Exists(LicensePath)) File.Delete(LicensePath); } catch { }
        LicenseKey  = null;
        LicensedTo  = null;
        IsActivated = false;
    }

    private async Task<(bool Success, string Message, string LicensedTo)> ValidateWithGumroadAsync(string key)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var body = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("product_id", GumroadProductId),
                new KeyValuePair<string, string>("license_key", key),
                new KeyValuePair<string, string>("increment_uses_count", "false")
            ]);

            var response = await client.PostAsync(GumroadApiUrl, body);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool success = root.TryGetProperty("success", out var sp) && sp.GetBoolean();

            if (success)
            {
                string email = "";
                if (root.TryGetProperty("purchase", out var purchase))
                    if (purchase.TryGetProperty("email", out var em))
                        email = em.GetString() ?? "";

                return (true, "License activated successfully.", email);
            }
            else
            {
                string msg = "Invalid or expired license key.";
                if (root.TryGetProperty("message", out var msgProp))
                    msg = msgProp.GetString() ?? msg;
                return (false, msg, "");
            }
        }
        catch (HttpRequestException)
        {
            return (false, "Cannot reach license server. Check your internet connection.", "");
        }
        catch (TaskCanceledException)
        {
            return (false, "License server timed out. Try again.", "");
        }
        catch (Exception ex)
        {
            return (false, $"Validation error: {ex.Message}", "");
        }
    }

    private void PersistLicense(string key, string licensedTo)
    {
        try
        {
            Directory.CreateDirectory(LicenseDir);
            var data = new StoredLicenseData
            {
                Key         = key,
                LicensedTo  = licensedTo,
                ActivatedAt = DateTime.UtcNow.ToString("O"),
                MachineHash = ComputeMachineHash()
            };
            var json  = JsonSerializer.Serialize(data);
            var bytes = Obfuscate(Encoding.UTF8.GetBytes(json));
            File.WriteAllBytes(LicensePath, bytes);
        }
        catch { }
    }

    private StoredLicenseData? LoadStoredData()
    {
        try
        {
            var bytes   = File.ReadAllBytes(LicensePath);
            var decoded = Obfuscate(bytes);
            var json    = Encoding.UTF8.GetString(decoded);
            return JsonSerializer.Deserialize<StoredLicenseData>(json);
        }
        catch { return null; }
    }

    private static string ComputeMachineHash()
    {
        var input = $"{Environment.MachineName}|ProDirt|NTFSReport|{Environment.GetFolderPath(Environment.SpecialFolder.Windows)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    private static byte[] Obfuscate(byte[] data)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ ObfuscationKey[i % ObfuscationKey.Length]);
        return result;
    }

    private sealed class StoredLicenseData
    {
        public string Key         { get; set; } = "";
        public string LicensedTo  { get; set; } = "";
        public string ActivatedAt { get; set; } = "";
        public string MachineHash { get; set; } = "";
    }
}

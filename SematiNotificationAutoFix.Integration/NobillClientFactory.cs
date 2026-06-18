using Microsoft.Extensions.Configuration;
using NobillCalls;
using System.ServiceModel;
using static NobillCalls.NobillServiceClient;

namespace LP.PluginHost.Integration;

public class NobillClientFactory 
{
    private const EndpointConfiguration _endpointConfiguration = EndpointConfiguration.BasicHttpBinding_INobillService;
    private readonly string _username;
    private readonly string _password;
    private readonly string _baseUrl;

    public NobillClientFactory(IConfiguration configuration)
    {
        var nobillConfig = configuration.GetSection("Nobill");
        _baseUrl = nobillConfig["URL"] ?? string.Empty;
        _username = nobillConfig["Username"] ?? string.Empty;
        _password = nobillConfig["Password"] ?? string.Empty;
    }

    public NobillServiceClient CreateClient()
    {
        var binding = CreateOptimizedBinding();

        NobillServiceClient client = string.IsNullOrWhiteSpace(_baseUrl)
            ? new NobillServiceClient(_endpointConfiguration)
            : new NobillServiceClient(binding, new EndpointAddress(_baseUrl));

        if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
        {
            client.ClientCredentials.UserName.UserName = _username;
            client.ClientCredentials.UserName.Password = _password;
        }
        return client;
    }

    public static BasicHttpBinding CreateOptimizedBinding()
    {
        var binding = new BasicHttpBinding();
        // Security configuration
        binding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
        // Timeout configurations
        binding.OpenTimeout = TimeSpan.FromSeconds(30);
        binding.CloseTimeout = TimeSpan.FromSeconds(30);
        binding.SendTimeout = TimeSpan.FromSeconds(120);
        binding.ReceiveTimeout = TimeSpan.FromSeconds(120);
        // Message size limits
        binding.MaxReceivedMessageSize = 65536;
        binding.MaxBufferSize = 65536;
        // Reader quotas
        binding.ReaderQuotas.MaxDepth = 32;
        binding.ReaderQuotas.MaxStringContentLength = 8192;
        binding.ReaderQuotas.MaxArrayLength = 16384;
        binding.ReaderQuotas.MaxBytesPerRead = 4096;
        binding.ReaderQuotas.MaxNameTableCharCount = 16384;
        // Transfer mode
        binding.TransferMode = TransferMode.Buffered;
        binding.MessageEncoding = WSMessageEncoding.Text;
        return binding;
    }

}
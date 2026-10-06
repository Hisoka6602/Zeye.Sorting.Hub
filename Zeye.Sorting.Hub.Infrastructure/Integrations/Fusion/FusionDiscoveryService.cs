using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>有界、签名认证的独立 UDP 发现，不在 UDP 上传输业务内容或凭据。</summary>
public sealed class FusionDiscoveryService(IOptions<FusionIngestionOptions> options, IFusionRuntimeConfiguration? runtime = null) : IFusionDiscoveryService {
    /// <summary>设备发现诊断日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>单来源分钟限流状态，键集合限定为登记来源。</summary>
    private readonly Dictionary<string, (DateTime Start, int Count)> _rates = new(StringComparer.Ordinal);

    /// <summary>按协议八字段及换行规则计算签名，禁止隐式加入尾部换行。</summary>
    public static string Sign(FusionDiscoveryPacket packet, string key) {
        var text = string.Join('\n', packet.Type, packet.ProtocolVersion, packet.SourceInstanceId, packet.HubId,
            packet.Nonce, packet.SentAtUnixMs.ToString(CultureInfo.InvariantCulture),
            packet.ExpiresAtUnixMs.ToString(CultureInfo.InvariantCulture), packet.Endpoint);
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>验证地址安全性；设备发现需要可达的专用接收路径。</summary>
    public static bool IsEndpoint(string endpoint, bool insecure) => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || insecure && uri.Scheme == "http") && uri.AbsolutePath == FusionProtocol.HubPath
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && !endpoint.Any(char.IsControl) && uri.Host is not ("0.0.0.0" or "[::]" or "::");

    /// <summary>校验协议、来源登记、过期窗口及 HMAC，未知来源静默丢弃。</summary>
    public FusionDiscoveryPacket? Offer(ReadOnlySpan<byte> bytes, IPAddress sender) {
        var settings = runtime?.Snapshot.Options ?? options.Value;
        if (!settings.IsEnabled || bytes.Length is 0 or > 4096 || !IsEndpoint(settings.AdvertisedEndpoint, settings.AllowInsecureHttp)) return null;
        FusionDiscoveryPacket? query;
        try { query = JsonSerializer.Deserialize<FusionDiscoveryPacket>(bytes, FusionProtocol.Json); }
        catch (JsonException exception) { Logger.Debug(exception, "丢弃无效 Fusion 发现 JSON。"); return null; }
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (query is not { Protocol: "zeye.fusion-hub.discovery", Type: "discover", ProtocolVersion: "1.0", Endpoint: "" }
            || query.HubId != settings.HubId || !FusionProtocol.IsIdentity(query.SourceInstanceId)
            || !FusionProtocol.IsHex(query.Nonce, 32) || !FusionProtocol.IsHex(query.Signature, 64)
            || query.SentAtUnixMs > now + 1000 || query.SentAtUnixMs < now - 10000 || query.ExpiresAtUnixMs <= now
            || query.ExpiresAtUnixMs <= query.SentAtUnixMs || query.ExpiresAtUnixMs - query.SentAtUnixMs > 10000) return null;
        var source = settings.Sources.SingleOrDefault(x => x.SourceInstanceId == query.SourceInstanceId);
        if (source is null || !source.Enabled || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(query.Signature),
            Convert.FromHexString(Sign(query, source.MachineApiKey)))) return null;
        var endpoint = new Uri(settings.AdvertisedEndpoint);
        if (!IPAddress.IsLoopback(sender) && (endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(endpoint.Host, out var host) && IPAddress.IsLoopback(host))) return null;
        var offer = query with { Type = "offer", Endpoint = settings.AdvertisedEndpoint, Signature = "" };
        return offer with { Signature = Sign(offer, source.MachineApiKey) };
    }

    /// <summary>监听接收循环串行且全局限流，取消立即释放端口；不占用分拣机端口。</summary>
    public async Task ListenAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var settings = runtime?.Snapshot.Options ?? options.Value;
            if (!settings.IsEnabled || !settings.DiscoveryEnabled) { await Task.Delay(1000, cancellationToken); continue; }
            if (settings.DiscoveryPort is < 1024 or > 65535 or 5089 || !IsEndpoint(settings.AdvertisedEndpoint, settings.AllowInsecureHttp))
                throw new ArgumentException("InvalidFusionDiscoveryConfiguration");
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, settings.DiscoveryPort));
            udp.Client.ReceiveBufferSize = 8192;
            Logger.Info("Fusion 认证发现启动，Port={Port}", settings.DiscoveryPort);
            _rates.Clear();
            var window = DateTime.Now; var packets = 0;
            while (!cancellationToken.IsCancellationRequested) {
                var current = runtime?.Snapshot.Options ?? options.Value;
                if (!current.IsEnabled || !current.DiscoveryEnabled || current.DiscoveryPort != settings.DiscoveryPort) break;
                using var receive = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                receive.CancelAfter(1000);
                UdpReceiveResult packet;
                try { packet = await udp.ReceiveAsync(receive.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { continue; }
                var now = DateTime.Now;
                if (now - window >= TimeSpan.FromSeconds(1)) { window = now; packets = 0; }
                if (++packets > 100 || packet.Buffer.Length > 4096) continue;
                var offer = Offer(packet.Buffer, packet.RemoteEndPoint.Address);
                if (offer is null) continue;
                // Limit state to currently registered sources even across years of directory changes.
                if (_rates.Count > 1000) _rates.Clear();
                _rates.TryGetValue(offer.SourceInstanceId, out var rate);
                if (now - rate.Start >= TimeSpan.FromMinutes(1)) rate = (now, 0);
                _rates[offer.SourceInstanceId] = (rate.Start, rate.Count + 1);
                if (rate.Count >= 20) continue;
                await udp.SendAsync(JsonSerializer.SerializeToUtf8Bytes(offer, FusionProtocol.Json), packet.RemoteEndPoint, cancellationToken);
            }
        }
    }
}

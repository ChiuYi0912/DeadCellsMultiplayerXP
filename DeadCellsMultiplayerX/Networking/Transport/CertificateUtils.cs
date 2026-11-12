namespace DeadCellsMultiplayerX.Networking.Transport;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal sealed class CertificateUtils
{
    /// <summary>
    /// 生成自签名 X.509 证书
    /// </summary>
    public static X509Certificate2 CreateSelfSignedCertificate(
        string subjectName,
        int validYears = 1)
    {
        using var rsa = RSA.Create(2048); // 或 4096

        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // 添加 KeyUsage 扩展
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature |
                X509KeyUsageFlags.KeyEncipherment |
                X509KeyUsageFlags.DataEncipherment,
                critical: true));

        // 添加 Enhanced Key Usage (EKU) — 服务器身份验证
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [
            new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
                ],
                critical: false));

        // 添加 Subject Alternative Name (SAN) — 支持多域名/IP
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);
        request.CertificateExtensions.Add(sanBuilder.Build());

        using (var cert = request.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(validYears)))
        {
            var d = cert.Export(X509ContentType.Pfx);
            return X509CertificateLoader.LoadPkcs12(d, null);
        }
    }
}

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MimeKit;
using Xunit;

namespace ECafe.Tests;

public sealed class PkcsCompatibilityTests
{
    [Fact]
    public void RuntimeLoadsThePatchedPkcsImplementation()
    {
        var informationalVersion = typeof(SignedCms).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.NotNull(informationalVersion);

        Assert.Equal(new Version(9, 0, 23), Version.Parse(informationalVersion.Split('+')[0]));
    }

    [Fact]
    public void SmallDetachedSignatureCanBeEncodedDecodedAndVerified()
    {
        var content = Encoding.UTF8.GetBytes("Local PKCS compatibility test");
        var encoded = Sign(content);
        var decoded = new SignedCms(new ContentInfo(content), detached: true);
        decoded.Decode(encoded);

        decoded.CheckSignature(verifySignatureOnly: true);

        Assert.Single(decoded.SignerInfos);
        Assert.Equal(content, decoded.ContentInfo.Content);
    }

    [Fact]
    public void ChangedContentDoesNotPassSignatureVerification()
    {
        var encoded = Sign(Encoding.UTF8.GetBytes("Original test content"));
        var decoded = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes("Changed test content")), detached: true);
        decoded.Decode(encoded);

        Assert.Throws<CryptographicException>(() => decoded.CheckSignature(verifySignatureOnly: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedSmallCmsPayloadIsRejectedWithControlledException(bool enveloped)
    {
        byte[] malformed = [0x30, 0x03, 0x01, 0x01, 0xff];

        if (enveloped)
            Assert.Throws<CryptographicException>(() => new EnvelopedCms().Decode(malformed));
        else
            Assert.Throws<CryptographicException>(() => new SignedCms().Decode(malformed));
    }

    [Fact]
    public async Task OrdinaryUtf8EmailStillPreservesAddressesSubjectAndBody()
    {
        const string subject = "E-Cafe bildirişi";
        const string body = "Salam, rezervasiyanız təsdiqləndi.";
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("E-Cafe", "sender@example.test"));
        message.To.Add(new MailboxAddress("Test", "recipient@example.test"));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        await using var stream = new MemoryStream();

        await message.WriteToAsync(stream);
        stream.Position = 0;
        using var decoded = await MimeMessage.LoadAsync(stream);

        Assert.Equal("sender@example.test", Assert.Single(decoded.From.Mailboxes).Address);
        Assert.Equal("recipient@example.test", Assert.Single(decoded.To.Mailboxes).Address);
        Assert.Equal(subject, decoded.Subject);
        var decodedBody = decoded.TextBody;
        Assert.NotNull(decodedBody);
        Assert.Equal(body, decodedBody.TrimEnd('\r', '\n'));
    }

    /// <summary>Yalnız testdə yaradılan sertifikatla kiçik CMS imzası qurur; sertifikat deposuna və canlı açarlara toxunmur.</summary>
    private static byte[] Sign(byte[] content)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=ECafe-Pkcs-Tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var message = new SignedCms(new ContentInfo(content), detached: true);
        message.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly });
        return message.Encode();
    }
}

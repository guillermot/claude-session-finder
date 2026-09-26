using System.Text;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Covers the head hash, which is the only signal that tells a rewritten file from an appended one
/// when the length happens to match.
/// </summary>
public sealed class FileFingerprintTests
{
    private const string Head = "{\"type\":\"user\",\"uuid\":\"u1\"}";

    [Fact]
    public void ComputeHeadHash_SameBytes_ProducesTheSameDigest()
    {
        var first = FileFingerprint.ComputeHeadHash(Encoding.UTF8.GetBytes(Head));
        var second = FileFingerprint.ComputeHeadHash(Encoding.UTF8.GetBytes(Head));

        second.Should().Be(first);
    }

    [Fact]
    public void ComputeHeadHash_DifferentBytes_ProducesADifferentDigest()
    {
        var first = FileFingerprint.ComputeHeadHash(Encoding.UTF8.GetBytes(Head));
        var second = FileFingerprint.ComputeHeadHash(Encoding.UTF8.GetBytes(Head.Replace("u1", "u2", StringComparison.Ordinal)));

        second.Should().NotBe(first);
    }

    [Fact]
    public void ComputeHeadHash_EmptyInput_StillProducesADigest()
    {
        var hash = FileFingerprint.ComputeHeadHash([]);

        hash.Should().NotBeNullOrEmpty();
    }
}

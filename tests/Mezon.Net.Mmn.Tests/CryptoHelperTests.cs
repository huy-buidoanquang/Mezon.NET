using Mezon.Net.Mmn.Models;
using Mezon.Net.Mmn.Utils;
using System.Numerics;
using Xunit;

namespace Mezon.Net.Mmn.Tests
{
    public sealed class CryptoHelperTests
    {
        [Fact]
        public void GenerateAddress_KnownInput_ReturnsExpectedAddress()
        {
            const string input = "3767478432163172990";
            const string expectedAddress = "DqrAfFo3yDQJhKuUo948RG4XfygHJPEe4UhcXxHF8hS2";

            var address = CryptoHelper.GenerateAddress(input);

            Assert.Equal(expectedAddress, address);
        }

        [Fact]
        public void SignTx_RoundTrip_VerifiesForTransfer()
        {
            var (publicKey, privateKey) = CryptoHelper.GenerateEd25519KeyPair();
            var sender = CryptoHelper.Base58Encode(publicKey);
            var tx = CryptoHelper.BuildTransferTx(
                (int)TxType.Transfer,
                sender,
                "CanBzWYv7Rf21DYZR5oDoon7NJmhLQ32eUvmyDGkeyK7",
                BigInteger.Parse("10000000"),
                nonce: 1,
                timestamp: 1,
                textData: "test",
                extraInfo: new Dictionary<string, string> { ["type"] = "transfer" },
                zkProof: "proof",
                zkPub: "pub");

            var signed = CryptoHelper.SignTx(tx, publicKey, privateKey);

            Assert.True(CryptoHelper.Verify(tx, signed.Sig));
        }

        [Fact]
        public void AmountToDecimal_ScalesByNativeDecimal()
        {
            var scaled = ValidationHelper.AmountToDecimal(BigInteger.One);
            Assert.Equal(BigInteger.Pow(10, Constants.NativeDecimal), scaled);
        }
    }
}

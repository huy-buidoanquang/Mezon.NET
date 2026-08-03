using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mezon.Net.Mmn.Models;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using SimpleBase;

namespace Mezon.Net.Mmn.Utils
{
    public static class CryptoHelper
    {
        public const int Ed25519PublicKeySizeInBytes = 32;
        public const int Ed25519ExpandedPrivateKeySizeInBytes = 64;
        public const int Ed25519PrivateKeySeedSizeInBytes = 32;

        private static readonly JsonSerializerOptions UserSigJsonOptions = new();

        public static byte[] Serialize(Tx tx)
        {
            var extraInfo = tx.ExtraInfo ?? string.Empty;
            var textData = tx.TextData ?? string.Empty;
            var metadata = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                tx.Type,
                tx.Sender,
                tx.Recipient,
                tx.Amount,
                textData,
                tx.Nonce,
                extraInfo);
            return Encoding.UTF8.GetBytes(metadata);
        }

        public static SignedTx SignTx(Tx tx, byte[] pubKey, byte[] privKey)
        {
            var signingKey = privKey.Length switch
            {
                32 => new Ed25519PrivateKeyParameters(privKey, 0),
                64 => new Ed25519PrivateKeyParameters(privKey, 0),
                _ => throw new ArgumentException("Unsupported private key length", nameof(privKey)),
            };

            var txHash = Serialize(tx);
            var signature = Sign(txHash, signingKey);

            if (tx.Type == (int)TxType.Faucet)
            {
                return new SignedTx
                {
                    Tx = tx,
                    Sig = Base58Encode(signature),
                };
            }

            var userSig = new UserSig
            {
                PubKey = pubKey,
                Sig = signature,
            };

            var userSigBytes = JsonSerializer.SerializeToUtf8Bytes(userSig, UserSigJsonOptions);
            return new SignedTx
            {
                Tx = tx,
                Sig = Base58Encode(userSigBytes),
            };
        }

        public static bool Verify(Tx tx, string sig)
        {
            var txHashBytes = Serialize(tx);
            if (tx.Type == (int)TxType.Faucet)
            {
                try
                {
                    var pubKeyBytes = Base58Decode(tx.Sender);
                    var signatureBytes = Base58Decode(sig);
                    return Verify(txHashBytes, signatureBytes, pubKeyBytes);
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                var sigBytes = Base58Decode(sig);
                var userSig = JsonSerializer.Deserialize<UserSig>(sigBytes, UserSigJsonOptions);
                if (userSig == null)
                {
                    return false;
                }

                return Verify(txHashBytes, userSig.Sig, userSig.PubKey);
            }
            catch
            {
                return false;
            }
        }

        public static Tx BuildTransferTx(
            int txType,
            string sender,
            string recipient,
            BigInteger amount,
            ulong nonce,
            ulong timestamp,
            string textData,
            Dictionary<string, string>? extraInfo,
            string zkProof,
            string zkPub)
        {
            ValidationHelper.ValidateAddress(sender);
            ValidationHelper.ValidateAddress(recipient);
            ValidationHelper.ValidateAmount(amount);

            var serializedTxExtra = ValidationHelper.SerializeTxExtraInfo(extraInfo);

            return new Tx
            {
                Type = txType,
                Sender = sender,
                Recipient = recipient,
                Amount = amount,
                Nonce = nonce,
                Timestamp = timestamp,
                TextData = textData,
                ExtraInfo = serializedTxExtra,
                ZkProof = zkProof,
                ZkPub = zkPub,
            };
        }

        public static byte[] Base58Decode(string input) => Base58.Bitcoin.Decode(input).ToArray();

        public static string Base58Encode(byte[] input) => Base58.Bitcoin.Encode(input);

        public static string GenerateAddress(string input)
        {
#if NET6_0_OR_GREATER
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
#else
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
#endif
            return Base58Encode(hash);
        }

        public static (byte[] publicKey, byte[] privateKey) GenerateEd25519KeyPair()
        {
            var seed = new byte[Ed25519PrivateKeySeedSizeInBytes];
            RandomNumberGenerator.Fill(seed);

            var privateKey = new Ed25519PrivateKeyParameters(seed, 0);
            var publicKey = privateKey.GeneratePublicKey().GetEncoded();
            return (publicKey, seed);
        }

        public static KeyPairAccount GenerateKeyPairAccount()
        {
            var (publicKey, privateKey) = GenerateEd25519KeyPair();
            return new KeyPairAccount(Base58Encode(publicKey), privateKey);
        }

        private static byte[] Sign(byte[] message, Ed25519PrivateKeyParameters privateKey)
        {
            var signer = new Ed25519Signer();
            signer.Init(true, privateKey);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.GenerateSignature();
        }

        private static bool Verify(byte[] message, byte[] signature, byte[] publicKey)
        {
            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
            verifier.BlockUpdate(message, 0, message.Length);
            return verifier.VerifySignature(signature);
        }
    }
}

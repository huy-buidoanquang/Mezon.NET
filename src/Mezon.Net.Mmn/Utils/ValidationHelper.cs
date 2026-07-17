using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Mezon.Net.Mmn.Utils
{
    public static class Constants
    {
        public const int NativeDecimal = 6;

        public const int AddressDecodedExpectedLength = 32;
    }

    public sealed class ValidationException : Exception
    {
        public ValidationException(string message)
            : base(message)
        {
        }
    }

    public static class ValidationHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new();

        public static void ValidateAddress(string addr)
        {
            if (string.IsNullOrEmpty(addr))
            {
                throw new ValidationException("Invalid address format");
            }

            try
            {
                var decoded = CryptoHelper.Base58Decode(addr);
                if (decoded.Length != Constants.AddressDecodedExpectedLength)
                {
                    throw new ValidationException("Invalid address format");
                }
            }
            catch (ValidationException)
            {
                throw;
            }
            catch
            {
                throw new ValidationException("Invalid address format");
            }
        }

        public static void ValidateAmount(BigInteger amount)
        {
            if (amount <= 0)
            {
                throw new ValidationException("Amount must be > 0");
            }
        }

        public static string SerializeTxExtraInfo(Dictionary<string, string>? data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            try
            {
                return JsonSerializer.Serialize(data, JsonOptions);
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Unable to marshal tx extra info: {ex.Message}");
            }
        }

        public static Dictionary<string, string>? DeserializeTxExtraInfo(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(raw, JsonOptions);
            }
            catch (Exception ex)
            {
                throw new ValidationException($"Unable to deserialize extra info: {ex.Message}");
            }
        }

        public static BigInteger AmountToDecimal(BigInteger amount)
        {
            var multiplier = BigInteger.Pow(10, Constants.NativeDecimal);
            return amount * multiplier;
        }

        public static BigInteger ParseScaledAmount(string amount)
        {
            if (!BigInteger.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new ValidationException("Invalid amount format");
            }

            return value;
        }
    }
}

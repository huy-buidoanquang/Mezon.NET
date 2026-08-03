using Mmn;
using Mezon.Net.Mmn.Models;
using System.Numerics;

namespace Mezon.Net.Mmn.Utils
{
    public static class ProtoConverter
    {
        public static BigInteger Uint256FromString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return BigInteger.Zero;
            }

            return BigInteger.Parse(value);
        }

        public static string Uint256ToString(BigInteger value) => value.ToString();

        public static TxMsg ToProtoTx(Tx tx)
        {
            return new TxMsg
            {
                Type = tx.Type,
                Sender = tx.Sender,
                Recipient = tx.Recipient,
                Amount = Uint256ToString(tx.Amount),
                Nonce = tx.Nonce,
                TextData = tx.TextData,
                Timestamp = tx.Timestamp,
                ExtraInfo = tx.ExtraInfo,
                ZkProof = tx.ZkProof,
                ZkPub = tx.ZkPub,
            };
        }

        public static SignedTxMsg ToProtoSigTx(SignedTx tx)
        {
            return new SignedTxMsg
            {
                TxMsg = ToProtoTx(tx.Tx),
                Signature = tx.Sig,
            };
        }

        public static Account FromProtoAccount(GetAccountResponse acc)
        {
            return new Account
            {
                Address = acc.Address,
                Balance = Uint256FromString(acc.Balance),
                Nonce = acc.Nonce,
            };
        }

        public static TxHistoryResponse FromProtoTxHistory(GetTxHistoryResponse res)
        {
            var txs = new List<TxMetaResponse>(res.Txs.Count);
            foreach (var tx in res.Txs)
            {
                txs.Add(new TxMetaResponse
                {
                    Sender = tx.Sender,
                    Recipient = tx.Recipient,
                    Amount = Uint256FromString(tx.Amount),
                    Nonce = tx.Nonce,
                    Timestamp = tx.Timestamp,
                    Status = (TxMetaStatus)tx.Status,
                });
            }

            return new TxHistoryResponse
            {
                Total = res.Total,
                Txs = txs,
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using SolanaTwentyfourtyeight;
using SolanaTwentyfourtyeight.Program;
using SolanaTwentyfourtyeight.Errors;
using SolanaTwentyfourtyeight.Accounts;
using SolanaTwentyfourtyeight.Types;

namespace SolanaTwentyfourtyeight
{
    namespace Accounts
    {
        public partial class PlayerData
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 9264901878634267077UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{197, 65, 216, 202, 43, 139, 147, 128};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ZzeEvyxXcpF";
            public PublicKey Authority { get; set; }

            public BoardData Board { get; set; }

            public uint Score { get; set; }

            public bool GameOver { get; set; }

            public byte Direction { get; set; }

            public uint TopTile { get; set; }

            public byte NewTileX { get; set; }

            public byte NewTileY { get; set; }

            public uint NewTileLevel { get; set; }

            public uint Xp { get; set; }

            public uint Level { get; set; }

            public static PlayerData Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                PlayerData result = new PlayerData();
                result.Authority = _data.GetPubKey(offset);
                offset += 32;
                offset += BoardData.Deserialize(_data, offset, out var resultBoard);
                result.Board = resultBoard;
                result.Score = _data.GetU32(offset);
                offset += 4;
                result.GameOver = _data.GetBool(offset);
                offset += 1;
                result.Direction = _data.GetU8(offset);
                offset += 1;
                result.TopTile = _data.GetU32(offset);
                offset += 4;
                result.NewTileX = _data.GetU8(offset);
                offset += 1;
                result.NewTileY = _data.GetU8(offset);
                offset += 1;
                result.NewTileLevel = _data.GetU32(offset);
                offset += 4;
                result.Xp = _data.GetU32(offset);
                offset += 4;
                result.Level = _data.GetU32(offset);
                offset += 4;
                return result;
            }
        }

        public partial class Highscore
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 11667186714381709185UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{129, 239, 224, 86, 128, 44, 234, 161};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "NjZ1MYFAXpU";
            public HighscoreEntry[] Global { get; set; }

            public HighscoreEntry[] Weekly { get; set; }

            public static Highscore Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Highscore result = new Highscore();
                int resultGlobalLength = (int)_data.GetU32(offset);
                offset += 4;
                result.Global = new HighscoreEntry[resultGlobalLength];
                for (uint resultGlobalIdx = 0; resultGlobalIdx < resultGlobalLength; resultGlobalIdx++)
                {
                    offset += HighscoreEntry.Deserialize(_data, offset, out var resultGlobalresultGlobalIdx);
                    result.Global[resultGlobalIdx] = resultGlobalresultGlobalIdx;
                }

                int resultWeeklyLength = (int)_data.GetU32(offset);
                offset += 4;
                result.Weekly = new HighscoreEntry[resultWeeklyLength];
                for (uint resultWeeklyIdx = 0; resultWeeklyIdx < resultWeeklyLength; resultWeeklyIdx++)
                {
                    offset += HighscoreEntry.Deserialize(_data, offset, out var resultWeeklyresultWeeklyIdx);
                    result.Weekly[resultWeeklyIdx] = resultWeeklyresultWeeklyIdx;
                }

                return result;
            }
        }

        public partial class Pricepool
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 8022590154388894035UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{83, 113, 229, 106, 61, 247, 85, 111};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ExXAhPBEyCi";
            public static Pricepool Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Pricepool result = new Pricepool();
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum SolanaTwentyfourtyeightErrorKind : uint
        {
            WrongAuthority = 6000U,
            GameNotOverYet = 6001U
        }
    }

    namespace Types
    {
        public partial class HighscoreEntry
        {
            public uint Score { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Nft { get; set; }

            public int Serialize(byte[] _data, int initialOffset)
            {
                int offset = initialOffset;
                _data.WriteU32(Score, offset);
                offset += 4;
                _data.WritePubKey(Player, offset);
                offset += 32;
                _data.WritePubKey(Nft, offset);
                offset += 32;
                return offset - initialOffset;
            }

            public static int Deserialize(ReadOnlySpan<byte> _data, int initialOffset, out HighscoreEntry result)
            {
                int offset = initialOffset;
                result = new HighscoreEntry();
                result.Score = _data.GetU32(offset);
                offset += 4;
                result.Player = _data.GetPubKey(offset);
                offset += 32;
                result.Nft = _data.GetPubKey(offset);
                offset += 32;
                return offset - initialOffset;
            }
        }

        public partial class BoardData
        {
            public uint[][] Data { get; set; }

            public int Serialize(byte[] _data, int initialOffset)
            {
                int offset = initialOffset;
                foreach (var dataElement in Data)
                {
                    foreach (var dataElementElement in dataElement)
                    {
                        _data.WriteU32(dataElementElement, offset);
                        offset += 4;
                    }
                }

                return offset - initialOffset;
            }

            public static int Deserialize(ReadOnlySpan<byte> _data, int initialOffset, out BoardData result)
            {
                int offset = initialOffset;
                result = new BoardData();
                result.Data = new uint[4][];
                for (uint resultDataIdx = 0; resultDataIdx < 4; resultDataIdx++)
                {
                    result.Data[resultDataIdx] = new uint[4];
                    for (uint resultDataresultDataIdxIdx = 0; resultDataresultDataIdxIdx < 4; resultDataresultDataIdxIdx++)
                    {
                        result.Data[resultDataIdx][resultDataresultDataIdxIdx] = _data.GetU32(offset);
                        offset += 4;
                    }
                }

                return offset - initialOffset;
            }
        }
    }

    public partial class SolanaTwentyfourtyeightClient : TransactionalBaseClient<SolanaTwentyfourtyeightErrorKind>
    {
        public SolanaTwentyfourtyeightClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>> GetPlayerDatasAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = PlayerData.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res);
            List<PlayerData> resultingAccounts = new List<PlayerData>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => PlayerData.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Highscore>>> GetHighscoresAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Highscore.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Highscore>>(res);
            List<Highscore> resultingAccounts = new List<Highscore>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Highscore.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Highscore>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Pricepool>>> GetPricepoolsAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Pricepool.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Pricepool>>(res);
            List<Pricepool> resultingAccounts = new List<Pricepool>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Pricepool.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Pricepool>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>> GetPlayerDataAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res);
            var resultingAccount = PlayerData.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Highscore>> GetHighscoreAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Highscore>(res);
            var resultingAccount = Highscore.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Highscore>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Pricepool>> GetPricepoolAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Pricepool>(res);
            var resultingAccount = Pricepool.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Pricepool>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribePlayerDataAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, PlayerData> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                PlayerData parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = PlayerData.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribeHighscoreAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Highscore> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Highscore parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Highscore.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribePricepoolAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Pricepool> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Pricepool parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Pricepool.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitPlayerAsync(InitPlayerAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.InitPlayer(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendResetWeeklyHighscoreAsync(ResetWeeklyHighscoreAccounts accounts, byte[] threadId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.ResetWeeklyHighscore(accounts, threadId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendResetAndDistributeAsync(ResetAndDistributeAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.ResetAndDistribute(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendPushInDirectionAsync(PushInDirectionAccounts accounts, byte direction, byte counter, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.PushInDirection(accounts, direction, counter, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendRestartAsync(RestartAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.Restart(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendStartThreadAsync(StartThreadAccounts accounts, byte[] threadId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.StartThread(accounts, threadId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendPauseThreadAsync(PauseThreadAccounts accounts, byte[] threadId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.PauseThread(accounts, threadId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendResumeThreadAsync(ResumeThreadAccounts accounts, byte[] threadId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.ResumeThread(accounts, threadId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendResetThreadAsync(ResetThreadAccounts accounts, byte[] threadId, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolanaTwentyfourtyeightProgram.ResetThread(accounts, threadId, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<SolanaTwentyfourtyeightErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<SolanaTwentyfourtyeightErrorKind>>{{6000U, new ProgramError<SolanaTwentyfourtyeightErrorKind>(SolanaTwentyfourtyeightErrorKind.WrongAuthority, "Wrong Authority")}, {6001U, new ProgramError<SolanaTwentyfourtyeightErrorKind>(SolanaTwentyfourtyeightErrorKind.GameNotOverYet, "Game not over yet")}, };
        }
    }

    namespace Program
    {
        public class InitPlayerAccounts
        {
            public PublicKey Player { get; set; }

            public PublicKey Highscore { get; set; }

            public PublicKey PricePool { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey Avatar { get; set; }

            public PublicKey ClientDevWallet { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class ResetWeeklyHighscoreAccounts
        {
            public PublicKey Highscore { get; set; }

            public PublicKey Place1 { get; set; }

            public PublicKey Place2 { get; set; }

            public PublicKey Place3 { get; set; }

            public PublicKey PricePool { get; set; }

            public PublicKey SystemProgram { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }
        }

        public class ResetAndDistributeAccounts
        {
            public PublicKey Highscore { get; set; }

            public PublicKey PricePool { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class PushInDirectionAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Highscore { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey Avatar { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class RestartAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Highscore { get; set; }

            public PublicKey PricePool { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey Avatar { get; set; }

            public PublicKey ClientDevWallet { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class StartThreadAccounts
        {
            public PublicKey Highscore { get; set; }

            public PublicKey PricePool { get; set; }

            public PublicKey ClockworkProgram { get; set; }

            public PublicKey Payer { get; set; }

            public PublicKey SystemProgram { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }
        }

        public class PauseThreadAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey ClockworkProgram { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }
        }

        public class ResumeThreadAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey ClockworkProgram { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }
        }

        public class ResetThreadAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey ClockworkProgram { get; set; }

            public PublicKey Thread { get; set; }

            public PublicKey ThreadAuthority { get; set; }
        }

        public static class SolanaTwentyfourtyeightProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitPlayer(InitPlayerAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PricePool, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Avatar, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.ClientDevWallet, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(4819994211046333298UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ResetWeeklyHighscore(ResetWeeklyHighscoreAccounts accounts, byte[] threadId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Place1 == null ? programId : accounts.Place1, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Place2 == null ? programId : accounts.Place2, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Place3 == null ? programId : accounts.Place3, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PricePool, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Thread, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9998402410022544069UL, offset);
                offset += 8;
                _data.WriteS32(threadId.Length, offset);
                offset += 4;
                _data.WriteSpan(threadId, offset);
                offset += threadId.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ResetAndDistribute(ResetAndDistributeAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PricePool, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Thread, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9198043416659377369UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction PushInDirection(PushInDirectionAccounts accounts, byte direction, byte counter, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Avatar, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(5017331766430244341UL, offset);
                offset += 8;
                _data.WriteU8(direction, offset);
                offset += 1;
                _data.WriteU8(counter, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Restart(RestartAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.PricePool, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Avatar, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.ClientDevWallet, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(2919679679907439389UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction StartThread(StartThreadAccounts accounts, byte[] threadId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Highscore, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.PricePool, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ClockworkProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Thread, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(15046500361448809729UL, offset);
                offset += 8;
                _data.WriteS32(threadId.Length, offset);
                offset += 4;
                _data.WriteSpan(threadId, offset);
                offset += threadId.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction PauseThread(PauseThreadAccounts accounts, byte[] threadId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ClockworkProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Thread, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(8210672603296534645UL, offset);
                offset += 8;
                _data.WriteS32(threadId.Length, offset);
                offset += 4;
                _data.WriteSpan(threadId, offset);
                offset += threadId.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ResumeThread(ResumeThreadAccounts accounts, byte[] threadId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ClockworkProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Thread, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(5675786826980878817UL, offset);
                offset += 8;
                _data.WriteS32(threadId.Length, offset);
                offset += 4;
                _data.WriteSpan(threadId, offset);
                offset += threadId.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ResetThread(ResetThreadAccounts accounts, byte[] threadId, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ClockworkProgram, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Thread, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.ThreadAuthority, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9796277319507992179UL, offset);
                offset += 8;
                _data.WriteS32(threadId.Length, offset);
                offset += 4;
                _data.WriteSpan(threadId, offset);
                offset += threadId.Length;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using ByteStream.Mananged;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.CommunicationModel.PacketStructs.Recv;
using NewGMHack.CommunicationModel.PacketStructs.Send;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.MemoryScanner;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.CommunicationModel.PacketStructs;
using SharpDX.Direct3D9;
using ZLinq;
using ZLogger;
using NewGMHack.Stub.Models;

namespace NewGMHack.Stub.Services;

public record PacketContext(nint Socket, byte[] Data);

public class PacketProcessorService : BackgroundService
{
    private readonly Channel<PacketContext> _packetChannel;

    //private readonly WinsockHookManager              _hookManager;
    private readonly ILogger<PacketProcessorService> _logger;
    private readonly IBuffSplitter                   _buffSplitter;
    private readonly GmMemory                        gm;
    private readonly SelfInformation                 _selfInformation;
    private readonly IpcNotificationService          _ipcService;
    private readonly Channel<(nint, List<Reborn>)>   _bombChannel;
    private readonly WinsockHookManager              _winsockHookManager;
    private readonly Channel<BattleLogEvent>         _battleLogChannel;


    /// <inheritdoc />
    public PacketProcessorService(Channel<PacketContext> packetChannel, ILogger<PacketProcessorService> logger,
                                  IBuffSplitter buffSplitter, GmMemory gm, SelfInformation selfInformation,
                                  Channel<(nint, List<Reborn>)> bombChannel, IEnumerable<IHookManager> managers,
                                  Channel<RewardEvent> rewardChannel, IpcNotificationService ipcService,
                                  Channel<BattleLogEvent> battleLogChannel)
    {
        _packetChannel      = packetChannel;
        _logger             = logger;
        _buffSplitter       = buffSplitter;
        this.gm             = gm;
        _selfInformation    = selfInformation;
        _bombChannel        = bombChannel;
        _winsockHookManager = managers.OfType<WinsockHookManager>().First();
        _rewardChannel      = rewardChannel;
        _ipcService         = ipcService;
        _battleLogChannel   = battleLogChannel;
    }

    private readonly Channel<RewardEvent> _rewardChannel;

    /// <inheritdoc />
    private readonly Lock _lock = new Lock();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.ZLogInformation($"Starting Parse for packet");
            await foreach (var packet in _packetChannel.Reader.ReadAllAsync(stoppingToken))
            {
                if (packet.Data.Length > 0)
                {
                    await Parse(packet, stoppingToken);
                }
            }

            _logger.ZLogInformation($"Finished Parse");
            //await foreach (var packet in _packetChannel.Reader.ReadAllAsync(stoppingToken))
            //{
            //    try
            //    {
            //        if(packet.Data.Length == 0)continue;
            //        await Parse(packet);
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.ZLogInformation($"{ex.Message} | {ex.StackTrace}");
            //    }
            //}
        }
        catch (Exception ex)
        {
            _logger.ZLogCritical(ex, $"the packet processor stopped");
        }
    }

    private async Task Parse(PacketContext packet, CancellationToken token)
    {
        try
        {

            if (packet.Data.Length == 0) return;
            var methodPackets = _buffSplitter.Split(packet.Data);
            if (methodPackets.Count == 0) return;
            var reborns = new ConcurrentQueue<Reborn>(); // Use Queue for better performance than Bag

            if (methodPackets.Count >= 20)
            {
                // Use 50% of processors or at least 2, cap at 8 to avoid saturation
                int maxDegree = Math.Max(2, Math.Min(Environment.ProcessorCount / 2, 8));

                await Parallel.ForEachAsync(methodPackets,
                                            new ParallelOptions()
                                                { MaxDegreeOfParallelism = maxDegree, CancellationToken = token },
                                            async (methodPacket, ct) =>
                                            {
                                                await DoParseWork(packet.Socket, methodPacket, reborns, ct);
                                            });
            }
            else
            {
                foreach (var methodPacket in methodPackets)
                {
                    await DoParseWork(packet.Socket, methodPacket, reborns, token);
                }
            }

            if (!reborns.IsEmpty)
            {
                await SendToBombServices(packet, reborns, token);
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error occur in packet processor");
        }
    }

    /// <summary>
    /// TODO large amount of hacks lost in this switch , need fix for new packet
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="methodPacket"></param>
    /// <param name="reborns"></param>
    /// <returns></returns>
    private async Task DoParseWork(IntPtr socket, PacketSegment methodPacket, ConcurrentQueue<Reborn> reborns,
                                   CancellationToken token)
    {
        var method = methodPacket.Method;
        var reader = new ByteReader(methodPacket.MethodBody);
        //_logger.ZLogInformation($"processing {method} {BitConverter.ToString(methodPacket.MethodBody)}");
        ////_logger.ZLogInformation($"method: {method}");
        switch (method)
        {
            // case 1992 or 1338 or 2312 or 1525 or 1521 or 2103:
            //1342 player reborn in battle
            case 2245: //after F5
                await ReadGameReady(methodPacket.MethodBody.AsMemory(), token);
                break;
            case 2143: // battle reborn
                _selfInformation.ClientConfig.IsInGame = true;
                ReadPlayerStats(methodPacket.MethodBody, reborns);
                //if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
                //{
                //    _logger.ZLogInformation($"battle reborn packet with stats");
                //    //ReadReborns(reader, reborns,false);
                //}
                break;
            case 1992 or 1342 or 2065: //or 1521 or 2312 or 1525 or 1518:
                _selfInformation.ClientConfig.IsInGame = true;
                if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) ||
                    _selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
                {
                    ReadReborns(reader, reborns);
                }

                // _logger.ZLogInformation($"found reborn  : {reborn.TargetId}");
                break;
            case 2722 or 2670 or 2361:
                ChargeCondom(socket, _selfInformation.PersonInfo.Slot);
                break;

            case 2107 or 2108 or 2109 or 2110: // these are possible back room`
                ChargeCondom(socket, _selfInformation.PersonInfo.Slot);
                SendF5(socket);
                break;
            case 2877:
                ReadPlayerBasicInfo(methodPacket.MethodBody);
                ChargeCondom(socket, _selfInformation.PersonInfo.Slot);
                SendF5(socket);
                break;
            //case 1616: // Hit response - track damage
            //    ReadHitResponse1616(methodPacket.MethodBody.AsMemory(), reborns);
            //    break;
            case 1246 or 2535:
                _selfInformation.ClientConfig.IsInGame = false;
                var changed = ReadChangedMachine(methodPacket.MethodBody);
                _logger.ZLogInformation($"change condom detected:{changed.MachineId}");

                // Store both raw and processed machine data
                //_selfInformation.CurrentMachine = changed;
                _selfInformation.CurrentMachineModel = MachineModel.FromRaw(changed);

                var slot = changed.Slot;
                _selfInformation.PersonInfo.Slot = slot;

                try
                {
                    var machineInfo = await ScanCondom(changed.MachineId, token: token);

                    if (machineInfo != null)
                    {
                        // Notify Frontend via IPC -> SignalR
                        _logger.ZLogInformation($"Sending MachineInfoUpdate for machine {changed.MachineId}");
                        var response = new NewGMHack.CommunicationModel.IPC.Responses.MachineInfoResponse
                        {
                            MachineModel    = _selfInformation.CurrentMachineModel,
                            MachineBaseInfo = machineInfo
                        };
                        await _ipcService.SendMachineInfoUpdateAsync(response);
                    }
                }
                catch (Exception ex)
                {
                    _logger.ZLogError($"ScanCondom/IPC Error: {ex} {ex.Message}");
                }

                ChargeCondom(socket, slot);
                break;
            case 1259: // get room list
                AssignPersonId(reader);
                _selfInformation.ClientConfig.IsInGame = false;
                break;
            case 1244 or 2109 or 1885 or 1565:
                AssignPersonId(reader);

                _selfInformation.ClientConfig.IsInGame = false;
                break;
            case 1550 or 1282 or 1490 or 2253 or 1933 or 2326: // 1691 or 2337 or 1550:
                _selfInformation.BombHistory.Clear();
                _selfInformation.ClientConfig.IsInGame = true;
                SendSkipScreen(socket);
                break;
            case 2751:
                _selfInformation.BombHistory.Clear();
                _selfInformation.ClientConfig.IsInGame = false;
                EndBattleSession(); // End battle session on first reward packet
                ReadReport(methodPacket.MethodBody);
                break;
            case 2280:
                _selfInformation.BombHistory.Clear();
                _selfInformation.ClientConfig.IsInGame = false;
                EndBattleSession(); // Safe to call multiple times - checks internally
                ReadBonus(methodPacket.MethodBody);
                break;
            case 1940:
                _selfInformation.BombHistory.Clear();
                _selfInformation.ClientConfig.IsInGame = false;
                EndBattleSession(); // Safe to call multiple times - checks internally
                ReadRewardGrade(methodPacket.MethodBody);
                break;
            //case 1858 or 1270:
            //    _selfInformation.BombHistory.Clear();
            //    _selfInformation.ClientConfig.IsInGame = false;
            //    var mates = ReadRoommates(methodPacket.MethodBody.AsMemory());
            //    _logger.LogInformation($"local Roomate:{string.Join("|", mates)}");
            //    _selfInformation.Roommates.Clear();
            //    foreach (var c in mates)
            //    {
            //        _selfInformation.Roommates.Add(c);
            //    }

            //    _logger.LogInformation($" global Roomate:{string.Join("|", _selfInformation.Roommates)}");
            //    break;
            //case 1847: // someone join 
            //    _selfInformation.ClientConfig.IsInGame = false;
            //    _selfInformation.BombHistory.Clear();
            //    RequestRoomInfo(socket);
            //    break;
            //case 1851: //someone leave

            //    _selfInformation.ClientConfig.IsInGame = false;
            //    _selfInformation.BombHistory.Clear();
            //    HandleRoommateLeave(socket, methodPacket.MethodBody.AsMemory());
            //break;
            case 2472:
                _selfInformation.ClientConfig.IsInGame = true;
                ReadHitResponse2472(methodPacket.MethodBody, reborns);
                break;

            case 1616:
                _selfInformation.ClientConfig.IsInGame = true;
                ReadHitResponse1616(methodPacket.MethodBody, reborns);
                break;
            case 2360:
                _selfInformation.ClientConfig.IsInGame = true;
                ReadDeads(methodPacket.MethodBody);
                break;
            //case 1506:
            //    _selfInformation.ClientConfig.IsInGame = true;
            //    ReadDeads1506(methodPacket.MethodBody);
            //    break;
            //case 1338: // hitted or got hitted recv

            //    _selfInformation.ClientConfig.IsInGame = true;
            //    ReadHitResponse1338(methodPacket.MethodBody.AsMemory(), reborns);
            //    break;
            //case 1525: // non direct hit 

            //    _selfInformation.ClientConfig.IsInGame = true;
            //    ReadHitResponse1525(methodPacket.MethodBody.AsMemory(), reborns);
            //    break;
            //case 1340:

            //    _selfInformation.ClientConfig.IsInGame = true;
            //    ReadDeads(methodPacket.MethodBody.AsMemory());
            //    break;
            case 2042:
//1E 00 F0 03 FA 07 46 EF 00 00 (personId[the guy changeit]) 4F 14 00 00 00 00 00 00 04 00 45 C7 00 00 30 75 45 24 14 00
                break;
            case 2080:
                // No-op
                break;
            case 2070: // gift recv 16 08
                ReadGifts(socket, methodPacket.MethodBody.AsMemory());
                break;
            //case 2132 : //funnel recv
            //    _selfInformation.ClientConfig.IsInGame = true;
            //    ReadAndSendFunnel(methodPacket.MethodBody.AsMemory());
            //    break;
            default:
                break;
        }
    }

    private unsafe void ReadPlayerBasicInfo(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _selfInformation.ClientConfig.IsInGame = false;
            var header = bytes.ReadStruct<PlayerBasicInfoStruct>();

            _selfInformation.PersonInfo.PersonId = header.PlayerId;

            // Get name from fixed byte array - decode as GB2312
            int                nameLen   = Math.Min((int)header.NameLength, 30);
            ReadOnlySpan<byte> nameBytes = new ReadOnlySpan<byte>(header.Name, nameLen);
            _selfInformation.PersonInfo.PlayerName = chs.GetString(nameBytes).Trim('\0').Trim();

            _logger.ZLogInformation($"PlayerBasicInfo: Id={header.PlayerId} Name={_selfInformation.PersonInfo.PlayerName}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error in ReadPlayerBasicInfo");
        }
    }

    /// <summary>
    /// End current battle session and send SessionEnd event to logger
    /// </summary>
    private void EndBattleSession()
    {
        var sessionId = _selfInformation.BattleState.CurrentSessionId;
        if (!string.IsNullOrEmpty(sessionId))
        {
            _battleLogChannel.Writer.TryWrite(new BattleLogEvent
            {
                Type      = BattleEventType.SessionEnd,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            });
            _selfInformation.BattleState.EndSession();
            _logger.ZLogInformation($"Battle session ended: {sessionId}");
        }

        _selfInformation.WeaponNameCache.Clear();
    }

    private async Task ReadGameReady(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        try
        {
            var header = bytes.Span.ReadStruct<GameReadyStruct>();
            // Convert to array since Span can't cross await boundaries
            var playersSpan = bytes.Span.SliceAfter<GameReadyStruct>().CastTo<PlayerBattleStruct>();
            int count       = Math.Min(header.PlayerCount, playersSpan.Length);
            var players     = playersSpan.Slice(0, count).ToArray();

            _logger.ZLogInformation($"GameReady: MyPlayerId={header.PlayerId} Map={header.MapId} GameType={header.GameType} IsTeam={header.IsTeam} PlayerCount={header.PlayerCount}");

            // Store self info
            _selfInformation.PersonInfo.PersonId   = header.PlayerId;
            _selfInformation.ClientConfig.IsInGame = true;

            // Start new battle session
            _selfInformation.BattleState.StartSession(header.MapId, header.GameType, header.IsTeam);
            var sessionId = _selfInformation.BattleState.CurrentSessionId!;

            // Build player records for persistence
            var playerRecords = new List<BattlePlayerRecord>();

            // ---------------------------------------------------------
            // OPTIMIZED BATCH SCANNING START
            // ---------------------------------------------------------
            try
            {
                // 1. Batch Scan Machines
                var distinctMachineIds = players.Select(p => p.MachineId).Where(id => id > 0).Distinct().ToList();
                _logger.ZLogInformation($"Batch scanning {distinctMachineIds.Count} machines");
                var loadedMachines = await gm.ScanMachines(distinctMachineIds, token);

                // 2. Batch Scan Transforms
                // Filter out IDs we just scanned to avoid duplicates (though cache handles it, this saves logic in ScanMachines)
                var transIds = loadedMachines
                              .Where(m => m.HasTransform && m.TransformId != 0 &&
                                          !distinctMachineIds.Contains(m.TransformId))
                              .Select(m => m.TransformId)
                              .Distinct()
                              .ToList();

                var loadedTrans = new List<MachineBaseInfo>();
                if (transIds.Count > 0)
                {
                    _logger.ZLogInformation($"Batch scanning {transIds.Count} transformed machines");
                    loadedTrans = await gm.ScanMachines(transIds, token);
                }

                // 3. Collect Weapon IDs from ALL machines (base + transforms)
                var allWeaponIds = new HashSet<uint>();
                foreach (var m in loadedMachines.Concat(loadedTrans))
                {
                    if (m.Weapon1Code       != 0) allWeaponIds.Add(m.Weapon1Code);
                    if (m.Weapon2Code       != 0) allWeaponIds.Add(m.Weapon2Code);
                    if (m.Weapon3Code       != 0) allWeaponIds.Add(m.Weapon3Code);
                    if (m.SpecialAttackCode != 0) allWeaponIds.Add(m.SpecialAttackCode);
                }

                // 4. Batch Scan Weapons
                if (allWeaponIds.Count > 0)
                {
                    _logger.ZLogInformation($"Batch scanning {allWeaponIds.Count} weapons");
                    await gm.ScanWeapons(allWeaponIds, token);
                }
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Error during batch scanning phase in ReadGameReady");
            }
            // ---------------------------------------------------------
            // OPTIMIZED BATCH SCANNING END
            // ---------------------------------------------------------

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.RoomSlot != i) continue;
                _logger.ZLogInformation($"Player[{p.RoomSlot}]: Id={p.Player} Team={p.TeamId1},{p.TeamId2} Machine={p.MachineId} HP={p.MaxHP} Atk={p.Attack} Def={p.Defense} Shield={p.Shield}");
                // Update in-memory state for HP tracking
                _selfInformation.BattleState.SetPlayer(p.Player, p.TeamId1, p.MachineId, p.MaxHP, p.Attack, p.Defense,
                                                       p.Shield, p.RoomSlot);

                // Build record for SQLite
                playerRecords.Add(new BattlePlayerRecord
                {
                    SessionId = sessionId,
                    PlayerId  = p.Player,
                    TeamId    = p.TeamId1,
                    MachineId = p.MachineId,
                    MaxHP     = p.MaxHP,
                    Attack    = p.Attack,
                    Defense   = p.Defense,
                    Shield    = p.Shield
                });

                // ORIGINAL LOGIC: Scan machine only for self (to avoid blocking)
                // Note: Thanks to the batch scan above, ScanCondom here will likely hit the cache instantly for machine/weapons!
                if (p.Player == header.PlayerId)
                {
                    try
                    {
                        var machineInfo = await ScanCondom(p.MachineId, token: token);

                        if (machineInfo != null)
                        {
                            _selfInformation.CurrentMachineModel = new MachineModel
                            {
                                MachineId = p.MachineId,
                                Slot      = p.RoomSlot
                            };
                            _selfInformation.PersonInfo.Slot = p.RoomSlot;

                            _logger.ZLogInformation($"Set current machine from GameReady: MachineId={p.MachineId}");

                            // Notify Frontend via IPC
                            var response = new NewGMHack.CommunicationModel.IPC.Responses.MachineInfoResponse
                            {
                                MachineModel    = _selfInformation.CurrentMachineModel,
                                MachineBaseInfo = machineInfo
                            };
                            await _ipcService.SendMachineInfoUpdateAsync(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ZLogError($"ScanCondom error for self: {ex.Message}");
                    }
                }
            }

            // Send to battle logger for persistence
            _battleLogChannel.Writer.TryWrite(new BattleLogEvent
            {
                Type      = BattleEventType.SessionStart,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Session = new BattleSessionRecord
                {
                    SessionId   = sessionId,
                    PlayerId    = header.PlayerId,
                    MapId       = header.MapId,
                    GameType    = header.GameType,
                    IsTeam      = header.IsTeam,
                    PlayerCount = count,
                    StartedAt   = DateTime.UtcNow.ToString("O")
                },
                Players = playerRecords
            });
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error in ReadGameReady");
        }
    }

    private void ReadPlayerStats(ReadOnlySpan<byte> bytes, ConcurrentQueue<Reborn> reborns)
    {
        try
        {
            var playerState = bytes.ReadStruct<PlayerStateStruct>();

            // Reset HP for player reborn
            _selfInformation.BattleState.ResetPlayerHP(playerState.SpawnId);

            // Log reborn event
            var sessionId = _selfInformation.BattleState.CurrentSessionId;
            if (!string.IsNullOrEmpty(sessionId))
            {
                _battleLogChannel.Writer.TryWrite(new BattleLogEvent
                {
                    Type           = BattleEventType.Reborn,
                    SessionId      = sessionId,
                    Timestamp      = DateTime.UtcNow,
                    RebornPlayerId = playerState.SpawnId
                });
            }

            if (!_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
                return;

            // If MyPlayerId != SpawnId, add to reborn queue
            if (playerState.MyPlayerId != playerState.SpawnId)
            {
                _logger.ZLogInformation($"battle reborn packet: MyPlayerId={playerState.MyPlayerId} SpawnId={playerState.SpawnId}");
                reborns.Enqueue(new Reborn(playerState.MyPlayerId, playerState.SpawnId, 0));
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error in ReadPlayerStats");
        }
    }

    private Machine ReadChangedMachine(ReadOnlyMemory<byte> methodPacketMethodBody)
    {
        var changed = methodPacketMethodBody.Span.ReadStruct<GetChangedMachine>();
        return changed.Machine;
    }

    private void ReadDeads1506(ReadOnlyMemory<byte> methodPacketMethodBody)
    {
        try
        {

            var  dead      = methodPacketMethodBody.Span.ReadStruct<Dead1506>();
            bool isRemoved = _selfInformation.BombHistory.TryRemove(dead.DeadId, out _);
            if (!isRemoved)
            {
                _logger.ZLogInformation($"error in bomb history remove : readdead 1506");
            }
            else
            {
                _logger.ZLogInformation($"removed :{dead.DeadId} since it is dead from 1506");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"error occur in readdeads 1506");
        }

    }

    //private void ReadAndSendFunnel(ReadOnlyMemory<byte> buffer)
    //{
    //    var funnelRecv = buffer.Span.ReadStruct<FunnelPacketRecv>();
    //    if (funnelRecv.FromId != _selfInformation.PersonInfo.PersonId) return;
    //    SendFunnel2129 sendFunnel2129 = new SendFunnel2129();
    //sendFunnel2129.Version = 20 ;
    //sendFunnel2129.Count = (byte)(funnelRecv.Count + 1);
    //sendFunnel2129.Method = 2129 ;
    //sendFunnel2129.MyPlayerId = funnelRecv.MyPlayerId;
    //sendFunnel2129.Split = funnelRecv.Split;
    //sendFunnel2129.WeaponId = funnelRecv.WeaponId;
    //sendFunnel2129.TargetId = sendFunnel.TargetId;
    //}
    public void SendF5(IntPtr socket)
    {
        if (!_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady)) return;
        ReadOnlySpan<byte> msg5 = new byte[]
            { 0x0A, 0x00, 0xF0, 0x03, 0x89, 0x09, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };
        ReadOnlySpan<byte> msg6 = [0x06, 0x00, 0xF0, 0x03, 0x1A, 0x09, 0x00, 0x00, 0x00, 0x00];
        _winsockHookManager.SendPacket(socket, msg5);
        _winsockHookManager.SendPacket(socket, msg6);
    }

    public void ReadGifts(IntPtr socket, ReadOnlyMemory<byte> buffer)
    {
        _logger.ZLogInformation($"gift buffer: {string.Join(" ", buffer.ToArray().Select(b => b.ToString("X2")))}");
        if (!_selfInformation.ClientConfig.Features.GetFeature(FeatureName.CollectGift).IsEnabled) return;
        var personId = BitConverter.ToUInt32(buffer.Span.Slice(0, 4)); // EB 02 00 00 → 0x000002EB
        _selfInformation.PersonInfo.PersonId = personId;

        var giftStructs = buffer.Slice(4).Span.CastTo<GiftStruct>().ToArray();

        _logger.ZLogInformation($"gifts count : {giftStructs.Length}");
        foreach (var gift in giftStructs.Where(x => x.ItemType != 301))
        {

            //_logger.ZLogInformation($"accepting gift:{gift.GiftId}");
            AcceptGiftPacket acceptGiftPacket = new AcceptGiftPacket()
            {
                Length   = 14,
                Splitter = 1008,
                Method   = 2071,
                GiftId   = gift.GiftId
            };

            AcceptGiftPacket acceptGiftPacket2 = new AcceptGiftPacket()
            {
                Length   = 14,
                Splitter = 1008,
                Method   = 2074,
                GiftId   = gift.GiftId
            };
            _winsockHookManager.SendPacket(socket, acceptGiftPacket.ToByteArray().AsSpan());
            _winsockHookManager.SendPacket(socket, acceptGiftPacket2.ToByteArray().AsSpan());

        }
    }

    private void ReadDeads(ReadOnlyMemory<byte> buffer)
    {
        try
        {
            var deadStruct = buffer.Span.ReadStruct<DeadStruct>();
            var deads      = buffer.Span.SliceAfter<DeadStruct>().CastTo<Deads>();
            // _logger.ZLogInformation($"sstruct : {deadStruct.PersonId}|{deadStruct.KillerId}|{deadStruct.Count}");

            // Enhanced logging: show header info
            _logger.ZLogInformation($"[ReadDeads] PersonId={deadStruct.PersonId} KillerId={deadStruct.KillerId} Count={deadStruct.Count} BufferLen={buffer.Length}");

            if (deadStruct.Count > 0)
            {
                var sessionId = _selfInformation.BattleState.CurrentSessionId;

                // Only process Count entries, not the entire array
                int actualCount = Math.Min(deadStruct.Count, deads.Length);

                // Log only the valid dead IDs
                var deadIds = new uint[actualCount];
                for (int i = 0; i < actualCount; i++)
                {
                    deadIds[i] = deads[i].Id;
                }
                //_logger.ZLogInformation($"[ReadDeads] deads:{string.Join("|", deadIds)}");

                for (int i = 0; i < actualCount; i++)
                {
                    var dead = deads[i];
                    //Log death event if in battle session (temporarily disabled)
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        // Set player HP to 0 on death
                        _selfInformation.BattleState.UpdatePlayerHP(dead.Id, 0, 0);

                        _battleLogChannel.Writer.TryWrite(new BattleLogEvent
                        {
                            Type      = BattleEventType.Death,
                            SessionId = sessionId,
                            Timestamp = DateTime.UtcNow,
                            Death = new DeathEventRecord
                            {
                                SessionId = sessionId,
                                Timestamp = DateTime.UtcNow.ToString("O"),
                                VictimId  = dead.Id,
                                KillerId  = deadStruct.KillerId
                            }
                        });
                    }

                    if (_selfInformation.BombHistory.TryGetValue(dead.Id, out var count))
                    {
                        bool isRemoved = _selfInformation.BombHistory.TryRemove(dead.Id, out var _);
                        if (isRemoved)
                        {
                            //_logger.ZLogInformation($"[ReadDeads] Removed:{dead.Id} since it is dead");
                        }
                        else
                        {
                            _logger.ZLogInformation($"[ReadDeads] cannot remove:{dead.Id}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[ReadDeads] error in readdead");
        }
    }

    /// <summary>
    /// Hit response handler for packet 2472
    /// Structure: MyId, AttackerId, AttackerSP, WeaponId, Unknown[3], VictimCount, Victim[]
    /// </summary>
    private void ReadHitResponse2472(ReadOnlyMemory<byte> bytes, ConcurrentQueue<Reborn> reborns)
    {
        try
        {
            var hitResponse = bytes.Span.ReadStruct<HitResponse2472>();
            var victims     = bytes.Span.SliceAfter<HitResponse2472>().CastTo<Victim>();

            //_logger.ZLogInformation($"[2472] MyId={hitResponse.MyPlayerId} From={hitResponse.FromId} SP={hitResponse.AttackerSP} Weapon={hitResponse.WeaponId} VictimCount={hitResponse.VictimCount}");

            uint toId      = hitResponse.VictimCount > 0 && victims.Length > 0 ? victims[0].VictimId : 0;
            var  sessionId = _selfInformation.BattleState.CurrentSessionId;

            int victimCount = Math.Min((int)hitResponse.VictimCount, victims.Length);
            for (int i = 0; i < victimCount; i++)
            {
                var victim = victims[i];

                int hpDelta = _selfInformation.BattleState.UpdatePlayerHP(
                                                                          victim.VictimId,
                                                                          victim.AfterHitHP,
                                                                          victim.AfterHitShieldHP);

                if (!string.IsNullOrEmpty(sessionId) && hpDelta != 0)
                {
                    _battleLogChannel.Writer.TryWrite(new BattleLogEvent
                    {
                        Type      = BattleEventType.Damage,
                        SessionId = sessionId,
                        Timestamp = DateTime.UtcNow,
                        Damage = new DamageEventRecord
                        {
                            SessionId         = sessionId,
                            Timestamp         = DateTime.UtcNow.ToString("O"),
                            AttackerId        = hitResponse.FromId,
                            WeaponId          = hitResponse.WeaponId,
                            VictimId          = victim.VictimId,
                            Damage            = hpDelta,
                            VictimHPAfter     = victim.AfterHitHP,
                            VictimShieldAfter = victim.AfterHitShieldHP,
                            IsKill            = 0
                        }
                    });
                }

                // Queue floating damage for overlay display (only when I am the attacker)
                if (hitResponse.FromId == _selfInformation.PersonInfo.PersonId && hpDelta != 0)
                {
                    _selfInformation.DamageNumbers.Enqueue(new FloatingDamage
                    {
                        Amount    = hpDelta,
                        SpawnTime = Environment.TickCount64,
                        VictimId  = victim.VictimId,
                        X         = _selfInformation.CrossHairX, // Start at crosshair, will update in overlay
                        Y         = _selfInformation.CrossHairY
                    });
                }

                // Send damage received message (only when I am the victim and attacker is someone else)
                if (victim.VictimId    == _selfInformation.PersonInfo.PersonId &&
                    hitResponse.FromId != _selfInformation.PersonInfo.PersonId &&
                    hpDelta            > 0)
                {
                    bool found = _selfInformation.WeaponNameCache.TryGetValue(hitResponse.WeaponId , out var name);
                    _selfInformation.ReceivedDamageLogs.Enqueue(new DamageLog
                    {
                        Message   = $"{hitResponse.FromId} use { (found ?  name : hitResponse.WeaponId)} dmg : {hpDelta}",
                        TimeAdded = Environment.TickCount64
                    });
                }
            }

            if (hitResponse.FromId != _selfInformation.PersonInfo.PersonId)
            {
                if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) ||
                    _selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
                {
                    reborns.Enqueue(new Reborn(hitResponse.MyPlayerId, toId, 0));
                }
            }

            if (toId == _selfInformation.PersonInfo.PersonId)
            {
                if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsRebound))
                {
                    reborns.Enqueue(new Reborn(hitResponse.MyPlayerId, hitResponse.FromId, 0));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error in hit response 2472");
        }
    }

    /// <summary>
    /// Hit response handler for packet 1616
    /// Structure: MyId, AttackerId, Unknown1, AttackerSP, WeaponId, Unknown2, VictimCount, Victim[]
    /// </summary>
    private void ReadHitResponse1616(ReadOnlyMemory<byte> bytes, ConcurrentQueue<Reborn> reborns)
    {
        try
        {
            var hitResponse = bytes.Span.ReadStruct<HitResponse1616>();
            var victims     = bytes.Span.SliceAfter<HitResponse1616>().CastTo<Victim>();

            //_logger.ZLogInformation($"[1616] MyId={hitResponse.MyPlayerId} From={hitResponse.FromId} SP={hitResponse.AttackerSP} Weapon={hitResponse.WeaponId} VictimCount={hitResponse.VictimCount}");

            uint toId      = hitResponse.VictimCount > 0 && victims.Length > 0 ? victims[0].VictimId : 0;
            var  sessionId = _selfInformation.BattleState.CurrentSessionId;

            // Update Attacker SP (Note: 1616 struct has AttackerSP too)
            _selfInformation.BattleState.UpdatePlayerSP(hitResponse.FromId, hitResponse.AttackerSP);

            int victimCount = Math.Min((int)hitResponse.VictimCount, victims.Length);
            for (int i = 0; i < victimCount; i++)
            {
                var victim = victims[i];

                int hpDelta = _selfInformation.BattleState.UpdatePlayerHP(
                                                                          victim.VictimId,
                                                                          victim.AfterHitHP,
                                                                          victim.AfterHitShieldHP);

                if (!string.IsNullOrEmpty(sessionId) && hpDelta != 0)
                {
                    _battleLogChannel.Writer.TryWrite(new BattleLogEvent
                    {
                        Type      = BattleEventType.Damage,
                        SessionId = sessionId,
                        Timestamp = DateTime.UtcNow,
                        Damage = new DamageEventRecord
                        {
                            SessionId         = sessionId,
                            Timestamp         = DateTime.UtcNow.ToString("O"),
                            AttackerId        = hitResponse.FromId,
                            WeaponId          = hitResponse.WeaponId,
                            VictimId          = victim.VictimId,
                            Damage            = hpDelta,
                            VictimHPAfter     = victim.AfterHitHP,
                            VictimShieldAfter = victim.AfterHitShieldHP,
                            IsKill            = 0
                        }
                    });
                }

                // Queue floating damage for overlay display (only when I am the attacker)
                if (hitResponse.FromId == _selfInformation.PersonInfo.PersonId && hpDelta != 0)
                {
                    _selfInformation.DamageNumbers.Enqueue(new FloatingDamage
                    {
                        Amount    = hpDelta,
                        SpawnTime = Environment.TickCount64,
                        VictimId  = victim.VictimId,
                        X         = _selfInformation.CrossHairX,
                        Y         = _selfInformation.CrossHairY
                    });
                }

                // Send damage received message (only when I am the victim and attacker is someone else)
                if (victim.VictimId    == _selfInformation.PersonInfo.PersonId &&
                    hitResponse.FromId != _selfInformation.PersonInfo.PersonId &&
                    hpDelta            > 0)
                {

                    bool found = _selfInformation.WeaponNameCache.TryGetValue(hitResponse.WeaponId , out var name);
                    _selfInformation.ReceivedDamageLogs.Enqueue(new DamageLog
                    {
                        Message   = $"{hitResponse.FromId} use {( found ? name : hitResponse.WeaponId  )} dmg : {hpDelta}",
                        TimeAdded = Environment.TickCount64
                    });
                }
            }

            if (hitResponse.FromId != _selfInformation.PersonInfo.PersonId)
            {
                if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) ||
                    _selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
                {
                    reborns.Enqueue(new Reborn(hitResponse.MyPlayerId, toId, 0));
                }
            }

            if (toId == _selfInformation.PersonInfo.PersonId)
            {
                if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsRebound))
                {
                    reborns.Enqueue(new Reborn(hitResponse.MyPlayerId, hitResponse.FromId, 0));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error in hit response 1616");
        }
    }

    private void ReadRewardGrade(ReadOnlySpan<byte> bytes)
    {
        var gradeReport = bytes.ReadStruct<RewardGrade>();

        var transformedGrade = RewardTransformations.ToGradeRank(gradeReport.Grade);
        _logger.ZLogInformation($"Grade Player:{gradeReport.PlayerId} Grade:{RewardTransformations.GradeRankToString(transformedGrade)} Damage:{gradeReport.DamageScore} Team:{gradeReport.TeamExpectationScore} Skill:{gradeReport.SkillFulScore}");

        var safeGrade = new SafeRewardGrade
        {
            PlayerId             = gradeReport.PlayerId,
            Grade                = transformedGrade,
            DamageScore          = gradeReport.DamageScore,
            TeamExpectationScore = gradeReport.TeamExpectationScore,
            SkillFulScore        = gradeReport.SkillFulScore
        };

        _rewardChannel.Writer.TryWrite(new RewardEvent
        {
            Timestamp = DateTime.UtcNow,
            Grade     = safeGrade
        });
    }

    private void ReadReport(ReadOnlySpan<byte> bytes)
    {
        var report     = bytes.ReadStruct<RewardReport>();
        var gameStatus = RewardTransformations.ToGameStatus(report.WinOrLostOrDraw);
        _logger.ZLogInformation($"Report Player[{RewardTransformations.GameStatusToString(gameStatus)}]:{report.PlayerId} K:{report.Kills} D:{report.Deaths} S:{report.Supports} Point:{report.Points} Exp:{report.ExpGain} GB:{report.GBGain} MachineAddedExp:{report.MachineAddedExp} MachineExp:{report.MachineExp} Practice:{report.PracticeExpAdded}");

        _rewardChannel.Writer.TryWrite(new RewardEvent
        {
            Timestamp = DateTime.UtcNow,
            Report    = report
        });
    }

    private unsafe void ReadBonus(ReadOnlySpan<byte> bytes)
    {
        var rewardsBonus = bytes.ReadStruct<RewardBonus>();
        //LLM,create log
        _logger.ZLogInformation($"Bonus Player:{rewardsBonus.PlayerId} Values: {rewardsBonus.Bonuses[0]}|{rewardsBonus.Bonuses[1]}|{rewardsBonus.Bonuses[2]}|{rewardsBonus.Bonuses[3]}|{rewardsBonus.Bonuses[4]}|{rewardsBonus.Bonuses[5]}|{rewardsBonus.Bonuses[6]}|{rewardsBonus.Bonuses[7]}");

        var safeBonus = new SafeRewardBonus
        {
            PlayerId = rewardsBonus.PlayerId,
            Bonuses =
            [
                rewardsBonus.Bonuses[0], rewardsBonus.Bonuses[1], rewardsBonus.Bonuses[2], rewardsBonus.Bonuses[3],
                rewardsBonus.Bonuses[4], rewardsBonus.Bonuses[5], rewardsBonus.Bonuses[6], rewardsBonus.Bonuses[7]
            ]
        };

        _rewardChannel.Writer.TryWrite(new RewardEvent
        {
            Timestamp = DateTime.UtcNow,
            Bonus     = safeBonus
        });
    }
    // ReadHitResponse2472 removed - consolidated into ReadHitResponse

    private void ReadHitResponse1525(ReadOnlyMemory<byte> bytes, ConcurrentBag<Reborn> reborns)
    {
        // _logger.ZLogInformation($"hit");
        var hitResponse = bytes.Span.ReadStruct<HitResponse1525>();

        // hitResponse = bytes.ReadStruct<HitResponse1525>();
        // _logger.ZLogInformation($"hit:{hitResponse.MyPlayerId} | {hitResponse.FromId} | {hitResponse.ToId}  ");
        // lock (_lock)
        // {
        //     _selfInformation.PersonInfo.PersonId = hitResponse.MyPlayerId;
        // }
        if (hitResponse.FromId != _selfInformation.PersonInfo.PersonId)
        {
            if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) ||
                _selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
            {
                reborns.Add(new Reborn(hitResponse.PlayerId, hitResponse.ToId, 0));
            }
        }

        if (hitResponse.ToId == _selfInformation.PersonInfo.PersonId)
        {
            if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsRebound))
            {
                reborns.Add(new Reborn(hitResponse.PlayerId, hitResponse.FromId, 0));
            }
        }
    }

    private void HandleRoommateLeave(IntPtr socket, ReadOnlyMemory<byte> bytes)
    {
        var roomActionOnLeave = bytes.Span.ReadStruct<RoomActionOnLeave>();
        var leaveId           = roomActionOnLeave.LeaveId;
        // var existingRoommate  = _selfInformation.Roommates.AsValueEnumerable().FirstOrDefault(x => x.MyPlayerId == leaveId);
        // if (existingRoommate != null)
        // {
        //     _selfInformation.Roommates.(existingRoommate);
        // }
        // else
        // {
        RequestRoomInfo(socket);
        // }
    }

    private void RequestRoomInfo(IntPtr socket)
    {
        ReadOnlySpan<byte> requestRoomInfoBytes =
        [
            0x0A, 0x00, 0xF0, 0x03, 0xF4, 0x04, 0x00, 0x00,
            0x00, 0x00, 0x4C, 0x00, 0x00, 0x00
        ];
        _winsockHookManager.SendPacket(socket, requestRoomInfoBytes);
    }

    private static Encoding chs = Encoding.GetEncoding(936) ?? Encoding.Default;

    private List<Roommate> ReadRoommates(ReadOnlyMemory<byte> data)
    {

        ReadOnlySpan<byte> start         = [0xE8, 0x03, 0x00, 0x00, 0xE9, 0x03, 0x00, 0x00];
        ReadOnlySpan<byte> end           = [0xB5, 0x1E, 0x04, 0x00, 0x33, 0xC1, 0x1D, 0x00];
        var                roommateBytes = data.Span.SliceBetweenMarkers(start, end);
        if (data.Span.IndexOf(start) >= 0)
        {
            _logger.ZLogInformation($"room start index found");
        }
        else
        {
            return [];
        }

        if (data.Span.IndexOf(end) >= 0)
        {
            _logger.ZLogInformation($"room end index found");
        }
        else
        {
            return [];
        }

        List<Roommate> roomates = new(12);
        //normally , the first mother fucker is the room leader once join room (of cuz reassign when room leader fucked off)
        foreach (var roommateByte in roommateBytes)
        {
            var roommate = roommateByte.AsSpan().ReadStruct<RoommateHeader>();
            roomates.Add(new Roommate
            {
                PlayerId = roommate.PlayerId,
                ItemId   = roommate.ItemId,
                Name     = chs.GetString(roommate.GetNameSpan()).Trim(),
            });
        }

        return roomates;
    }

    private async Task SendToBombServices(PacketContext     packet, ConcurrentQueue<Reborn> reborns,
                                          CancellationToken token)
    {
        try
        {
            var distinctTargets = reborns.AsValueEnumerable()
                                         .Where(x => x.TargetId != x.PersionId)
                                          //.Distinct()
                                         .ToList();
            int targetCount = distinctTargets.Count;
            _logger.ZLogInformation($"target counts : {targetCount} -- {string.Join(",", distinctTargets.Select(c => c.TargetId))}");
            if (targetCount > 0)
            {
                await _bombChannel.Writer.WriteAsync((packet.Socket, distinctTargets), token);
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"{ex.Message} {ex.StackTrace ?? ""}");
        }
    }

    private void ReadReborns(ByteReader reader, ConcurrentQueue<Reborn> reborns, bool isReadLocation = true)
    {
        try
        {
            var reborn = reader.ReadReborn(isReadLocation);

            // lock (_lock)
            // {
            if (reborn.PersionId == _selfInformation.PersonInfo.PersonId)
            {
                reborns.Enqueue(reborn);
            }
            // }
        }
        catch
        {
        }
    }

    private void ChargeCondom(IntPtr socket, UInt32 slot)
    {
        //return;//TODO fix
        if (slot == 0) return;
        _logger.ZLogInformation($"charging condom: {slot} ");
        if (!_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoCharge)) return;
        ChargeRequest r = new();
        r.Version = 14;
        r.Split   = 1008;
        r.Method  = 1437;
        r.Slot    = slot;
        _winsockHookManager.SendPacket(socket, r.ToByteArray().AsSpan());
    }

    private async Task<MachineBaseInfo?> ScanCondom(UInt32 machineId, CancellationToken token)
    {
        _logger.ZLogInformation($"Machine id begin scan: {machineId}");

        var machineInfo = await gm.ScanMachineWithDetails(machineId, token);

        _logger.ZLogInformation($"Machine id scan completed: {machineId}");

        if (machineInfo == null) return null;

        lock (_lock)
        {
            _selfInformation.PersonInfo.CondomId = machineId;
            if (!string.IsNullOrEmpty(machineInfo.ChineseName))
                _selfInformation.PersonInfo.CondomName = machineInfo.ChineseName;
            else if (!string.IsNullOrEmpty(machineInfo.EnglishName))
                _selfInformation.PersonInfo.CondomName = machineInfo.EnglishName;

            if (machineInfo.Weapon1Code != 0)
                _selfInformation.PersonInfo.Weapon1 = machineInfo.Weapon1Code;
            if (machineInfo.Weapon2Code != 0)
                _selfInformation.PersonInfo.Weapon2 = machineInfo.Weapon2Code;
            if (machineInfo.Weapon3Code != 0)
                _selfInformation.PersonInfo.Weapon3 = machineInfo.Weapon3Code;

            // Store the full machine info for API access
            _selfInformation.CurrentMachineBaseInfo = machineInfo;
        }

        _logger.ZLogInformation($"Machine: {machineInfo.ChineseName} | W1:{machineInfo.Weapon1Code} | W2:{machineInfo.Weapon2Code} | W3:{machineInfo.Weapon3Code}");
        return machineInfo;
    }

    private void AssignPersonId(ByteReader reader)
    {
        var s = reader.ReadPersonId();

        //_logger.ZLogInformation($"personid :{s.PersionId}");
        lock (_lock)
        {
            _selfInformation.PersonInfo.PersonId = s.PersionId;
        }
    }

    private void SendSkipScreen(IntPtr socket)
    {
        //byte[] escBuffer =
        //[
        //    0x0E, 0x00, 0xF0, 0x03, 0x23, 0x08,
        //    0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        //    0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        //];

        byte[] escBuffer =
        [
            0x0E, 0x00, 0xF0, 0x03, 0x39, 0x09, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ];
        ReadOnlySpan<byte> zone1 =
        [
            0x17, 0x00, 0xF0, 0x03, 0x6A, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x8D, 0xF6, 0xE1, 0x44,
            0xD2, 0x3F, 0x2C, 0x45, 0x7D, 0x68, 0x31, 0x00, 0x01
        ];
        ReadOnlySpan<byte> zone2 =
        [
            0x17, 0x00, 0xF0, 0x03, 0x6A, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xB0, 0x21, 0x09, 0x45,
            0x0C, 0xF4, 0xEC, 0xC3, 0x7E, 0x68, 0x31, 0x00, 0x02
        ];
        ReadOnlySpan<byte> zone3 =
        [
            0x17, 0x00, 0xF0, 0x03, 0x6A, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xD0, 0x91, 0x1A, 0xC5,
            0x89, 0x47, 0xF1, 0xC4, 0x7F, 0x68, 0x31, 0x00, 0x03
        ];
//ReadOnlySpan<byte>   unknownSkip  = [0x0C, 0x00, 0xF0, 0x03, 0x1E, 0x08, 0x00, 0x00, 0x00, 0x00, 0xFA, 0x52, 0x00, 0x80, 0x00, 0x02
//];
        _winsockHookManager.SendPacket(socket, escBuffer);
        if (_selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) ||
            _selfInformation.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
        {
            DestroyAllBuildings();
        }
        //_winsockHookManager.SendPacket(socket, zone1);

        //_winsockHookManager.SendPacket(socket, zone2);

        //_winsockHookManager.SendPacket(socket, zone3);

        //_winsockHookManager.SendPacket(socket, unknownSkip);
    }

    /// <summary>
    /// Build and send a fake game message packet for damage received notification
    /// </summary>
    private void SendDamageReceivedMessage(uint attackerId, uint weaponId, int damage)
    {
        string message = $"{attackerId} use {weaponId} Dmg:{damage}";
        SendReceivedMessage(message);
    }

    /// <summary>
    /// Send a fake game message to the game client
    /// Uses the loopback socket to inject as recv to the game
    /// </summary>
    private void SendReceivedMessage(string message, string name = "Michael Van", string tag = "Michael Van")
    {
        try
        {
            // Create GameMessage2574 struct
            var gameMsg = GameMessage2574.Create(
                                                 _selfInformation.PersonInfo.PersonId,
                                                 name,
                                                 tag,
                                                 message
                                                );

            // Build packet: [Length:2][Split:2][Method:2][Struct:123] = 129 bytes
            Span<byte> packet = stackalloc byte[129];

            // Header
            packet[0] = 0x81; // Length low byte (129)
            packet[1] = 0x00; // Length high byte
            packet[2] = 0xF0; // Split marker
            packet[3] = 0x03;
            packet[4] = 0x0E; // Method ID 2574 (0x0A0E) low byte
            packet[5] = 0x0A; // Method ID high byte

            System.Runtime.InteropServices.MemoryMarshal.Write(packet.Slice(6), in gameMsg);
            _winsockHookManager.SendRecvPacket(packet);
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Failed to send received message: {ex.Message}");
        }
    }

    /// <summary>
    /// Destroy all buildings from ID 7000 to 8000
    /// </summary>
    public void DestroyAllBuildings()
    {
        var socket = _selfInformation.LastSocket;
        if (socket == IntPtr.Zero)
        {
            _logger.ZLogError($"Cannot destroy buildings: No active socket.");
            return;
        }

        _logger.ZLogInformation($"Starting DestroyAllBuildings (7000-8000)...");
        
        // Generate range 7000-8000 (inclusive)
        var buildingIds = Enumerable.Range(7000, 1001).Select(i => (uint)i).ToList();
        
        // Process in batches of 8
        foreach (var batch in buildingIds.Chunk(8))
        {
            SendBuildingDamage(socket, batch);
        }
        
        _logger.ZLogInformation($"Finished DestroyAllBuildings.");
    }

  
    private unsafe void SendBuildingDamage(nint socket, uint[] ids)
    {
        try 
        {
            var packet = new BuildingDamageFrame
            {
                // Header
                Length      = (ushort)sizeof(BuildingDamageFrame), // Should be 79 (0x4F)
                Split       = 0x03F0,
                Method      = 0x08E6,
                Padding1    = 0,
                AttackerUID = _selfInformation.PersonInfo.PersonId,
                Count       = (byte)ids.Length
            };

            // Fill Arrays
            for (int i = 0; i < 8; i++)
            {
                if (i < ids.Length)
                {
                    packet.Buildings[i] = ids[i];
                    packet.Damages[i]   = 999999; // Massive damage to ensure destruction
                }
                else
                {
                    packet.Buildings[i] = 0;
                    packet.Damages[i]   = 0;
                }
            }

            // Serialize and Send
            Span<byte> buffer = stackalloc byte[sizeof(BuildingDamageFrame)];
            System.Runtime.InteropServices.MemoryMarshal.Write(buffer, in packet);
            
            _winsockHookManager.SendPacket(socket, buffer);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Error sending building damage packet");
        }
    }
    
}


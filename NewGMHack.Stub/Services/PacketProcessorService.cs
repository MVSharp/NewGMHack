using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using ByteStream.Mananged;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.CommunicationModel.PacketStructs.Recv;
using NewGMHack.Stub.MemoryScanner;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Recv;
using Reloaded.Memory.Extensions;
using ZLinq;
using ZLogger;

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
    private readonly Channel<(nint, List<Reborn>)>   _bombChannel;
    private readonly WinsockHookManager              _winsockHookManager;

    /// <inheritdoc />
    public PacketProcessorService(Channel<PacketContext> packetChannel, ILogger<PacketProcessorService> logger,
                                  IBuffSplitter buffSplitter, GmMemory gm, SelfInformation selfInformation,
                                  Channel<(nint, List<Reborn>)> bombChannel, WinsockHookManager winsockHookManager)
    {
        _packetChannel      = packetChannel;
        _logger             = logger;
        _buffSplitter       = buffSplitter;
        this.gm             = gm;
        _selfInformation    = selfInformation;
        _bombChannel        = bombChannel;
        _winsockHookManager = winsockHookManager;
    }

    /// <inheritdoc />
    private readonly Lock _lock = new Lock();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var packet in _packetChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await Parse(packet);
            }
            catch (Exception ex)
            {
                _logger.ZLogInformation($"{ex.Message} | {ex.StackTrace}");
            }
        }
    }

    private async Task Parse(PacketContext packet)
    {
        if (packet.Data.Length == 0) return;
        //_logger.ZLogInformation($"{packet.Socket} | {packet.Data.Length}");
        var methodPackets = _buffSplitter.Split(packet.Data);
        //_logger.ZLogInformation($"method packet : {methodPackets.Count}");
        if (methodPackets.Count == 0) return;
        var reborns = new ConcurrentBag<Reborn>();
        if (methodPackets.Count >= 9)
        {
            await Parallel.ForEachAsync(methodPackets, new ParallelOptions() { MaxDegreeOfParallelism = 3 },
                                        async (methodPacket, _) =>
                                        {
                                            await DoParseWork(packet.Socket, methodPacket, reborns);
                                        });
        }
        else
        {
            foreach (var methodPacket in methodPackets)
            {
                await DoParseWork(packet.Socket, methodPacket, reborns);
            }
        }

        if (!reborns.IsEmpty)
        {
            await SendToBombServices(packet, reborns);
        }
    }

    private async Task DoParseWork(IntPtr socket, PacketSegment methodPacket, ConcurrentBag<Reborn> reborns)
    {
        var method = methodPacket.Method;
        var reader = new ByteReader(methodPacket.MethodBody);
        //_logger.ZLogInformation($"method: {method}");
        switch (method)
        {
            // case 1992 or 1338 or 2312 or 1525 or 1521 or 2103:
            //1342 player reborn in battle
            case 1992 or 1342: //or 1521 or 2312 or 1525 or 1518:

                ReadReborns(reader, reborns);

                // _logger.ZLogInformation($"found reborn  : {reborn.TargetId}");
                break;
            // case 1338:
            //     //need add ignore teammale
            //     var damaged = reader.ReadDamaged();
            //     _logger.ZLogInformation($"Damaged Targets:{damaged.Count}");
            //     foreach (var damagedTarget in damaged)
            //     {
            //         reborns.Add(damagedTarget);
            //     }
            //     //damage recv
            //     break;
            case 1246:
                await ScanGundam(reader);
                break;

            case 1244:
                AssignPersonId(reader);

                break;
            case 1550: // 1691 or 2337 or 1550:
                SendSkipScreen(socket);
                break;
            case 1858 or 1270:
                var mates = ReadRoommates(methodPacket.MethodBody);

                // _logger.LogInformation($"local Roomate:{string.Join("|" , mates)}");
                _selfInformation.Roommates.Clear();
                foreach (var c in mates)
                {
                    _selfInformation.Roommates.Add(c);
                }

                // _logger.LogInformation($" global Roomate:{string.Join("|" ,_selfInformation.Roommates)}");
                break;
            case 1847: // someone join 
                RequestRoomInfo(socket);
                break;
            case 1851: //someone leave
                HandleRoommateLeave(socket, methodPacket.MethodBody);
                break;

            case 1338: // hitted or got hitted recv
                ReadHitResponse1338(methodPacket.MethodBody, reborns);
                break;
            case 1525: // non direct hit 

                ReadHitResponse1525(methodPacket.MethodBody, reborns);
                break;
            case 2080:
                // No-op
                break;
            default:
                break;
        }
    }

    private void ReadHitResponse1338(ReadOnlySpan<byte> bytes, ConcurrentBag<Reborn> reborns )
    {
        // _logger.ZLogInformation($"hit");
           var  hitResponse = bytes.ReadStruct<HitResponse1338>();

             // hitResponse = bytes.ReadStruct<HitResponse1525>();
        // _logger.ZLogInformation($"hit:{hitResponse.PlayerId} | {hitResponse.FromId} | {hitResponse.ToId}  ");
        // lock (_lock)
        // {
        //     _selfInformation.PersonInfo.PersonId = hitResponse.PlayerId;
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

    private void ReadHitResponse1525(ReadOnlySpan<byte> bytes, ConcurrentBag<Reborn> reborns )
    {
        // _logger.ZLogInformation($"hit");
           var  hitResponse = bytes.ReadStruct<HitResponse1525>();

             // hitResponse = bytes.ReadStruct<HitResponse1525>();
        // _logger.ZLogInformation($"hit:{hitResponse.PlayerId} | {hitResponse.FromId} | {hitResponse.ToId}  ");
        // lock (_lock)
        // {
        //     _selfInformation.PersonInfo.PersonId = hitResponse.PlayerId;
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
        // var existingRoommate  = _selfInformation.Roommates.AsValueEnumerable().FirstOrDefault(x => x.PlayerId == leaveId);
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
        List<Roommate>     roomates      = new(12);
        //normally , the first mother fucker is the room leader once join room (of cuz reassign when room leader fucked off)
        foreach (var roommateByte in roommateBytes)
        {
            var roommate = roommateByte.AsSpanFast().ReadStruct<RoommateHeader>();
            roomates.Add(new Roommate
            {
                PlayerId = roommate.PlayerId,
                ItemId   = roommate.ItemId,
                Name     = chs.GetString(roommate.GetNameSpan()).Trim(),
            });
        }

        return roomates;
    }

    private async Task SendToBombServices(PacketContext packet, ConcurrentBag<Reborn> reborns)
    {
        try
        {
            var distinctTargets = reborns.AsValueEnumerable()
                                         .Where(x => x.TargetId != x.PersionId)
                                          //.Distinct()
                                         .ToList();
            int targetCount = distinctTargets.Count;
            _logger.ZLogInformation($"target counts : {targetCount}");
            if (targetCount > 0)
            {
                await _bombChannel.Writer.WriteAsync((packet.Socket, distinctTargets));
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"{ex.Message} {ex.StackTrace ?? ""}");
            return;
        }
    }

    private void ReadReborns(ByteReader reader, ConcurrentBag<Reborn> reborns)
    {
        try
        {
            var reborn = reader.ReadReborn();

            // lock (_lock)
            // {
            if (reborn.PersionId == _selfInformation.PersonInfo.PersonId)
            {
                reborns.Add(reborn);
            }
            // }
        }
        catch
        {
        }
    }

    private async Task ScanGundam(ByteReader reader)
    {
        var changed   = reader.ReadChangedMachine();
        var machineId = changed.MachineId;

        _logger
           .ZLogInformation($"Machine id begin scan: {changed.MachineId} ");
        var w = await gm.ScanAsync(machineId).ConfigureAwait(false);

        _logger
           .ZLogInformation($"Machine id  scan completed: {changed.MachineId} ");
        if (w is { w1: 0, w2: 0, w3: 0 }) return;
        lock (_lock)
        {
            _selfInformation.PersonInfo.GundamId = machineId;
            if (w.w1 != 0) _selfInformation.PersonInfo.Weapon1 = (uint)w.w1;
            if (w.w2 != 0) _selfInformation.PersonInfo.Weapon2 = (uint)w.w2;
            if (w.w3 != 0) _selfInformation.PersonInfo.Weapon3 = (uint)w.w3;
        }

        _logger
           .ZLogInformation($"machine id : {machineId} | weapon1 : {w.w1} | weapon2 : {w.w2} | weapon3 : {w.w3}");
    }

    private void AssignPersonId(ByteReader reader)
    {
        var s = reader.ReadPersonId();

        _logger.ZLogInformation($"personid :{s.PersionId}");
        lock (_lock)
        {
            _selfInformation.PersonInfo.PersonId = s.PersionId;
        }
    }

    private void SendSkipScreen(IntPtr socket)
    {
        byte[] escBuffer =
        [
            0x0E, 0x00, 0xF0, 0x03, 0x23, 0x08,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        _winsockHookManager.SendPacket(socket, escBuffer);
    }
}
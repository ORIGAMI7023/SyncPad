namespace SyncPad.Server.Core.Utils;

/// <summary>
/// 雪花算法ID生成器
/// </summary>
public class SnowflakeIdGenerator
{
    private readonly long _machineId;
    private long _sequence = 0L;
    private long _lastTimestamp = -1L;
    private readonly object _lock = new object();

    // 常量定义
    private const long Epoch = 1704067200000; // 2024-01-01 00:00:00 UTC (毫秒)
    private const int MachineIdBits = 10;     // 机器ID位数（支持1024个节点）
    private const int SequenceBits = 12;      // 序列号位数（每毫秒4096个ID）

    private const long MaxMachineId = (1L << MachineIdBits) - 1;
    private const long MaxSequence = (1L << SequenceBits) - 1;

    private const int TimestampShift = MachineIdBits + SequenceBits;
    private const int MachineIdShift = SequenceBits;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="machineId">机器ID（0-1023）</param>
    public SnowflakeIdGenerator(long machineId)
    {
        if (machineId < 0 || machineId > MaxMachineId)
        {
            throw new ArgumentException($"机器ID必须在0到{MaxMachineId}之间", nameof(machineId));
        }
        _machineId = machineId;
    }

    /// <summary>
    /// 生成下一个ID
    /// </summary>
    public long NextId()
    {
        lock (_lock)
        {
            long timestamp = GetCurrentTimestamp();

            // 时钟回拨检查
            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException($"时钟回拨检测。上次时间戳: {_lastTimestamp}, 当前时间戳: {timestamp}");
            }

            // 同一毫秒内，序列号递增
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    // 序列号溢出，等待下一毫秒
                    timestamp = WaitForNextMillis(_lastTimestamp);
                }
            }
            else
            {
                // 新的毫秒，序列号重置
                _sequence = 0L;
            }

            _lastTimestamp = timestamp;

            // 生成ID
            return ((timestamp - Epoch) << TimestampShift)
                   | (_machineId << MachineIdShift)
                   | _sequence;
        }
    }

    /// <summary>
    /// 获取当前时间戳（毫秒）
    /// </summary>
    private static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 等待下一毫秒
    /// </summary>
    private static long WaitForNextMillis(long lastTimestamp)
    {
        long timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            Thread.Sleep(0); // 让出CPU时间片
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    public static DateTime GetDateTimeFromId(long id)
    {
        long timestamp = (id >> TimestampShift) + Epoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
    }

    /// <summary>
    /// 从ID中提取机器ID
    /// </summary>
    public static long GetMachineIdFromId(long id)
    {
        return (id >> MachineIdShift) & MaxMachineId;
    }

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    public static long GetSequenceFromId(long id)
    {
        return id & MaxSequence;
    }
}

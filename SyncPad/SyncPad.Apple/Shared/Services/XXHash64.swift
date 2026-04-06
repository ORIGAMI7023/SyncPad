import Foundation

// MARK: - XXHash64
/// 纯 Swift 实现的 XXHash64 算法，用于缓存键计算
struct XXHash64 {
    private static let prime1: UInt64 = 0x9E3779B185EBCA87
    private static let prime2: UInt64 = 0xC2B2AE3D27D4EB4F
    private static let prime3: UInt64 = 0x165667B19E3779F9
    private static let prime4: UInt64 = 0x85EBCA77C2B2AE63
    private static let prime5: UInt64 = 0x27D4EB2F165667C5

    /// 计算 Data 的 XXHash64 值
    static func hash(data: Data, seed: UInt64 = 0) -> UInt64 {
        var hash: UInt64
        var index = 0

        if data.count >= 32 {
            var v1 = seed &+ prime1 &+ prime2
            var v2 = seed &+ prime2
            var v3 = seed
            var v4 = seed &- prime1

            while index <= data.count - 32 {
                v1 = round(v1, readUInt64(data, index)); index += 8
                v2 = round(v2, readUInt64(data, index)); index += 8
                v3 = round(v3, readUInt64(data, index)); index += 8
                v4 = round(v4, readUInt64(data, index)); index += 8
            }

            hash = rotateLeft(v1, 1) &+ rotateLeft(v2, 7) &+ rotateLeft(v3, 12) &+ rotateLeft(v4, 18)
            hash = hash &+ mergeRound(hash, v1)
            hash = hash &+ mergeRound(hash, v2)
            hash = hash &+ mergeRound(hash, v3)
            hash = hash &+ mergeRound(hash, v4)
        } else {
            hash = seed &+ prime5
        }

        hash &+= UInt64(data.count)

        while index <= data.count - 8 {
            hash = hash &+ (readUInt64(data, index) &* prime3)
            hash = rotateLeft(hash, 17) &* prime4
            index += 8
        }

        if index <= data.count - 4 {
            hash = hash &+ (UInt64(readUInt32(data, index)) &* prime3)
            hash = rotateLeft(hash, 17) &* prime4
            index += 4
        }

        while index < data.count {
            hash = hash &+ (UInt64(data[index]) &* prime5)
            hash = rotateLeft(hash, 11) &* prime1
            index += 1
        }

        hash = finalize(hash)
        return hash
    }

    /// 计算文件的 XXHash64，返回十六进制字符串（16字符）
    static func hashFile(url: URL, seed: UInt64 = 0) -> String? {
        guard let data = try? Data(contentsOf: url) else { return nil }
        let hashValue = hash(data: data, seed: seed)
        return String(format: "%016llx", hashValue)
    }

    // MARK: - Private Helpers

    private static func round(_ acc: UInt64, _ input: UInt64) -> UInt64 {
        var acc = acc &+ input &* prime2
        acc = rotateLeft(acc, 31)
        acc &*= prime1
        return acc
    }

    private static func mergeRound(_ hash: UInt64, _ value: UInt64) -> UInt64 {
        let tmp = value ^ round(0, value)
        return hash ^ tmp
    }

    private static func finalize(_ hash: UInt64) -> UInt64 {
        var h = hash
        h ^= h >> 33
        h &*= prime2
        h ^= h >> 29
        h &*= prime3
        h ^= h >> 32
        return h
    }

    private static func rotateLeft(_ value: UInt64, _ count: UInt64) -> UInt64 {
        (value << count) | (value >> (64 - count))
    }

    private static func readUInt64(_ data: Data, _ offset: Int) -> UInt64 {
        var value: UInt64 = 0
        withUnsafeMutableBytes(of: &value) { dest in
            dest.copyBytes(from: data[offset..<(offset + 8)])
        }
        return value.littleEndian
    }

    private static func readUInt32(_ data: Data, _ offset: Int) -> UInt32 {
        var value: UInt32 = 0
        withUnsafeMutableBytes(of: &value) { dest in
            dest.copyBytes(from: data[offset..<(offset + 4)])
        }
        return value.littleEndian
    }
}

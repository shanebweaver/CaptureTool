#pragma once

#include <cstdint>

namespace CaptureInterop::V2
{
    inline constexpr int64_t MediaTicksPerSecond = 10'000'000;
    inline constexpr int64_t MediaTicksPerMillisecond = MediaTicksPerSecond / 1000;

    enum class MediaKind
    {
        Unknown = 0,
        Video,
        Audio
    };

    enum class SourceKind
    {
        Unknown = 0,
        Desktop,
        Window,
        Camera,
        Microphone,
        SystemAudio,
        File
    };

    struct SourceId
    {
        uint32_t value{ 0 };

        static constexpr SourceId Invalid() noexcept { return {}; }
        static constexpr SourceId FromValue(uint32_t value) noexcept { return SourceId{ value }; }

        [[nodiscard]] constexpr bool IsValid() const noexcept { return value != 0; }

        friend constexpr bool operator==(SourceId left, SourceId right) noexcept = default;
    };

    struct StreamId
    {
        uint32_t value{ 0 };

        static constexpr StreamId Invalid() noexcept { return {}; }
        static constexpr StreamId FromValue(uint32_t value) noexcept { return StreamId{ value }; }

        [[nodiscard]] constexpr bool IsValid() const noexcept { return value != 0; }

        friend constexpr bool operator==(StreamId left, StreamId right) noexcept = default;
    };

    struct Rational
    {
        uint32_t numerator{ 0 };
        uint32_t denominator{ 0 };

        static constexpr Rational Invalid() noexcept { return {}; }
        static constexpr Rational From(uint32_t numerator, uint32_t denominator) noexcept
        {
            return Rational{ numerator, denominator };
        }

        [[nodiscard]] constexpr bool IsValid() const noexcept
        {
            return numerator != 0 && denominator != 0;
        }

        friend constexpr bool operator==(Rational left, Rational right) noexcept = default;
    };

    struct MediaDuration
    {
        int64_t ticks100ns{ 0 };

        static constexpr MediaDuration Zero() noexcept { return {}; }
        static constexpr MediaDuration FromTicks(int64_t ticks100ns) noexcept
        {
            return MediaDuration{ ticks100ns };
        }

        static constexpr MediaDuration FromMilliseconds(int64_t milliseconds) noexcept
        {
            return MediaDuration{ milliseconds * MediaTicksPerMillisecond };
        }

        static constexpr MediaDuration FromSeconds(int64_t seconds) noexcept
        {
            return MediaDuration{ seconds * MediaTicksPerSecond };
        }

        [[nodiscard]] constexpr bool IsZero() const noexcept { return ticks100ns == 0; }
        [[nodiscard]] constexpr bool IsNegative() const noexcept { return ticks100ns < 0; }
        [[nodiscard]] constexpr bool IsPositive() const noexcept { return ticks100ns > 0; }

        friend constexpr bool operator==(MediaDuration left, MediaDuration right) noexcept = default;
    };

    [[nodiscard]] constexpr MediaDuration operator+(MediaDuration left, MediaDuration right) noexcept
    {
        return MediaDuration::FromTicks(left.ticks100ns + right.ticks100ns);
    }

    [[nodiscard]] constexpr MediaDuration operator-(MediaDuration left, MediaDuration right) noexcept
    {
        return MediaDuration::FromTicks(left.ticks100ns - right.ticks100ns);
    }

    [[nodiscard]] constexpr MediaDuration operator-(MediaDuration duration) noexcept
    {
        return MediaDuration::FromTicks(-duration.ticks100ns);
    }

    [[nodiscard]] constexpr bool operator<(MediaDuration left, MediaDuration right) noexcept
    {
        return left.ticks100ns < right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator<=(MediaDuration left, MediaDuration right) noexcept
    {
        return left.ticks100ns <= right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator>(MediaDuration left, MediaDuration right) noexcept
    {
        return left.ticks100ns > right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator>=(MediaDuration left, MediaDuration right) noexcept
    {
        return left.ticks100ns >= right.ticks100ns;
    }

    struct MediaTime
    {
        int64_t ticks100ns{ 0 };

        static constexpr MediaTime Zero() noexcept { return {}; }
        static constexpr MediaTime FromTicks(int64_t ticks100ns) noexcept
        {
            return MediaTime{ ticks100ns };
        }

        [[nodiscard]] constexpr bool IsZero() const noexcept { return ticks100ns == 0; }
        [[nodiscard]] constexpr bool IsNegative() const noexcept { return ticks100ns < 0; }

        friend constexpr bool operator==(MediaTime left, MediaTime right) noexcept = default;
    };

    [[nodiscard]] constexpr MediaTime operator+(MediaTime time, MediaDuration duration) noexcept
    {
        return MediaTime::FromTicks(time.ticks100ns + duration.ticks100ns);
    }

    [[nodiscard]] constexpr MediaTime operator+(MediaDuration duration, MediaTime time) noexcept
    {
        return time + duration;
    }

    [[nodiscard]] constexpr MediaTime operator-(MediaTime time, MediaDuration duration) noexcept
    {
        return MediaTime::FromTicks(time.ticks100ns - duration.ticks100ns);
    }

    [[nodiscard]] constexpr MediaDuration operator-(MediaTime left, MediaTime right) noexcept
    {
        return MediaDuration::FromTicks(left.ticks100ns - right.ticks100ns);
    }

    [[nodiscard]] constexpr bool operator<(MediaTime left, MediaTime right) noexcept
    {
        return left.ticks100ns < right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator<=(MediaTime left, MediaTime right) noexcept
    {
        return left.ticks100ns <= right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator>(MediaTime left, MediaTime right) noexcept
    {
        return left.ticks100ns > right.ticks100ns;
    }

    [[nodiscard]] constexpr bool operator>=(MediaTime left, MediaTime right) noexcept
    {
        return left.ticks100ns >= right.ticks100ns;
    }
}

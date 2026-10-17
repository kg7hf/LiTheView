// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/layout.hpp"

#include <cstddef>
#include <cstdint>
#include <span>

namespace lithe
{

enum class XmlWriteError : std::uint8_t
{
    none,
    empty,
    invalidHierarchy,
    missingAnimationFrames,
    nestingTooDeep,
    bufferTooSmall
};

struct XmlWriteOptions
{
    constexpr XmlWriteOptions() noexcept = default;

    constexpr explicit XmlWriteOptions(std::span<const std::string_view> animationFrames) noexcept
        : animationFrames{animationFrames}
    {
    }

    std::span<const std::string_view> animationFrames;
};

struct XmlWriteResult
{
    XmlWriteError error{XmlWriteError::none};
    std::size_t bytesWritten{0};
    constexpr bool ok() const noexcept
    {
        return error == XmlWriteError::none;
    }
    constexpr explicit operator bool() const noexcept
    {
        return ok();
    }
};

// Serializes a ViewSpec tree to the LCDViewGUI object dialect. The destination
// buffer is supplied by the caller and is always NUL terminated when capacity
// is non-zero. No allocation or I/O is performed.
class XmlLayoutWriter
{
public:
    static XmlWriteResult write(std::span<const ViewSpec> specs,
                                std::span<char> destination,
                                XmlWriteOptions options = {}) noexcept;
};

} // namespace lithe

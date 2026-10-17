// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/detail/index.hpp"
#include "lithe/pixel_format.hpp"

#include <cstddef>
#include <cstdint>
#include <expected>
#include <limits>

namespace lithe
{

enum class StorageError : std::uint8_t
{
    unsupportedFormat,
    invalidExtent,
    misalignedExtent,
    misalignedOrigin,
    invalidStride,
    arithmeticOverflow,
    insufficientStorage
};

struct StorageLayout
{
    std::size_t rowBytes{0U};
    std::size_t rowCount{0U};
    std::size_t strideBytes{0U};
    std::size_t usedBytes{0U};
};

// Extents/origins use logical pixels; strideBytes advances one storage-block
// row. Complete blocks, representable exclusive logical edges, and an explicit
// nonzero stride are required. Padding
// between rows is included in usedBytes; padding after the final row is not.
// This calculation requires no allocation or target profile. Surface/view
// binding separately checks whether the caller's span contains usedBytes.
[[nodiscard]] constexpr std::expected<StorageLayout, StorageError>
storageLayout(PixelFormat format, Size extent, std::size_t strideBytes, Point origin = {}) noexcept
{
    const StorageBlock block = storageBlock(format);
    if (block.width == 0 || block.height == 0 || block.bytes == 0U)
    {
        return std::unexpected{StorageError::unsupportedFormat};
    }
    if (extent.width <= 0 || extent.height <= 0)
    {
        return std::unexpected{StorageError::invalidExtent};
    }
    if (origin.x > std::numeric_limits<std::int32_t>::max() - extent.width ||
        origin.y > std::numeric_limits<std::int32_t>::max() - extent.height)
    {
        return std::unexpected{StorageError::arithmeticOverflow};
    }
    if (extent.width % block.width != 0 || extent.height % block.height != 0)
    {
        return std::unexpected{StorageError::misalignedExtent};
    }
    if (origin.x % block.width != 0 || origin.y % block.height != 0)
    {
        return std::unexpected{StorageError::misalignedOrigin};
    }

    const std::size_t columns = detail::nonnegativeSize(extent.width / block.width);
    const std::size_t rows = detail::nonnegativeSize(extent.height / block.height);
    std::size_t rowBytes = 0U;
    if (!detail::checkedMultiply(columns, block.bytes, rowBytes))
    {
        return std::unexpected{StorageError::arithmeticOverflow};
    }
    if (strideBytes < rowBytes)
    {
        return std::unexpected{StorageError::invalidStride};
    }
    std::size_t precedingBytes = 0U;
    if (!detail::checkedMultiply(rows - 1U, strideBytes, precedingBytes) ||
        rowBytes > std::numeric_limits<std::size_t>::max() - precedingBytes)
    {
        return std::unexpected{StorageError::arithmeticOverflow};
    }
    return StorageLayout{rowBytes, rows, strideBytes, precedingBytes + rowBytes};
}

} // namespace lithe

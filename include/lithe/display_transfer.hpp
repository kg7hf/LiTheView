// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/display.hpp"

#include <cstdint>

namespace lithe
{

enum class DisplayTransferEngine : std::uint8_t
{
    automaticSelection,
    cpu,
    dma,
    dma2d,
    ltdcLayer
};

struct DisplayTransferCapabilities
{
    bool dma{false};
    bool dma2d{false};
    bool ltdcLayers{false};
    bool asynchronous{false};
    std::uint8_t layerCount{1U};
};

struct DisplayTransferToken
{
    std::uint32_t value{0U};

    constexpr bool valid() const noexcept
    {
        return value != 0U;
    }
};

enum class DisplayTransferStatus : std::uint8_t
{
    completed,
    pending,
    failed,
    unknownToken
};

class DisplayTransferObserver
{
public:
    DisplayTransferObserver() = default;
    virtual ~DisplayTransferObserver() = default;

    DisplayTransferObserver(const DisplayTransferObserver&) = delete;
    DisplayTransferObserver& operator=(const DisplayTransferObserver&) = delete;
    DisplayTransferObserver(DisplayTransferObserver&&) = delete;
    DisplayTransferObserver& operator=(DisplayTransferObserver&&) = delete;

    virtual void transferCompleted(DisplayTransferToken token,
                                   DisplayTransferStatus status) noexcept = 0;
};

struct DisplayTransferRequest
{
    PixelBufferView source;
    Rect destination{};
    DisplayTransferEngine engine{DisplayTransferEngine::automaticSelection};
    std::uint8_t layer{0U};
    DisplayTransferObserver* observer{nullptr};
};

struct DisplayTransferResult
{
    DisplayTransferStatus status{DisplayTransferStatus::failed};
    DisplayTransferToken token{};

    constexpr bool accepted() const noexcept
    {
        return status == DisplayTransferStatus::completed ||
               (status == DisplayTransferStatus::pending && token.valid());
    }
};

// This optional platform boundary is not used by the portable renderer. A
// completed submission consumed or copied source before returning. For a
// pending submission, source and observer (when supplied) must remain valid
// until status() or transferCompleted() reports completed or failed. status()
// is non-blocking; callback execution context is owned by the adapter.
class DisplayTransferAdapter
{
public:
    DisplayTransferAdapter() = default;
    virtual ~DisplayTransferAdapter() = default;

    DisplayTransferAdapter(const DisplayTransferAdapter&) = delete;
    DisplayTransferAdapter& operator=(const DisplayTransferAdapter&) = delete;
    DisplayTransferAdapter(DisplayTransferAdapter&&) = delete;
    DisplayTransferAdapter& operator=(DisplayTransferAdapter&&) = delete;

    virtual DisplayTransferCapabilities capabilities() const noexcept = 0;
    virtual DisplayTransferResult submit(const DisplayTransferRequest& request) noexcept = 0;
    virtual DisplayTransferStatus status(DisplayTransferToken token) const noexcept = 0;
};

} // namespace lithe

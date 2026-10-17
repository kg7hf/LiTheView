// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/display.hpp"

namespace lithe
{

namespace
{

constexpr bool touchesOrOverlaps(Rect lhs, Rect rhs) noexcept
{
    return lhs.x <= detail::saturatingAdd(rhs.right(), 1) &&
           detail::saturatingAdd(lhs.right(), 1) >= rhs.x &&
           lhs.y <= detail::saturatingAdd(rhs.bottom(), 1) &&
           detail::saturatingAdd(lhs.bottom(), 1) >= rhs.y;
}

} // namespace

void DamageTracker::add(Rect region) noexcept
{
    if (region.empty())
    {
        return;
    }

    for (std::size_t index = 0; index < mCount; ++index)
    {
        if (touchesOrOverlaps(mRegions[index], region))
        {
            mRegions[index] = unite(mRegions[index], region);
            for (std::size_t other = index + 1U; other < mCount;)
            {
                if (touchesOrOverlaps(mRegions[index], mRegions[other]))
                {
                    mRegions[index] = unite(mRegions[index], mRegions[other]);
                    mRegions[other] = mRegions[mCount - 1U];
                    --mCount;
                }
                else
                {
                    ++other;
                }
            }
            return;
        }
    }

    if (mCount < mRegions.size())
    {
        mRegions[mCount++] = region;
        return;
    }

    Rect combined = region;
    for (std::size_t index = 0; index < mCount; ++index)
    {
        combined = unite(combined, mRegions[index]);
    }
    mRegions[0] = combined;
    mCount = 1U;
}

} // namespace lithe

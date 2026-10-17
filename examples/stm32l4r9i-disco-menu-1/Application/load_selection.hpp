// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <cstdint>
#include <optional>
#include <string_view>
namespace menu
{
enum class EntryId : std::uint8_t
{
    sessionA,
    sessionB,
    demoLoop,
    recording1,
    recording2,
    rehearsal,
    backup,
    factory
};
// Fixed demo catalog: selection and loading are deliberately different operations.
class LoadSelection final
{
public:
    [[nodiscard]] EntryId selected() const noexcept
    {
        return mSelected;
    }
    [[nodiscard]] std::optional<EntryId> loaded() const noexcept
    {
        return mLoaded;
    }
    void select(EntryId id) noexcept
    {
        mSelected = id;
    }
    void load() noexcept
    {
        mLoaded = mSelected;
    }
    [[nodiscard]] std::string_view loadedText() const noexcept
    {
        if (!mLoaded)
            return "LOADED: NONE";
        switch (*mLoaded)
        {
        case EntryId::sessionA:
            return "LOADED: SESSION A";
        case EntryId::sessionB:
            return "LOADED: SESSION B";
        case EntryId::demoLoop:
            return "LOADED: DEMO LOOP";
        case EntryId::recording1:
            return "LOADED: RECORDING 1";
        case EntryId::recording2:
            return "LOADED: RECORDING 2";
        case EntryId::rehearsal:
            return "LOADED: REHEARSAL";
        case EntryId::backup:
            return "LOADED: BACKUP";
        case EntryId::factory:
            return "LOADED: FACTORY";
        }
        return "LOADED: NONE";
    }

private:
    EntryId mSelected{EntryId::sessionA};
    std::optional<EntryId> mLoaded;
};
} // namespace menu

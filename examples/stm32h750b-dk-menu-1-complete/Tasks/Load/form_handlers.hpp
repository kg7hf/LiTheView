// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include "Application/load_selection.hpp"
#include "Application/navigation.hpp"
#include "Controls/selection_list.hpp"
#include "Generated/form.hpp"

#include <array>
namespace menu
{
using LoadFormBase = lithe::generated::LoadMenu::Instance<
    lithe::WidgetOverrides<lithe::generated::LoadMenu::EntryListType<controls::SelectionList>>>;
class LoadMenuForm final : public LoadFormBase, private controls::ListEvents
{
public:
    explicit LoadMenuForm(Requests& requests) noexcept : mRequests{requests}
    {
    }
    [[nodiscard]] bool connect() noexcept
    {
        auto* list = getEntryList();
        if (list == nullptr || getLoadedLabel() == nullptr ||
            !list->configure(kEntries, lithe::font5x7(), *this))
            return false;
        refresh();
        return true;
    }
    [[nodiscard]] EntryId selectedEntry() const noexcept
    {
        return mSelection.selected();
    }
    [[nodiscard]] std::optional<EntryId> loadedEntry() const noexcept
    {
        return mSelection.loaded();
    }

protected:
    // <litheview-handlers>
    // litheview-handler component.main.load onLoadButtonClicked
    void onLoadButtonClicked() noexcept override
    {
        loadSelected();
    }
    // litheview-handler component.main.back onBackButtonClicked
    void onBackButtonClicked() noexcept override
    {
        mRequests.offer(Event::back);
    }
    // </litheview-handlers>
    void onBack(lithe::ViewController&) noexcept override
    {
        mRequests.offer(Event::back);
    }

private:
    void onListSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        if (event.index < kIds.size())
            mSelection.select(kIds[event.index]);
    }
    void onListActivated(std::size_t index) noexcept override
    {
        if (index < kIds.size())
        {
            mSelection.select(kIds[index]);
            loadSelected();
        }
    }
    void loadSelected() noexcept
    {
        mSelection.load();
        refresh();
    }
    void refresh() noexcept
    {
        if (auto* label = getLoadedLabel(); label != nullptr)
            label->setText(mSelection.loadedText());
    }
    inline static constexpr std::array<std::string_view, 8U> kEntries{"SESSION A",
                                                                      "SESSION B",
                                                                      "DEMO LOOP",
                                                                      "RECORDING 1",
                                                                      "RECORDING 2",
                                                                      "REHEARSAL",
                                                                      "BACKUP",
                                                                      "FACTORY"};
    inline static constexpr std::array<EntryId, 8U> kIds{EntryId::sessionA,
                                                         EntryId::sessionB,
                                                         EntryId::demoLoop,
                                                         EntryId::recording1,
                                                         EntryId::recording2,
                                                         EntryId::rehearsal,
                                                         EntryId::backup,
                                                         EntryId::factory};
    Requests& mRequests;
    LoadSelection mSelection;
};
} // namespace menu

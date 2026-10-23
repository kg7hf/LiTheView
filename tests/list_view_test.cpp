// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "designer-fixtures/selectable-list/form.hpp"
#include "lithe/controller.hpp"
#include "lithe/list_view.hpp"
#include "lithe/xml_layout.hpp"
#include "lithe/xml_writer.hpp"

#include <array>
#include <cstdio>
#include <string>
#include <string_view>

namespace
{
int failures{};
void check(bool condition, const char* message)
{
    if (!condition)
    {
        std::printf("FAIL: %s\n", message);
        ++failures;
    }
}
class List final : public lithe::ListView
{
public:
    using ListView::ListView;
    int hooks{};
    int activations{};

protected:
    void onSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        ++hooks;
        check(event.index == selectedIndex(), "selection settled before control hook");
        check(!setSelectedIndex(0U), "callback cannot recursively replace selection");
    }
    void onItemActivated(lithe::ItemActivation event) noexcept override
    {
        ++activations;
        check(event.index == selectedIndex(), "activation reports selected item");
        check(!setItems({}), "callback cannot replace borrowed items");
    }
};
class Events final : public lithe::ControllerDelegate
{
public:
    explicit Events(List& list) noexcept : mList(list)
    {
    }
    int changes{};
    int activations{};
    std::size_t lastIndex{};
    void onAction(lithe::ViewController&, lithe::ViewId) noexcept override
    {
    }
    void onBack(lithe::ViewController&) noexcept override
    {
    }
    void onControlSelectionChanged(lithe::ViewController& controller,
                                   lithe::View& source,
                                   lithe::SelectionChange event) noexcept override
    {
        ++changes;
        lastIndex = event.index;
        check(&source == &mList && mList.hooks == changes, "typed form event follows hook");
        check(!controller.handleKey(lithe::Key::select), "nested controller dispatch rejected");
    }
    void onControlItemActivated(lithe::ViewController&,
                                lithe::View& source,
                                lithe::ItemActivation event) noexcept override
    {
        ++activations;
        lastIndex = event.index;
        check(&source == &mList && mList.activations == activations,
              "typed activation follows hook");
    }

private:
    List& mList;
};
void behavior()
{
    constexpr std::array<std::string_view, 8U> entries{
        "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT"};
    lithe::View root{{0, 0, 240, 160}};
    List list{{10, 10, 200, 108}, lithe::font5x7(), {}, 0U, 36U};
    check(root.addSubview(list), "list attached");
    check(list.setItems(entries), "application-owned entries attached");
    Events events{list};
    lithe::ViewController controller{root, events};
    controller.activate();
    check(controller.focusedView() == &list, "list focuses");
    for (int n = 0; n < 10; ++n)
        (void)controller.handleKey(lithe::Key::down);
    check(list.selectedIndex() == 7U && list.firstVisibleIndex() == 5U && events.changes == 7 &&
              events.activations == 0,
          "bounded keyboard selection scrolls hidden items without activation");
    check(controller.handleKey(lithe::Key::select) && events.activations == 1 &&
              events.lastIndex == 7U,
          "select activates only current item");
    check(controller.handlePointer(lithe::Point{20, 24}) && list.selectedIndex() == 5U &&
              events.activations == 1,
          "touch chooses visible row without activation");
    check(controller.handleKey(lithe::Key::select) && events.activations == 2 &&
              events.lastIndex == 5U,
          "select after touch activates chosen row");
    check(list.setSelectedIndex(1U) && list.firstVisibleIndex() == 1U && events.changes == 8,
          "programmatic selection is silent and visible");
    constexpr std::array<std::string_view, 2U> shorter{"A", "B"};
    check(list.setItems(shorter) && list.selectedIndex() == 1U && events.changes == 8,
          "replacement clamps and remains silent");
    constexpr std::array<std::string_view, 65U> oversized{};
    check(!list.setItems(oversized) && list.itemCount() == 2U,
          "oversized replacement rejected without changing current list");
    const std::string tooManyPipes(64U, '|');
    List invalid{{10, 10, 200, 108}, lithe::font5x7(), tooManyPipes};
    check(invalid.itemCount() == 0U, "oversized constructor catalog fails closed");
    list.setEnabled(false);
    check(!controller.handleKey(lithe::Key::select) && events.activations == 2,
          "disabled list cannot activate");
    list.setEnabled(true);
    check(list.setItems({}) && list.itemCount() == 0U && !list.setSelectedIndex(0U),
          "empty list has no selection");
}
void pointerScrolling()
{
    constexpr std::array<std::string_view, 6U> entries{
        "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX"};
    lithe::View root{{0, 0, 240, 160}};
    List list{{10, 10, 200, 108}, lithe::font5x7(), {}, 0U, 36U};
    check(root.addSubview(list) && list.setItems(entries), "touch list attached");
    Events events{list};
    lithe::ViewController controller{root, events};
    controller.activate();
    for (int n = 0; n < 8; ++n)
        (void)controller.handlePointer(lithe::Point{195, 100});
    check(list.selectedIndex() == 5U && list.firstVisibleIndex() == 3U && events.changes == 5 &&
              events.activations == 0,
          "touch scroll exposes hidden rows without activating");
    check(controller.handlePointer(lithe::Point{20, 24}) && list.selectedIndex() == 3U &&
              events.activations == 0,
          "tap maps scrolled row to stable index");
    (void)controller.handlePointer({{20, 24}, lithe::PointerPhase::pressed, 0U});
    (void)controller.handlePointer({{20, 64}, lithe::PointerPhase::moved, 0U});
    (void)controller.handlePointer({{20, 64}, lithe::PointerPhase::released, 0U});
    check(list.selectedIndex() == 3U && events.activations == 0,
          "drag between rows cancels the selection");
    (void)controller.handlePointer({{20, 24}, lithe::PointerPhase::pressed, 0U});
    (void)controller.cancelPointerCapture();
    check(list.selectedIndex() == 3U && events.activations == 0,
          "pointer cancellation does not activate");
    check(controller.handleKey(lithe::Key::select) && events.activations == 1 &&
              events.lastIndex == 3U,
          "center activates scrolled selection");
}
void rendering()
{
    lithe::View root{{0, 0, 160, 120}};
    const lithe::Style style{.background = lithe::Color::rgb(0x101010U),
                             .foreground = lithe::Color::rgb(0xffffffU),
                             .accent = lithe::Color::rgb(0x0040ffU)};
    List list{{10, 10, 120, 84}, lithe::font5x7(), "ONE|TWO|THREE|FOUR", 0U, 28U, style};
    check(root.addSubview(list), "render list attached");
    lithe::ViewController controller{root};
    controller.activate();
    std::array<lithe::Pixel, 160U * 120U> pixels{};
    lithe::DamageTracker damage;
    lithe::Painter painter{{pixels, 160, 120, 160}, damage};
    root.render(painter, {}, true);
    const auto first = pixels;
    check(controller.handleKey(lithe::Key::down), "render list selection moves");
    root.render(painter);
    const auto incremental = pixels;
    check(incremental != first, "selection highlight changes native pixels");
    root.render(painter, {}, true);
    check(pixels == incremental, "incremental list repaint matches full repaint");
}
class GeneratedLoad final : public lithe::generated::Load::Instance<>
{
public:
    int selections{};
    int activations{};

protected:
    void onEntryListSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        ++selections;
        check(event.index == getEntryList()->selectedIndex(), "generated selection reaches form");
    }
    void onEntryListItemActivated(lithe::ItemActivation event) noexcept override
    {
        ++activations;
        getLoadedLabel()->setText(getEntryList()->item(event.index));
    }
};
void generatedBehavior()
{
    GeneratedLoad form;
    check(form.build().ok(), "designer-generated list form builds");
    auto* controller = form.controller();
    check(controller != nullptr, "generated controller exists");
    if (controller == nullptr)
        return;
    controller->activate();
    check(controller->handleKey(lithe::Key::down) && form.selections == 1,
          "generated typed selection dispatch");
    check(controller->handleKey(lithe::Key::select) && form.activations == 1 &&
              form.getLoadedLabel()->text() == "TWO",
          "generated typed activation drives label");
}
void xmlBehavior()
{
    constexpr std::string_view source =
        "<panel frame=\"0,0,240,160\"><list frame=\"10,10,200,108\" "
        "items=\"ONE|TWO|THREE|FOUR\" selectedIndex=\"3\" rowHeight=\"36\"/></panel>";
    std::array<lithe::ViewSpec, 3U> specs{};
    check(lithe::XmlLayoutParser::parse(source, specs, lithe::XmlParseOptions{true}).ok() &&
              specs[1].kind == lithe::ViewKind::list && specs[1].selectedIndex == 3U,
          "strict XML accepts bounded list");
    std::array<char, 2048U> output{};
    const auto written = lithe::XmlLayoutWriter::write(std::span{specs}.first(2U), output);
    std::array<lithe::ViewSpec, 3U> roundtrip{};
    check(written.ok() &&
              std::string_view{output.data(), written.bytesWritten}.find("wrap=") ==
                  std::string_view::npos &&
              lithe::XmlLayoutParser::parse(
                  {output.data(), written.bytesWritten}, roundtrip, lithe::XmlParseOptions{true})
                  .ok() &&
              roundtrip[1].kind == lithe::ViewKind::list && roundtrip[1].rowHeight == 36U,
          "list XML round trip retains selection and row height");
    constexpr std::string_view invalid =
        "<panel frame=\"0,0,240,160\"><list frame=\"10,10,200,108\" "
        "items=\"ONE|TWO\" selectedIndex=\"2\"/></panel>";
    check(!lithe::XmlLayoutParser::parse(invalid, specs, lithe::XmlParseOptions{true}),
          "strict XML rejects out-of-range selection");
}
} // namespace
int main()
{
    behavior();
    pointerScrolling();
    rendering();
    generatedBehavior();
    xmlBehavior();
    if (failures != 0)
        return 1;
    std::puts("PASS: bounded selectable list, typed selection and activation, silent refresh");
    return 0;
}

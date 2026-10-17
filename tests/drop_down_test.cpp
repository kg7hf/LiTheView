// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "designer-fixtures/drop-down-events/form.hpp"
#include "lithe/controller.hpp"
#include "lithe/layout.hpp"
#include "lithe/xml_layout.hpp"
#include "lithe/xml_writer.hpp"

#include <array>
#include <cstdio>
#include <limits>
#include <string>

namespace
{
int failures{0};
void check(bool condition, const char* message)
{
    if (!condition)
    {
        std::printf("FAIL: %s\n", message);
        ++failures;
    }
}
class Selector : public lithe::DropDownView
{
public:
    using DropDownView::DropDownView;
    std::string order;
    bool disableOnChange{false};
    bool disableParentOnChange{false};

protected:
    void onSelectionChanged(lithe::SelectionChange event) noexcept override
    {
        order += 'c';
        check(event.index == selectedIndex(), "selection is settled before control hook");
        check(!setSelectedIndex(0U) && !beginEdit() && !commitEdit() && !cancelEdit(),
              "callback cannot recursively mutate the selection transaction");
        if (disableOnChange)
        {
            setEnabled(false);
        }
        if (disableParentOnChange && parent() != nullptr)
        {
            parent()->setEnabled(false);
        }
    }
    void onEditCommitted(lithe::SelectionEdit event) noexcept override
    {
        order += 'a';
        check(!editing() && event.index == selectedIndex(), "commit is settled before hook");
    }
    void onEditCancelled(lithe::SelectionEdit event) noexcept override
    {
        order += 'x';
        check(!editing() && event.index == event.initialIndex && event.index == selectedIndex(),
              "cancel restores original selection before hook");
    }
};
class Delegate : public lithe::ControllerDelegate
{
public:
    explicit Delegate(Selector& selector) : mSelector(selector)
    {
    }
    int commits{0};
    int cancels{0};
    int actions{0};
    void onAction(lithe::ViewController&, lithe::ViewId) noexcept override
    {
        ++actions;
    }
    void onBack(lithe::ViewController&) noexcept override
    {
    }
    void onControlSelectionChanged(lithe::ViewController& controller,
                                   lithe::View& source,
                                   lithe::SelectionChange event) noexcept override
    {
        check(&source == &mSelector && event.index == mSelector.selectedIndex(),
              "form source and value");
        check(!controller.handleKey(lithe::Key::select), "controller rejects recursive input");
        mSelector.order += 'C';
    }
    void onControlEditCommitted(lithe::ViewController&,
                                lithe::View&,
                                lithe::SelectionEdit) noexcept override
    {
        ++commits;
        mSelector.order += 'A';
    }
    void onControlEditCancelled(lithe::ViewController&,
                                lithe::View&,
                                lithe::SelectionEdit) noexcept override
    {
        ++cancels;
        mSelector.order += 'X';
    }

private:
    Selector& mSelector;
};
void behavior()
{
    lithe::View root{{0, 0, 160, 180}};
    Selector selector{{10, 10, 100, 20}, lithe::font5x7(), "AUTO|MANUAL|OFF", 0U, 20U, false};
    lithe::ButtonView other{{10, 130, 100, 20}, lithe::font5x7(), "OTHER"};
    check(root.addSubview(selector) && root.addSubview(other), "attach controls");
    Delegate events{selector};
    lithe::ViewController controller{root, events};
    controller.activate();
    check(controller.focusedView() == &selector, "initial focus");
    check(controller.handleKey(lithe::Key::select) && selector.editing(), "select opens popup");
    check(selector.popupFrame().y == 30 && selector.popupFrame().height == 60, "popup fits below");
    check(controller.handleKey(lithe::Key::down) && selector.selectedIndex() == 1U,
          "open navigation changes tentative value");
    check(events.commits == 0 && selector.order == "cC", "hook precedes form; no implicit commit");
    controller.handleKey(lithe::Key::back);
    check(selector.selectedIndex() == 0U && !selector.editing() && selector.order == "cCcCxX",
          "back restores, then reports cancellation");
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::up);
    check(selector.selectedIndex() == 0U && selector.order == "cCcCxX", "clamped edge is silent");
    controller.handleKey(lithe::Key::select);
    check(events.commits == 1 && selector.order.ends_with("aA"),
          "unchanged explicit commit reports once");
    selector.order.clear();
    check(controller.handlePointer(lithe::Point{20, 20}) && selector.editing(),
          "pointer opens closed selector");
    check(root.hitTestPointer({20, 60}) == &selector, "popup hit outside closed frame");
    check(controller.handlePointer(lithe::Point{20, 60}), "pointer commits row");
    check(selector.selectedIndex() == 1U && !selector.editing() && selector.order == "cCaA",
          "row tap changes and commits exactly once");
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    const int previousActions = events.actions;
    check(controller.handlePointer({{20, 140}, lithe::PointerPhase::pressed, 3U}),
          "outside press consumed");
    controller.handlePointer({{20, 140}, lithe::PointerPhase::released, 3U});
    check(!selector.editing() && selector.selectedIndex() == 1U &&
              events.actions == previousActions,
          "outside click cancels without clicking through");
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    const auto beforeRefresh = selector.order;
    check(selector.setSelectedIndex(0U) && !selector.editing() && selector.order == beforeRefresh,
          "application refresh closes popup silently");
    check(!selector.setSelectedIndex(99U) && selector.selectedIndex() == 0U, "bad index rejected");
    controller.handleKey(lithe::Key::select);
    selector.disableOnChange = true;
    const int beforeCancels = events.cancels;
    controller.handleKey(lithe::Key::down);
    check(!selector.editing() && selector.selectedIndex() == 0U &&
              events.cancels == beforeCancels + 1,
          "self-disable cancels once after changed callbacks");
    selector.disableOnChange = false;
    selector.setEnabled(true);
    controller.activate();
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    root.setEnabled(false);
    check(controller.focusedView() == nullptr && !selector.editing() &&
              selector.selectedIndex() == 0U,
          "disabled ancestor cancels retained edit");
    root.setEnabled(true);
    controller.activate();
    selector.setFrame({10, 150, 100, 20});
    controller.handleKey(lithe::Key::select);
    check(selector.popupFrame().y == 90, "near bottom popup opens upward");
    controller.handlePointer({{20, 100}, lithe::PointerPhase::pressed, 1U});
    controller.handlePointer({{20, 100}, lithe::PointerPhase::cancelled, 1U});
    check(!selector.editing(), "cancelled touch closes popup without commit");
    controller.handleKey(lithe::Key::select);
    selector.setFocused(false);
    check(!selector.editing(), "direct focus loss cancels");
    // Restore the focus flag changed directly above; the controller still owns this focus.
    selector.setFocused(true);
    selector.setFrame({10, 10, 100, 20});
    controller.activate();
    controller.handlePointer(lithe::Point{20, 20});
    check(selector.editing(), "pointer reopens before ancestor-disable callback");
    const int actionsBeforeDisable = events.actions;
    const int commitsBeforeDisable = events.commits;
    selector.disableParentOnChange = true;
    controller.handlePointer(lithe::Point{20, 60});
    check(!selector.editing() && !root.enabled() && events.actions == actionsBeforeDisable &&
              events.commits == commitsBeforeDisable,
          "ancestor disabled by release callback cannot receive a trailing activation/action");
}
void scrollingAndBounds()
{
    lithe::View root{{0, 0, 180, 200}};
    lithe::DropDownView selector{
        {10, 10, 140, 20}, lithe::font5x7(), "0|1|2|3|4|5|6|7|8|9", 0U, 20U};
    root.addSubview(selector);
    lithe::ViewController controller{root};
    controller.activate();
    controller.handleKey(lithe::Key::select);
    check(selector.popupFrame().height == 100, "visible rows are bounded to five");
    controller.handlePointer({{20, 110}, lithe::PointerPhase::pressed, 1U});
    controller.handlePointer({{20, 50}, lithe::PointerPhase::moved, 1U});
    // Render while dragging: drawing must not move the viewport back to selection.
    std::array<lithe::Pixel, 180U * 200U> pixels{};
    lithe::DamageTracker damage;
    lithe::Painter painter{{pixels, 180, 200, 180}, damage};
    root.render(painter);
    controller.handlePointer({{20, 50}, lithe::PointerPhase::released, 1U});
    check(selector.editing() && selector.selectedIndex() == 0U, "drag scroll does not commit");
    controller.handlePointer(lithe::Point{20, 40});
    check(!selector.editing() && selector.selectedIndex() == 3U,
          "scrolled row keeps immutable item identity");
    selector.setSelectedIndex(9U);
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    check(selector.selectedIndex() == 0U, "wrap navigates from final item to first");
    controller.handleKey(lithe::Key::back);
    check(selector.selectedIndex() == 9U, "wrapped edit cancellation restores final item");
    std::string tooMany(255U, '|');
    lithe::DropDownView invalid{{}, lithe::font5x7(), tooMany};
    lithe::DropDownView badIndex{{}, lithe::font5x7(), "ONE", 2U};
    lithe::DropDownView empty{{}, lithe::font5x7(), ""};
    check(invalid.itemCount() == 0U && badIndex.itemCount() == 0U && empty.itemCount() == 0U,
          "invalid direct models remain inert rather than silently truncating");
    check(!empty.beginEdit() && !empty.handleKey(lithe::Key::select) && empty.item(999U).empty(),
          "empty input and lookup fail safely");
    root.setFrame({0, 0, 180, 30});
    check(!selector.beginEdit(), "no complete popup row means open is rejected");
    root.setFrame({0, 0, 180, 200});
    controller.handleKey(lithe::Key::select);
    controller.handlePointer({{20, 40}, lithe::PointerPhase::pressed, 2U});
    controller.handlePointer(
        {{20, std::numeric_limits<std::int32_t>::min()}, lithe::PointerPhase::moved, 2U});
    controller.handlePointer({{20, 40}, lithe::PointerPhase::cancelled, 2U});
    check(!selector.editing(), "extreme coordinates are bounded and cancellable");
}
void rendering()
{
    const lithe::Style background{.background = lithe::Color::rgb(0x113355U)};
    const lithe::Style fieldStyle{.background = lithe::Color::rgb(0x000000U),
                                  .foreground = lithe::Color::rgb(0xffffffU)};
    lithe::View root{{0, 0, 160, 160}, background};
    lithe::View parent{{10, 10, 110, 24}};
    lithe::DropDownView selector{
        {0, 0, 100, 20}, lithe::font5x7(), "AUTO|MANUAL|OFF", 0U, 20U, true, fieldStyle};
    lithe::View sibling{{0, 35, 160, 100}, {.background = lithe::Color::rgb(0xff0000U)}};
    root.addSubview(parent);
    parent.addSubview(selector);
    root.addSubview(sibling);
    lithe::ViewController controller{root};
    controller.activate();
    std::array<lithe::Pixel, 160U * 160U> pixels{};
    lithe::DamageTracker damage;
    lithe::Painter painter{{pixels, 160, 160, 160}, damage};
    root.render(painter, {}, true);
    const auto closed = pixels;
    controller.handleKey(lithe::Key::select);
    root.render(painter);
    const auto opened = pixels;
    check(opened != closed && pixels[50U * 160U + 90U] != closed[50U * 160U + 90U],
          "popup paints over later sibling and outside parent clip");
    check(root.hitTestPointer({90, 50}) == &selector,
          "popup hit testing matches its drawing order");
    controller.handleKey(lithe::Key::down);
    root.render(painter);
    const auto incremental = pixels;
    root.render(painter, {}, true);
    check(pixels == incremental, "incremental selection repaint matches a full repaint");
    controller.handleKey(lithe::Key::back);
    root.render(painter);
    check(pixels == closed, "cancel erases popup and restores covered siblings and closed field");
    controller.handleKey(lithe::Key::select);
    root.render(painter);
    parent.setVisible(false);
    root.render(painter);
    const auto hidden = pixels;
    root.render(painter, {}, true);
    check(hidden == pixels, "hiding an ancestor erases popup without waiting for new input");
    check(controller.focusedView() == nullptr && !selector.editing(),
          "hidden ancestor clears edit at next dispatch");

    parent.setVisible(true);
    controller.activate();
    controller.handleKey(lithe::Key::select);
    root.render(painter);
    parent.setFrame({40, 80, 110, 24});
    root.render(painter);
    const auto moved = pixels;
    root.render(painter, {}, true);
    check(moved == pixels, "moving a popup's ancestor repaints both old and new popup locations");

    lithe::View scrollRoot{{0, 0, 160, 160}, background};
    lithe::ScrollView scroll{{10, 10, 110, 60}, 200, fieldStyle};
    lithe::DropDownView scrollingSelector{{0, 20, 100, 20}, lithe::font5x7(), "A|B|C", 0U, 20U};
    scrollRoot.addSubview(scroll);
    scroll.addSubview(scrollingSelector);
    lithe::ViewController scrollController{scrollRoot};
    scrollController.activate();
    scrollController.handleKey(lithe::Key::select);
    scrollRoot.render(painter, {}, true);
    scroll.setContentOffset(10);
    scrollRoot.render(painter);
    const auto scrolled = pixels;
    scrollRoot.render(painter, {}, true);
    check(scrolled == pixels, "scrolling under an open popup erases the previous popup region");
    scroll.setContentOffset(80);
    scrollRoot.render(painter);
    const auto scrolledOff = pixels;
    scrollRoot.render(painter, {}, true);
    check(scrolledOff == pixels && scrollingSelector.popupFrame().empty(),
          "fully clipped anchor has no popup pixels");
}
void xmlAndGeneratedHandlers()
{
    constexpr std::string_view source =
        "<panel frame=\"0,0,160,160\"><drop-down frame=\"4,4,100,24\" items=\"A|B|C\" "
        "selectedIndex=\"1\" rowHeight=\"20\" wrap=\"0\"/></panel>";
    std::array<lithe::ViewSpec, 4> specs{};
    check(lithe::XmlLayoutParser::parse(source, specs, lithe::XmlParseOptions{true}).ok(),
          "strict XML accepts drop-down");
    check(specs[1].kind == lithe::ViewKind::dropDown && specs[1].rowHeight == 20U &&
              specs[1].selectedIndex == 1U && !specs[1].wraps,
          "XML properties retain native types");
    std::array<char, 2048> output{};
    const auto written = lithe::XmlLayoutWriter::write(std::span{specs}.first(2U), output);
    check(written.ok(), "drop-down writes legacy XML dialect");
    std::array<lithe::ViewSpec, 4> roundtrip{};
    check(lithe::XmlLayoutParser::parse(
              {output.data(), written.bytesWritten}, roundtrip, lithe::XmlParseOptions{true})
                  .ok() &&
              roundtrip[1].kind == lithe::ViewKind::dropDown && roundtrip[1].text == "A|B|C" &&
              roundtrip[1].selectedIndex == 1U && roundtrip[1].rowHeight == 20U,
          "XML round trip");
    for (const auto* attributes :
         {"items=\"\"", "items=\"A\" selectedIndex=\"1\"", "items=\"A\" rowHeight=\"11\""})
    {
        const std::string invalid =
            std::string{"<panel frame=\"0,0,160,160\"><dropdown frame=\"4,4,100,24\" "} +
            attributes + "/></panel>";
        check(!lithe::XmlLayoutParser::parse(invalid, specs, lithe::XmlParseOptions{true}),
              "strict XML rejects invalid drop-down data");
    }
    class Form : public lithe::generated::ModeSelection::Instance<>
    {
    public:
        std::size_t applied{0U};
        int changes{0};
        int commits{0};
        int cancels{0};

    protected:
        void onModeSelectionChanged([[maybe_unused]] lithe::SelectionChange event) noexcept override
        {
            ++changes;
        }
        void onModeEditCommitted(lithe::SelectionEdit event) noexcept override
        {
            applied = event.index;
            ++commits;
            // Immutable generated item storage outlives the label's borrowed text.
            getApplied()->setText(getMode()->item(applied));
        }
        void onModeEditCancelled([[maybe_unused]] lithe::SelectionEdit event) noexcept override
        {
            ++cancels;
        }
    } form;
    check(form.build(lithe::font5x7()).ok(), "generated drop-down consumer builds");
    auto& controller = *form.controller();
    controller.activate();
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    check(form.applied == 0U && form.getApplied()->text() == "AUTO" && form.changes == 1,
          "generated changed handler leaves backend untouched until commit");
    controller.handleKey(lithe::Key::select);
    check(form.applied == 1U && form.getApplied()->text() == "MANUAL" && form.commits == 1,
          "generated commit handler updates backend and label");
    controller.handleKey(lithe::Key::select);
    controller.handleKey(lithe::Key::down);
    controller.handleKey(lithe::Key::back);
    check(form.applied == 1U && form.getMode()->selectedIndex() == 1U && form.cancels == 1,
          "generated cancellation preserves applied backend");
}
} // namespace
int main()
{
    behavior();
    scrollingAndBounds();
    rendering();
    xmlAndGeneratedHandlers();
    std::printf("Drop-down: %d failures\n", failures);
    return failures == 0 ? 0 : 1;
}

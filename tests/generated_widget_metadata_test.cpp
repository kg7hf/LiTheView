// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "generated_widget_metadata_test.hpp"

#include "lithe/designer/generated_widget_metadata.hpp"
#include "lithe/layout.hpp"

#include <array>
#include <cstddef>
#include <cstdint>
#include <string_view>
#include <utility>

namespace lithe::tests
{
namespace
{

constexpr std::size_t kExpectedWidgetCount{25U};
constexpr std::size_t kExpectedPropertyCount{49U};
constexpr std::int32_t kSupportedSchemaVersion{1};
constexpr std::string_view kExpectedCatalogHash{
    "0a62fd6a3f77b871d0c3a1e40c1cd6a5210ac4f1c3647b542fc3487d8ea03016"};

template <ViewKind... Kinds> constexpr bool distinctKinds(WidgetSet<Kinds...> /*unused*/) noexcept
{
    constexpr std::array<ViewKind, sizeof...(Kinds)> kinds{Kinds...};
    for (std::size_t first = 0U; first < kinds.size(); ++first)
    {
        for (std::size_t second = first + 1U; second < kinds.size(); ++second)
        {
            if (kinds[first] == kinds[second])
            {
                return false;
            }
        }
    }
    return true;
}

constexpr bool sliceFits(designer::GeneratedSlice slice, std::size_t size) noexcept
{
    return slice.offset <= size && slice.count <= size - slice.offset;
}

} // namespace

bool generatedWidgetMetadataValid() noexcept
{
    static_assert(designer::kWidgets.size() == kExpectedWidgetCount);
    static_assert(designer::kWidgetProperties.size() == kExpectedPropertyCount);
    static_assert(std::to_underlying(ViewKind::meter) + 1U == kExpectedWidgetCount);
    // The default layout set names every kind: as many distinct kinds as ViewKind has.
    static_assert(AllWidgets::size == kExpectedWidgetCount && distinctKinds(AllWidgets{}));

    bool slicesValid = true;
    for (const designer::GeneratedWidgetDescriptor& widget : designer::kWidgets)
    {
        slicesValid = slicesValid && !widget.id.empty() && !widget.runtimeKind.empty() &&
                      !widget.cppType.empty() &&
                      sliceFits(widget.xmlElementAliases, designer::kWidgetStrings.size()) &&
                      sliceFits(widget.legacyClassAliases, designer::kWidgetStrings.size()) &&
                      sliceFits(widget.allowedParentIds, designer::kWidgetStrings.size()) &&
                      sliceFits(widget.propertyIds, designer::kWidgetStrings.size()) &&
                      sliceFits(widget.inputCapabilities, designer::kWidgetStrings.size()) &&
                      sliceFits(widget.actions, designer::kWidgetActions.size()) &&
                      sliceFits(widget.bindings, designer::kWidgetBindings.size()) &&
                      sliceFits(widget.states, designer::kWidgetStates.size()) &&
                      sliceFits(widget.resources, designer::kWidgetResources.size());
    }
    return designer::kWidgetSchemaVersion == kSupportedSchemaVersion &&
           designer::kWidgetCompatibilityVersion == kSupportedSchemaVersion &&
           designer::kWidgetCatalogSha256 == kExpectedCatalogHash && slicesValid;
}

} // namespace lithe::tests

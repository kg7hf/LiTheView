// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#pragma once

#include "lithe/widget_types.hpp"

#include <array>
#include <concepts>
#include <cstddef>
#include <optional>
#include <tuple>
#include <type_traits>
#include <utility>

namespace lithe
{

// A compile-time replacement for one form component. The actual object has its
// own typed storage; it is never placed in a slot sized for the base class.
template <std::size_t Index, ViewKind Kind, typename Custom> struct WidgetOverride
{
    using Type = Custom;
    using Base = typename WidgetType<Kind>::Type;
    static_assert(std::derived_from<Custom, Base>);
    static_assert(std::is_nothrow_destructible_v<Custom>);
    static constexpr std::size_t index{Index};
    static constexpr ViewKind kind{Kind};
};

namespace detail
{
template <std::size_t Index, typename Default, typename... Bindings> struct OverriddenType
{
    using Type = Default;
};

template <std::size_t Index, typename Default, typename First, typename... Rest>
struct OverriddenType<Index, Default, First, Rest...>
{
    using Type = std::conditional_t<Index == First::index,
                                    typename First::Type,
                                    typename OverriddenType<Index, Default, Rest...>::Type>;
};
} // namespace detail

template <typename... Bindings> class WidgetOverrides
{
public:
    static constexpr std::size_t size{sizeof...(Bindings)};

    template <std::size_t Index, typename Default>
    using TypeAt = typename detail::OverriddenType<Index, Default, Bindings...>::Type;

    template <typename Type> static consteval bool isCustomType() noexcept
    {
        return ((std::is_same_v<Type, typename Bindings::Type> &&
                 !std::is_same_v<Type, typename Bindings::Base>) ||
                ...);
    }

    template <std::size_t Capacity> static consteval bool fits() noexcept
    {
        constexpr std::array<std::size_t, size> indexes{Bindings::index...};
        for (std::size_t i = 0U; i < size; ++i)
        {
            if (indexes[i] >= Capacity)
            {
                return false;
            }
            for (std::size_t j = 0U; j < i; ++j)
            {
                if (indexes[i] == indexes[j])
                {
                    return false;
                }
            }
        }
        return true;
    }

    static constexpr bool contains([[maybe_unused]] std::size_t index) noexcept
    {
        return ((Bindings::index == index) || ...);
    }
    static constexpr bool presentIn([[maybe_unused]] std::size_t count) noexcept
    {
        return ((Bindings::index < count) && ...);
    }
    static constexpr bool accepts([[maybe_unused]] std::size_t index,
                                  [[maybe_unused]] ViewKind kind) noexcept
    {
        return ((Bindings::index != index || Bindings::kind == kind) && ...);
    }
    static constexpr std::size_t builtinIndex(std::size_t index) noexcept
    {
        return index - (std::size_t{0U} + ... + (Bindings::index < index ? 1U : 0U));
    }

private:
    template <std::size_t, typename, typename> friend class StaticLayout;

    template <typename Base, typename... Arguments>
    Base& construct(std::size_t index, Arguments&&... arguments) noexcept
    {
        Base* result{nullptr};
        const auto create =
            [&]<typename Binding>(std::optional<typename Binding::Type>& slot) noexcept
        {
            if constexpr (std::is_same_v<Base, typename Binding::Base>)
            {
                static_assert(
                    std::is_nothrow_constructible_v<typename Binding::Type, Arguments...>);
                if (Binding::index == index)
                {
                    result = &slot.emplace(std::forward<Arguments>(arguments)...);
                }
            }
        };
        visitBindings(create, std::index_sequence_for<Bindings...>{});
        // StaticLayout calls only after contains/accepts validate this slot.
        return *result;
    }

    template <typename Base> Base* get(std::size_t index) noexcept
    {
        Base* result{nullptr};
        visit(index,
              [&]<typename Actual>(Actual& value) noexcept
              {
                  if constexpr (std::derived_from<Actual, Base>)
                  {
                      result = &value;
                  }
              });
        return result;
    }

    template <typename Base> const Base* get(std::size_t index) const noexcept
    {
        const Base* result{nullptr};
        visit(index,
              [&]<typename Actual>(const Actual& value) noexcept
              {
                  if constexpr (std::derived_from<Actual, Base>)
                  {
                      result = &value;
                  }
              });
        return result;
    }

    void finish(std::size_t index) noexcept
    {
        visit(index,
              []<typename Actual>(Actual& view) noexcept
              {
                  if constexpr (std::derived_from<Actual, ScrollView>)
                  {
                      view.fitContentToSubviews();
                  }
                  else if constexpr (std::derived_from<Actual, TabView>)
                  {
                      view.syncPages();
                  }
              });
    }

    void destroy(std::size_t index) noexcept
    {
        const auto reset =
            [&]<typename Binding>(std::optional<typename Binding::Type>& slot) noexcept
        {
            if (Binding::index == index)
            {
                slot.reset();
            }
        };
        visitBindings(reset, std::index_sequence_for<Bindings...>{});
    }

private:
    template <typename Action, std::size_t... Indexes>
    void visitBindings(Action& action, std::index_sequence<Indexes...>) noexcept
    {
        (action.template operator()<std::tuple_element_t<Indexes, std::tuple<Bindings...>>>(
             std::get<Indexes>(mStorage)),
         ...);
    }

    template <typename Action> void visit(std::size_t index, Action&& action) noexcept
    {
        const auto apply =
            [&]<typename Binding>(std::optional<typename Binding::Type>& slot) noexcept
        {
            if (Binding::index == index && slot)
            {
                action(*slot);
            }
        };
        visitBindings(apply, std::index_sequence_for<Bindings...>{});
    }

    template <typename Action> void visit(std::size_t index, Action&& action) const noexcept
    {
        [&]<std::size_t... Indexes>(std::index_sequence<Indexes...>) noexcept
        {
            ((Bindings::index == index && std::get<Indexes>(mStorage)
                  ? (action(*std::get<Indexes>(mStorage)), true)
                  : false),
             ...);
        }(std::index_sequence_for<Bindings...>{});
    }

    std::tuple<std::optional<typename Bindings::Type>...> mStorage{};
};

} // namespace lithe

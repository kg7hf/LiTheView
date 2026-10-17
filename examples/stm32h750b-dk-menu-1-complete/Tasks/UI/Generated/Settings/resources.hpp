#pragma once
#include "lithe/layout.hpp"
#include <array>
#include <span>
#include <string_view>

// Compile only the hash-locked RequiredSources listed by the export result.
// Descriptors and payloads are borrowed from static converter output.
namespace lithe
{
}
namespace lithe::generated::assets::SettingsMenu
{
class SelectedResources final : public ResourceProvider
{
public:
    const Bitmap* bitmap(std::string_view name) const noexcept override
    {
        for (const auto& entry : kBitmaps)
        {
            if (entry.runtimeName == name)
            {
                return entry.get();
            }
        }
        return nullptr;
    }
    const Font* font(std::string_view name) const noexcept override
    {
        for (const auto& entry : kFonts)
        {
            if (entry.runtimeName == name)
            {
                return entry.get();
            }
        }
        return nullptr;
    }
private:
    template <typename T> struct Binding
    {
        std::string_view resourceId;
        std::string_view runtimeName;
        const T* (*get)() noexcept;
    };
    inline static constexpr std::array<Binding<Bitmap>, 0U> kBitmaps{{
    }};
    inline static constexpr std::array<Binding<Font>, 0U> kFonts{{
    }};
};
} // namespace lithe::generated::assets::SettingsMenu

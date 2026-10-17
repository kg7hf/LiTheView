// Generated integration shell. Code inside user-code regions is preserved byte for byte.
#include "tab_settings.hpp"

// LITHEVIEW USER CODE BEGIN form.tabs.includes
// LITHEVIEW USER CODE END form.tabs.includes

namespace lithe::generated
{

void dispatchTabSettingsAction(ViewId action) noexcept
{
    switch (action)
    {
    case std::to_underlying(TabSettings::Action::settings):
        // LITHEVIEW USER CODE BEGIN form.tabs.action.action.settings
        // LITHEVIEW USER CODE END form.tabs.action.action.settings
        break;
    case std::to_underlying(TabSettings::Action::connect):
        // LITHEVIEW USER CODE BEGIN form.tabs.action.action.connect
        // LITHEVIEW USER CODE END form.tabs.action.action.connect
        break;
    case std::to_underlying(TabSettings::Action::mode):
        // LITHEVIEW USER CODE BEGIN form.tabs.action.action.mode
        // LITHEVIEW USER CODE END form.tabs.action.action.mode
        break;
    default:
        break;
    }
}

} // namespace lithe::generated

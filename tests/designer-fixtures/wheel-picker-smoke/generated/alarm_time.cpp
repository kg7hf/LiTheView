// Generated integration shell. Code inside user-code regions is preserved byte for byte.
#include "alarm_time.hpp"

// LITHEVIEW USER CODE BEGIN form.alarm.includes
// LITHEVIEW USER CODE END form.alarm.includes

namespace lithe::generated
{

void dispatchAlarmTimeAction(ViewId action) noexcept
{
    switch (action)
    {
    case std::to_underlying(AlarmTime::Action::hour):
        // LITHEVIEW USER CODE BEGIN form.alarm.action.action.hour
        // LITHEVIEW USER CODE END form.alarm.action.action.hour
        break;
    case std::to_underlying(AlarmTime::Action::minute):
        // LITHEVIEW USER CODE BEGIN form.alarm.action.action.minute
        // LITHEVIEW USER CODE END form.alarm.action.action.minute
        break;
    case std::to_underlying(AlarmTime::Action::repeat):
        // LITHEVIEW USER CODE BEGIN form.alarm.action.action.repeat
        // LITHEVIEW USER CODE END form.alarm.action.action.repeat
        break;
    case std::to_underlying(AlarmTime::Action::save):
        // LITHEVIEW USER CODE BEGIN form.alarm.action.action.save
        // LITHEVIEW USER CODE END form.alarm.action.action.save
        break;
    default:
        break;
    }
}

} // namespace lithe::generated

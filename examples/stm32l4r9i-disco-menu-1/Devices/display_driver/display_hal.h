#ifndef DISPLAY_DRIVER_DISPLAY_HAL_H
#define DISPLAY_DRIVER_DISPLAY_HAL_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C"
{
#endif

    typedef enum
    {
        DISPLAY_HAL_OK,
        DISPLAY_HAL_ERROR,
        DISPLAY_HAL_TIMEOUT,
        DISPLAY_HAL_BOARD_IO_ERROR
    } DisplayHalStatus;

    DisplayHalStatus display_hal_initialize(uint32_t revision);
    DisplayHalStatus display_hal_copy(uintptr_t source,
                                      uint32_t x,
                                      uint32_t y,
                                      uint32_t width,
                                      uint32_t height,
                                      size_t input_stride_pixels);
    DisplayHalStatus display_hal_refresh(void);
    uint32_t display_hal_completed(void);
    uint32_t display_hal_failed(void);
    uint32_t display_hal_ticks(void);
    void display_hal_stop(void);

#ifdef __cplusplus
}
#endif

#endif

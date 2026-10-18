// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <stdbool.h>
#include <stdint.h>
#ifdef __cplusplus
extern "C"
{
#endif
    bool board_init(void);
    uint32_t board_milliseconds(void);
    bool board_pb0(void);
    bool board_pb1(void);
    void board_led(bool on);
    bool board_display(const uint32_t rows[8]);
    // 1 complete sample, 0 pending, -1 fault. Never waits for a scan.
    int board_slider(uint32_t counts[4]);
    void board_sleep(void);
#ifdef __cplusplus
}
#endif

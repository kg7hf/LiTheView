# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

set(LITHEVIEW_DEMO_FONT_BACKEND "AUTO" CACHE STRING
    "Compile-time demo font backend: AUTO, COMPACT5X7, or GENERATED_SANS")
set_property(CACHE LITHEVIEW_DEMO_FONT_BACKEND PROPERTY STRINGS
    AUTO
    COMPACT5X7
    GENERATED_SANS)
set(LITHEVIEW_DEMO_ANIMATION_FORMAT "AUTO" CACHE STRING
    "Compile-time demo animation format: AUTO, ALPHA1, or INDEXED4")
set_property(CACHE LITHEVIEW_DEMO_ANIMATION_FORMAT PROPERTY STRINGS
    AUTO
    ALPHA1
    INDEXED4)
set(LITHEVIEW_WIN32_COLOR_DEPTH "XRGB8888" CACHE STRING
    "Win32 simulator framebuffer color depth: RGB565 or XRGB8888")
set_property(CACHE LITHEVIEW_WIN32_COLOR_DEPTH PROPERTY STRINGS RGB565 RGB888)
if(LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB888")
    message(FATAL_ERROR "The old RGB888 inspection option meant XRGB8888 storage. Reconfigure with -DLITHEVIEW_WIN32_COLOR_DEPTH=XRGB8888.")
endif()
include(${CMAKE_CURRENT_LIST_DIR}/display_products.cmake)

if(NOT LITHEVIEW_DEMO_FONT_BACKEND STREQUAL "AUTO" AND
   NOT LITHEVIEW_DEMO_FONT_BACKEND STREQUAL "COMPACT5X7" AND
   NOT LITHEVIEW_DEMO_FONT_BACKEND STREQUAL "GENERATED_SANS")
    message(FATAL_ERROR
        "LITHEVIEW_DEMO_FONT_BACKEND must be AUTO, COMPACT5X7, or GENERATED_SANS")
endif()

if(NOT LITHEVIEW_DEMO_ANIMATION_FORMAT STREQUAL "AUTO" AND
   NOT LITHEVIEW_DEMO_ANIMATION_FORMAT STREQUAL "ALPHA1" AND
   NOT LITHEVIEW_DEMO_ANIMATION_FORMAT STREQUAL "INDEXED4")
    message(FATAL_ERROR
        "LITHEVIEW_DEMO_ANIMATION_FORMAT must be AUTO, ALPHA1, or INDEXED4")
endif()

if(LITHEVIEW_BUILD_WIN32 AND
   NOT LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB565" AND
   NOT LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "XRGB8888")
    message(FATAL_ERROR
        "LITHEVIEW_WIN32_COLOR_DEPTH must be RGB565 or XRGB8888")
endif()

set(litheview_resolved_demo_font_backend "${LITHEVIEW_DEMO_FONT_BACKEND}")
if(litheview_resolved_demo_font_backend STREQUAL "AUTO")
    set(litheview_resolved_demo_font_backend "COMPACT5X7")
    if(LITHEVIEW_BUILD_WIN32 AND LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "XRGB8888")
        set(litheview_resolved_demo_font_backend "GENERATED_SANS")
    endif()
endif()
message(STATUS
    "LitheView demo font backend: ${litheview_resolved_demo_font_backend} "
    "(requested ${LITHEVIEW_DEMO_FONT_BACKEND})")

set(litheview_resolved_demo_animation_format "${LITHEVIEW_DEMO_ANIMATION_FORMAT}")
if(litheview_resolved_demo_animation_format STREQUAL "AUTO")
    set(litheview_resolved_demo_animation_format "ALPHA1")
    if(LITHEVIEW_BUILD_WIN32 AND LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "XRGB8888")
        set(litheview_resolved_demo_animation_format "INDEXED4")
    endif()
endif()
message(STATUS
    "LitheView demo animation format: ${litheview_resolved_demo_animation_format} "
    "(requested ${LITHEVIEW_DEMO_ANIMATION_FORMAT})")

set(litheview_build_info_variant "RENDERING REGRESSION")
if(LITHEVIEW_BUILD_WIN32)
    if(LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB565")
        set(litheview_build_info_variant "WIN32 RGB565")
    else()
        set(litheview_build_info_variant "WIN32 XRGB8888")
    endif()
elseif(LITHEVIEW_BUILD_STM32U545_PROBE)
    set(litheview_build_info_variant "CORTEX-M33 ${LITHEVIEW_DESTINATION_FORMAT} TILES")
endif()
if(litheview_resolved_demo_font_backend STREQUAL "GENERATED_SANS")
    string(APPEND litheview_build_info_variant " / GENERATED SANS")
    set(litheview_demo_font "generatedSans")
else()
    string(APPEND litheview_build_info_variant " / COMPACT 5X7")
    set(litheview_demo_font "compact5x7")
endif()
if(litheview_resolved_demo_animation_format STREQUAL "INDEXED4")
    string(APPEND litheview_build_info_variant " / INDEXED4 ANIMATION")
    set(litheview_demo_animation "indexed4")
else()
    string(APPEND litheview_build_info_variant " / ALPHA1 ANIMATION")
    set(litheview_demo_animation "alpha1")
endif()

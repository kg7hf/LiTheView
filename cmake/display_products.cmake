# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# Product selection is a configure-time decision. Universal conformance builds
# explicitly compile all writers and all probe/example configurations.
set(LITHEVIEW_STM32U545_PROBE_PROFILE "AUTO" CACHE STRING
    "U545 probes: AUTO, ALL, RGB, or NONE")
set(LITHEVIEW_WIN32_EXAMPLE_SET "AUTO" CACHE STRING
    "Win32 inspection tools: AUTO, ALL, RGB, or NONE")
set(litheview_product_choices AUTO ALL RGB NONE)
set_property(CACHE LITHEVIEW_STM32U545_PROBE_PROFILE PROPERTY STRINGS ${litheview_product_choices})
set_property(CACHE LITHEVIEW_WIN32_EXAMPLE_SET PROPERTY STRINGS ${litheview_product_choices})
foreach(selection IN ITEMS LITHEVIEW_STM32U545_PROBE_PROFILE LITHEVIEW_WIN32_EXAMPLE_SET)
    if(NOT ${selection} IN_LIST litheview_product_choices)
        message(FATAL_ERROR "${selection} must be one of: ${litheview_product_choices}")
    endif()
endforeach()

set(litheview_default_product RGB)
if(LITHEVIEW_DESTINATION_FORMAT STREQUAL "ALL")
    set(litheview_default_product ALL)
elseif(LITHEVIEW_DESTINATION_FORMAT STREQUAL "R2_PACKED2X2")
    set(litheview_default_product NONE)
elseif(LITHEVIEW_DESTINATION_FORMAT STREQUAL "RRRR2222")
    set(litheview_default_product NONE)
elseif(LITHEVIEW_DESTINATION_FORMAT STREQUAL "RYRY2222")
    set(litheview_default_product NONE)
endif()
set(litheview_probe_profile "${LITHEVIEW_STM32U545_PROBE_PROFILE}")
set(litheview_win32_example_set "${LITHEVIEW_WIN32_EXAMPLE_SET}")
if(litheview_probe_profile STREQUAL "AUTO")
    set(litheview_probe_profile "${litheview_default_product}")
endif()
if(litheview_win32_example_set STREQUAL "AUTO")
    set(litheview_win32_example_set "${litheview_default_product}")
endif()

function(litheview_require_destination format consumer)
    if(NOT litheview_${format}_enabled)
        message(FATAL_ERROR "${consumer} requires the ${format} destination writer; select a matching product build.")
    endif()
endfunction()



if(LITHEVIEW_BUILD_RENDERING_FIXTURES AND LITHEVIEW_BUILD_STM32U545_PROBE)
    if(litheview_probe_profile STREQUAL "ALL" OR litheview_probe_profile STREQUAL "RGB")
        litheview_require_destination(RGB565 "The RGB U545 probes")
    endif()
endif()
if(LITHEVIEW_BUILD_RENDERING_FIXTURES AND LITHEVIEW_BUILD_WIN32)
    if(litheview_win32_example_set STREQUAL "ALL" OR litheview_win32_example_set STREQUAL "RGB")
        if(LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB565")
            litheview_require_destination(RGB565 "The compact Win32 fixtures")
        else()
            litheview_require_destination(XRGB8888 "The high-color Win32 fixtures")
        endif()
    endif()
endif()
if(LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_TESTS AND
   (NOT LITHEVIEW_BUILD_RENDERING_FIXTURES OR NOT LITHEVIEW_BUILD_STM32U545_PROBE OR
    NOT litheview_probe_profile STREQUAL "ALL"))
    message(FATAL_ERROR "Rendering conformance requires fixtures and all U545 probe profiles. Set LITHEVIEW_BUILD_RENDERING_TESTS=OFF for portable library tests only.")
endif()

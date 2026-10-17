# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

function(litheview_add_probe target demo)
    add_library(${target} STATIC ${ARGN})
    target_link_libraries(${target} PUBLIC ${demo})
    target_include_directories(${target} PUBLIC ${CMAKE_CURRENT_LIST_DIR})
    litheview_configure_first_party_target(${target})
endfunction()

if(litheview_probe_profile STREQUAL "ALL" OR litheview_probe_profile STREQUAL "RGB")
    litheview_add_probe(litheview_stm32u545_probe litheview_demo_common graphics_probe.cpp)
    litheview_add_probe(litheview_stm32u545_round_probe litheview_round_demo round_graphics_probe.cpp)
endif()

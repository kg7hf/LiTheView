# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# Every adapter executable receives an ordinary C++ product alias in its own
# generated include directory. Product choice never becomes a runtime branch.
function(litheview_add_win32_product target product scene_header scene library title entry)
    set(litheview_rgb_configuration_header "${PROJECT_SOURCE_DIR}/tests/rendering/${product}/configuration.hpp")
    set(litheview_rgb_configuration_type "lithe::examples::${product}::Configuration")
    set(litheview_host_scene_header "${scene_header}")
    set(litheview_host_scene "lithe::demo::${scene}")
    set(litheview_host_title "${title}")
    set(litheview_host_class "LitheView.${target}")
    set(generated_dir "${CMAKE_CURRENT_BINARY_DIR}/${target}/generated")
    configure_file("${PROJECT_SOURCE_DIR}/cmake/win32_host_config.hpp.in"
        "${generated_dir}/litheview/win32_host_config.hpp" @ONLY)
    add_executable(${target} WIN32 "${entry}" main.cpp)
    # Windows SDK header configuration contract only.
    target_compile_definitions(${target} PRIVATE WIN32_LEAN_AND_MEAN)
    target_include_directories(${target} PRIVATE "${generated_dir}")
    target_link_libraries(${target} PRIVATE ${library} user32 gdi32)
    litheview_configure_first_party_target(${target})
endfunction()

if(litheview_win32_example_set STREQUAL "ALL" OR litheview_win32_example_set STREQUAL "RGB")
    set(litheview_rgb_example "xrgb8888")
    if(LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB565")
        set(litheview_rgb_example "rgb565")
    endif()
    litheview_add_win32_product(litheview_win32 ${litheview_rgb_example} demo.hpp Demo
        litheview_demo_common "LitheView graphics demo - ${litheview_rgb_example}"
        "../${litheview_rgb_example}/win32_main.cpp")
    litheview_add_win32_product(litheview_win32_round ${litheview_rgb_example} round_demo.hpp RoundDemo
        litheview_round_demo "LitheView round display - ${litheview_rgb_example}"
        legacy_main.cpp)
    if(LITHEVIEW_WIN32_COLOR_DEPTH STREQUAL "RGB565")
        set_target_properties(litheview_win32 PROPERTIES OUTPUT_NAME litheview_win32_rgb565)
        set_target_properties(litheview_win32_round PROPERTIES OUTPUT_NAME litheview_win32_round_rgb565)
    endif()
endif()

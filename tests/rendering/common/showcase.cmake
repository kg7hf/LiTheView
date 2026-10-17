# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# An example owns its scene and assets. An installed LiTheView supplies only the
# rendering library and explicitly selected fonts, never a demo application.
function(litheview_add_showcase target_name format)
    set(example_root "${CMAKE_CURRENT_FUNCTION_LIST_DIR}/../../..")
    if(format STREQUAL "RGB565")
        set(litheview_demo_font compact5x7)
        set(litheview_demo_animation alpha1)
        set(font_target LitheView::CompactFont)
    elseif(format STREQUAL "XRGB8888")
        set(litheview_demo_font generatedSans)
        set(litheview_demo_animation indexed4)
        set(font_target LitheView::GeneratedFonts)
    else()
        message(FATAL_ERROR "Showcase format must be RGB565 or XRGB8888")
    endif()
    if(NOT TARGET ${font_target})
        message(FATAL_ERROR "This example needs the optional ${font_target} resource package")
    endif()
    set(generated "${CMAKE_CURRENT_BINARY_DIR}/${target_name}/generated")
    configure_file("${example_root}/cmake/demo_font_config.hpp.in"
        "${generated}/litheview/demo_font_config.hpp" @ONLY)
    add_custom_target(${target_name}_build_info
        COMMAND ${CMAKE_COMMAND}
            "-DLITHEVIEW_SOURCE_DIR=${example_root}"
            "-DLITHEVIEW_BUILD_INFO_HEADER=${generated}/litheview/build_info.hpp"
            "-DLITHEVIEW_BUILD_INFO_VERSION=${LitheView_VERSION}"
            "-DLITHEVIEW_BUILD_INFO_VARIANT=${format} SHOWCASE"
            -P "${example_root}/tools/generate_build_info.cmake"
        BYPRODUCTS "${generated}/litheview/build_info.hpp" VERBATIM)
    add_library(${target_name} STATIC "${CMAKE_CURRENT_FUNCTION_LIST_DIR}/demo.cpp")
    add_dependencies(${target_name} ${target_name}_build_info)
    target_link_libraries(${target_name} PUBLIC LitheView::Core ${font_target})
    target_include_directories(${target_name} PUBLIC "${CMAKE_CURRENT_FUNCTION_LIST_DIR}" "${generated}")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(${target_name} PRIVATE
            -Wall -Wextra -Wpedantic -Wconversion -Wsign-conversion -Wdouble-promotion -Wold-style-cast -Werror)
    endif()
endfunction()

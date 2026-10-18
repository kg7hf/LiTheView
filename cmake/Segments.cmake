# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only
# May be included by a consuming application without configuring the raster core.
if(NOT TARGET LitheView::Segments)
    add_library(litheview_segments INTERFACE)
    add_library(LitheView::Segments ALIAS litheview_segments)
    set_target_properties(litheview_segments PROPERTIES EXPORT_NAME Segments)
    target_compile_features(litheview_segments INTERFACE cxx_std_23)
    target_include_directories(litheview_segments INTERFACE
        "$<BUILD_INTERFACE:${CMAKE_CURRENT_LIST_DIR}/../include>"
        "$<INSTALL_INTERFACE:include>")
endif()

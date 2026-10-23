# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

# Bundled font data is optional. The dependency points toward Core, so linking
# Core alone never brings in a resource archive or a particular font family.
function(litheview_add_font_resource target_name export_name)
    add_library(${target_name} STATIC ${ARGN})
    add_library(LitheView::${export_name} ALIAS ${target_name})
    set_target_properties(${target_name} PROPERTIES EXPORT_NAME ${export_name})
    target_link_libraries(${target_name} PUBLIC LitheView::Core)
    litheview_configure_first_party_target(${target_name})
    install(TARGETS ${target_name} EXPORT LitheViewTargets
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR})
endfunction()

if(LITHEVIEW_BUILD_COMPACT_FONT)
    litheview_add_font_resource(litheview_compact_font CompactFont font5x7.cpp)
endif()
if(LITHEVIEW_BUILD_GENERATED_FONTS)
    litheview_add_font_resource(litheview_generated_fonts GeneratedFonts
        generated_sans_bold.cpp generated_sans_regular.cpp)
    install(FILES generated_sans_bold.json generated_sans_regular.json
        DESTINATION ${CMAKE_INSTALL_DATADIR}/LitheView/fonts)
endif()
if(LITHEVIEW_BUILD_GENERATED_SANS20)
    litheview_add_font_resource(litheview_generated_sans20 GeneratedSans20
        generated_sans_20_regular.cpp generated_sans_20_bold.cpp)
    install(FILES generated_sans_20_regular.json generated_sans_20_bold.json
        DESTINATION ${CMAKE_INSTALL_DATADIR}/LitheView/fonts)
endif()
if(LITHEVIEW_BUILD_GENERATED_SANS28)
    litheview_add_font_resource(litheview_generated_sans28 GeneratedSans28
        generated_sans_28_regular.cpp generated_sans_28_bold.cpp)
    install(FILES generated_sans_28_regular.json generated_sans_28_bold.json
        DESTINATION ${CMAKE_INSTALL_DATADIR}/LitheView/fonts)
endif()

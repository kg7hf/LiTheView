// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Documents;

internal static class FormRuntimeLimits
{
    // Keep in sync with lithe::config::kMaxChildren in include/lithe/config.hpp.
    internal const int MaximumDirectChildren = 16;
}

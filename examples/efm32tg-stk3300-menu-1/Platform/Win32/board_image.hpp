// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
#pragma once
#include <array>
#include <optional>
#include <string_view>
#include <windows.h>
#include <wtypes.h>
#include <gdiplus.h>

namespace tiny::host
{
// Windows-only image lifetime. Destroy the bitmap before shutting down GDI+.
class BoardImage final
{
public:
    BoardImage() = default;
    BoardImage(const BoardImage&) = delete;
    BoardImage& operator=(const BoardImage&) = delete;
    ~BoardImage()
    {
        mBitmap.reset();
        if (mToken != 0U)
            Gdiplus::GdiplusShutdown(mToken);
    }
    [[nodiscard]] bool load()
    {
        Gdiplus::GdiplusStartupInput startup;
        if (Gdiplus::GdiplusStartup(&mToken, &startup, nullptr) != Gdiplus::Ok)
            return false;
        std::array<wchar_t, 32768U> executable{};
        const auto length = GetModuleFileNameW(nullptr, executable.data(), executable.size());
        if (length == 0U || length >= executable.size())
            return false;
        const std::wstring_view name{executable.data(), length};
        const auto separator = name.find_last_of(L"\\/");
        constexpr std::wstring_view filename{L"board-front.png"};
        if (separator == name.npos || separator + 1U + filename.size() >= executable.size())
            return false;
        filename.copy(executable.data() + separator + 1U, filename.size());
        executable[separator + 1U + filename.size()] = L'\0';
        mBitmap.emplace(executable.data());
        return mBitmap->GetLastStatus() == Gdiplus::Ok &&
               mBitmap->GetWidth() == 465U && mBitmap->GetHeight() == 288U;
    }
    [[nodiscard]] bool draw(HDC dc)
    {
        if (!mBitmap)
            return false;
        Gdiplus::Graphics graphics{dc};
        return graphics.DrawImage(&*mBitmap, 0, 0, 930, 576) == Gdiplus::Ok;
    }
private:
    ULONG_PTR mToken{};
    std::optional<Gdiplus::Bitmap> mBitmap;
};
} // namespace tiny::host

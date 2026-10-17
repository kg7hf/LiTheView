// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "demo.hpp"
#include "generated_form_test.hpp"
#include "generated_widget_metadata_test.hpp"
#include "graphics_probe.hpp"
#include "lithe/lithe.hpp"
#include "litheview/build_info.hpp"
#include "litheview/demo_font_config.hpp"
#include "round_demo.hpp"

#include <array>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <limits>
#include <span>
#include <string_view>

namespace
{

int& failureCount() noexcept
{
    static int count = 0;
    return count;
}

struct DemoGoldenSignatures
{
    std::uint32_t initialRgb565{};
    std::uint32_t focusedRgb565{};
    std::uint32_t initialXrgb8888{};
    std::uint32_t focusedXrgb8888{};
};

constexpr bool kUsesCompactDemoFont{lithe::demo::config::kFontBackend ==
                                    lithe::BuiltInFontBackend::compact5x7};
constexpr DemoGoldenSignatures kCompactDemoGoldens{
    0xf88f6224U,
    0x807cef56U,
    0x94b033a1U,
    0x25b32d3bU,
};
constexpr DemoGoldenSignatures kGeneratedDemoGoldens{
    0x8baac9e5U,
    0xb4159684U,
    0x7955ec5cU,
    0x56343042U,
};
constexpr DemoGoldenSignatures kDemoGoldens{kUsesCompactDemoFont ? kCompactDemoGoldens
                                                                 : kGeneratedDemoGoldens};
struct RoundDemoGoldenSignatures
{
    std::uint32_t dashboardRgb565{};
    std::uint32_t gestureLabRgb565{};
};
constexpr RoundDemoGoldenSignatures kCompactRoundDemoGoldens{0x7596e85cU, 0x16d5a161U};
constexpr RoundDemoGoldenSignatures kGeneratedRoundDemoGoldens{0x786b049aU, 0xd1469b14U};
constexpr RoundDemoGoldenSignatures kRoundDemoGoldens{
    kUsesCompactDemoFont ? kCompactRoundDemoGoldens : kGeneratedRoundDemoGoldens};
std::uint32_t crc32Pixels(std::span<const lithe::Pixel> pixels) noexcept
{
    std::uint32_t crc = 0xffffffffU;
    for (const lithe::Pixel pixel : pixels)
    {
        const std::array<std::uint8_t, 2> bytes{
            lithe::detail::lowByte(pixel),
            lithe::detail::lowByte(pixel >> 8U),
        };
        for (const std::uint8_t byte : bytes)
        {
            crc ^= byte;
            for (std::uint8_t bit = 0U; bit < 8U; ++bit)
            {
                const std::uint32_t mask = 0U - (crc & 1U);
                crc = (crc >> 1U) ^ (0xedb88320U & mask);
            }
        }
    }
    return crc ^ 0xffffffffU;
}

std::uint32_t fnv1aBytes(std::span<const std::byte> bytes) noexcept
{
    std::uint32_t checksum = 0x811c9dc5U;
    for (const std::byte byte : bytes)
    {
        checksum ^= std::to_integer<std::uint8_t>(byte);
        checksum *= 0x01000193U;
    }
    return checksum;
}

std::uint32_t crc32Buffer(lithe::PixelBufferView buffer) noexcept
{
    if (!buffer.valid())
    {
        return 0U;
    }
    const std::size_t rowBytes =
        lithe::detail::nonnegativeSize(buffer.width) * lithe::bytesPerPixel(buffer.format);
    std::uint32_t crc = 0xffffffffU;
    for (std::int32_t row = 0; row < buffer.height; ++row)
    {
        const std::size_t rowOffset = lithe::detail::nonnegativeSize(row) * buffer.strideBytes;
        for (std::size_t column = 0U; column < rowBytes; ++column)
        {
            crc ^= std::to_integer<std::uint8_t>(buffer.data[rowOffset + column]);
            for (std::uint8_t bit = 0U; bit < 8U; ++bit)
            {
                const std::uint32_t mask = 0U - (crc & 1U);
                crc = (crc >> 1U) ^ (0xedb88320U & mask);
            }
        }
    }
    return crc ^ 0xffffffffU;
}

[[maybe_unused]] bool rgb565Equivalent(std::span<const lithe::Pixel> rgb565,
                                       lithe::PixelBufferView xrgb8888) noexcept
{
    if (!xrgb8888.valid() || xrgb8888.format != lithe::PixelFormat::xrgb8888 ||
        rgb565.size() != lithe::detail::nonnegativeSize(xrgb8888.width) *
                             lithe::detail::nonnegativeSize(xrgb8888.height))
    {
        return false;
    }
    using Traits = lithe::PixelFormatTraits<lithe::PixelFormat::xrgb8888>;
    for (std::size_t index = 0U; index < rgb565.size(); ++index)
    {
        const lithe::Color color = Traits::read(xrgb8888.data, index * Traits::kBytesPerPixel);
        if (color.rgb565() != rgb565[index])
        {
            return false;
        }
    }
    return true;
}

void expect(bool condition, std::string_view description)
{
    if (!condition)
    {
        std::cerr << "FAIL: " << description << '\n';
        ++failureCount();
    }
}

void expectCrc32(std::uint32_t actual, std::uint32_t expected, std::string_view description)
{
    if (actual != expected)
    {
        std::cerr << "FAIL: " << description << " (actual=" << actual << ", expected=" << expected
                  << ")\n";
        ++failureCount();
    }
}

bool sameColor(lithe::Color lhs, lithe::Color rhs) noexcept
{
    return lhs.red == rhs.red && lhs.green == rhs.green && lhs.blue == rhs.blue &&
           lhs.alpha == rhs.alpha;
}

void testBuildInfo()
{
    using namespace lithe::demo::buildinfo;
    expect(kVersionText.starts_with("VERSION: ") && kVersionText.size() > 9U,
           "build information contains the semantic version");
    expect(kVariantText.starts_with("TARGET: ") && kVariantText.size() > 8U,
           "build information contains the target and pixel format");
    expect(kBranchText.starts_with("BRANCH: ") && kBranchText.size() > 8U,
           "build information contains the Git branch");
    expect(kCommitText.starts_with("COMMIT: ") && kCommitText.size() > 8U,
           "build information contains the Git commit");
    expect(kTimestampText.starts_with("BUILT: ") && kTimestampText.ends_with(" UTC"),
           "build information contains an explicit UTC build time");
}

template <lithe::PixelFormat Format> bool referenceSceneMatches() noexcept
{
    using Traits = lithe::PixelFormatTraits<Format>;
    std::array<typename Traits::Storage, 16> pixels{};
    lithe::DamageTracker damage{};
    lithe::Painter painter{lithe::Surface{lithe::PixelSurface<Format>{std::span{pixels}, 4, 4, 4}},
                           damage};
    const lithe::Color background = lithe::Color::rgb(0x214365U);
    const lithe::Color foreground = lithe::Color::rgb(0xe6a240U);
    painter.fillRect({0, 0, 4, 4}, background);
    painter.setClip({1, 1, 2, 2});
    painter.fillRect({0, 0, 4, 4}, foreground);
    const std::span<const std::byte> bytes = std::as_bytes(std::span{pixels});
    const lithe::Color outside = Traits::read(bytes, 0U);
    const lithe::Color inside = Traits::read(bytes, 5U * Traits::kBytesPerPixel);
    return outside.rgb565() == background.rgb565() && inside.rgb565() == foreground.rgb565() &&
           !damage.empty();
}

template <lithe::PixelFormat Format,
          std::int32_t Width = lithe::demo::Demo::kWidth,
          std::int32_t Height = lithe::demo::Demo::kHeight>
class FormatTestDisplay final : public lithe::Display
{
public:
    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;

    static constexpr std::size_t kPixelCount =
        lithe::detail::nonnegativeSize(Width) * lithe::detail::nonnegativeSize(Height);

    lithe::Surface surface() noexcept override
    {
        return lithe::Surface{lithe::PixelSurface<Format>{std::span{pixels}, Width, Height, Width}};
    }

    void present(std::span<const lithe::Rect> regions) noexcept override
    {
        ++presentCount;
        lastRegionCount = regions.size();
        if (!regions.empty())
        {
            lastRegion = regions.front();
        }
    }

    lithe::PixelBufferView buffer() const noexcept
    {
        return {std::as_bytes(std::span{pixels}),
                Format,
                Width,
                Height,
                lithe::detail::nonnegativeSize(Width) *
                    lithe::PixelFormatTraits<Format>::kBytesPerPixel};
    }

    std::array<Storage, kPixelCount> pixels{};
    std::size_t presentCount{0};
    std::size_t lastRegionCount{0};
    lithe::Rect lastRegion{};
};

using TestDisplay = FormatTestDisplay<lithe::PixelFormat::rgb565>;
using XrgbTestDisplay = FormatTestDisplay<lithe::PixelFormat::xrgb8888>;
using RoundTestDisplay = FormatTestDisplay<lithe::PixelFormat::rgb565,
                                           lithe::demo::RoundDemo::kWidth,
                                           lithe::demo::RoundDemo::kHeight>;

class ComparingTileSink final : public lithe::TileSink
{
public:
    ComparingTileSink(lithe::PixelBufferView expected, lithe::Size size) noexcept
        : mExpected(expected), mSize(size)
    {
    }

    bool beginFrame(lithe::Size frameSize, lithe::PixelFormat format) noexcept override
    {
        mTiles = 0U;
        mEnded = false;
        mMatches = true;
        mBegan = frameSize.width == mSize.width && frameSize.height == mSize.height &&
                 format == mExpected.format && mExpected.valid();
        return mBegan;
    }

    bool writeTile(lithe::Rect destination, lithe::PixelBufferView pixels) noexcept override
    {
        if (!mBegan || destination.width <= 0 || destination.height <= 0 || destination.x < 0 ||
            destination.y < 0 || destination.right() > mSize.width ||
            destination.bottom() > mSize.height || pixels.format != mExpected.format ||
            pixels.width != destination.width || pixels.height != destination.height ||
            !pixels.valid())
        {
            return false;
        }
        const std::size_t pixelBytes = lithe::bytesPerPixel(pixels.format);
        for (std::int32_t row = 0; row < destination.height; ++row)
        {
            for (std::int32_t column = 0; column < destination.width; ++column)
            {
                const auto expectedOffset =
                    (lithe::detail::nonnegativeSize(destination.y + row) * mExpected.strideBytes) +
                    (lithe::detail::nonnegativeSize(destination.x + column) * pixelBytes);
                const auto tileOffset = (lithe::detail::nonnegativeSize(row) * pixels.strideBytes) +
                                        (lithe::detail::nonnegativeSize(column) * pixelBytes);
                for (std::size_t byte = 0U; byte < pixelBytes; ++byte)
                {
                    if (mExpected.data[expectedOffset + byte] != pixels.data[tileOffset + byte])
                    {
                        mMatches = false;
                    }
                }
            }
        }
        ++mTiles;
        return true;
    }

    bool endFrame(bool complete) noexcept override
    {
        mEnded = complete;
        return complete && mMatches;
    }

    bool matches() const noexcept
    {
        return mBegan && mEnded && mMatches;
    }
    std::size_t tiles() const noexcept
    {
        return mTiles;
    }

private:
    lithe::PixelBufferView mExpected;
    lithe::Size mSize{};
    std::size_t mTiles{0U};
    bool mBegan{false};
    bool mEnded{false};
    bool mMatches{true};
};

class RecordingTransferObserver final : public lithe::DisplayTransferObserver
{
public:
    void transferCompleted(lithe::DisplayTransferToken token,
                           lithe::DisplayTransferStatus status) noexcept override
    {
        lastToken = token;
        lastStatus = status;
        ++completionCount;
    }

    lithe::DisplayTransferToken lastToken{};
    lithe::DisplayTransferStatus lastStatus{lithe::DisplayTransferStatus::unknownToken};
    std::size_t completionCount{0U};
};

class TestTransferAdapter final : public lithe::DisplayTransferAdapter
{
public:
    lithe::DisplayTransferCapabilities capabilities() const noexcept override
    {
        return {true, true, true, true, 2U};
    }

    lithe::DisplayTransferResult
    submit(const lithe::DisplayTransferRequest& request) noexcept override
    {
        if (!request.source.valid() || request.destination.width != request.source.width ||
            request.destination.height != request.source.height || request.layer >= 2U || mPending)
        {
            return {};
        }
        if (request.engine == lithe::DisplayTransferEngine::cpu)
        {
            return {lithe::DisplayTransferStatus::completed, {}};
        }
        mObserver = request.observer;
        mPending = true;
        return {lithe::DisplayTransferStatus::pending, mToken};
    }

    lithe::DisplayTransferStatus status(lithe::DisplayTransferToken token) const noexcept override
    {
        if (token.value != mToken.value)
        {
            return lithe::DisplayTransferStatus::unknownToken;
        }
        return mPending ? lithe::DisplayTransferStatus::pending
                        : lithe::DisplayTransferStatus::completed;
    }

    void complete() noexcept
    {
        if (!mPending)
        {
            return;
        }
        mPending = false;
        if (mObserver != nullptr)
        {
            mObserver->transferCompleted(mToken, lithe::DisplayTransferStatus::completed);
        }
    }

private:
    lithe::DisplayTransferObserver* mObserver{nullptr};
    lithe::DisplayTransferToken mToken{1U};
    bool mPending{false};
};

class CountingDelegate final : public lithe::ControllerDelegate
{
public:
    void onAction([[maybe_unused]] lithe::ViewController& controller,
                  lithe::ViewId action) noexcept override
    {
        lastAction = action;
        ++actions;
    }
    void onBack([[maybe_unused]] lithe::ViewController& controller) noexcept override
    {
        ++backs;
    }
    bool onGesture([[maybe_unused]] lithe::ViewController& controller,
                   lithe::GestureEvent event) noexcept override
    {
        lastGesture = event.type;
        ++gestures;
        return handleGestures;
    }

    lithe::ViewId lastAction{0U};
    lithe::GestureType lastGesture{lithe::GestureType::tap};
    std::size_t actions{0U};
    std::size_t backs{0U};
    std::size_t gestures{0U};
    bool handleGestures{true};
};

class AnimationResources final : public lithe::ResourceProvider
{
public:
    AnimationResources() noexcept
    {
        mBitmap.data = std::as_bytes(std::span{mPixel});
        mBitmap.width = 1U;
        mBitmap.height = 1U;
        mBitmap.strideBytes = 1U;
        mBitmap.format = lithe::BitmapFormat::alpha1;
    }

    const lithe::Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return name == "strip_f1" || name == "strip_f2" || name == "strip_f3" ? &mBitmap : nullptr;
    }

    lithe::MutableTextBuffer textBuffer(std::string_view name) const noexcept override
    {
        return name == "unit-name" ? lithe::MutableTextBuffer{mText} : lithe::MutableTextBuffer{};
    }

private:
    std::array<std::uint8_t, 1> mPixel{0x80U};
    lithe::Bitmap mBitmap{};
    mutable std::array<char, 12> mText{{'L', 'T', 'E', '\0'}};
};

template <lithe::PixelFormat Format> bool generatedFontSceneMatchesTiles() noexcept
{
    FormatTestDisplay<Format> display{};
    lithe::Style style{};
    style.background = lithe::Color::rgb(0x102030U);
    style.foreground = lithe::Color::rgb(0xe8b040U);
    style.textAlign = {lithe::HorizontalAlign::center, lithe::VerticalAlign::center};
    lithe::LabelView label{{0, 0, lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight},
                           lithe::generatedSans(),
                           "Generated fonts: Caf\xc3\xa9 25\xc2\xb0\xe2\x80\xa6",
                           style};
    CountingDelegate delegate{};
    lithe::ViewController controller{label, delegate};
    lithe::Application application{display};
    if (!application.push(controller) || !application.render())
    {
        return false;
    }

    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    std::array<Storage, std::size_t{73U} * 11U> tilePixels{};
    const lithe::Surface workspace{lithe::PixelSurface<Format>{tilePixels, 73, 11, 73}};
    ComparingTileSink sink{display.buffer(),
                           {lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight}};
    const lithe::TileRenderResult result =
        lithe::TiledRenderer::render(application,
                                     {lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight},
                                     workspace,
                                     sink,
                                     style.background);
    return result.ok() && sink.matches();
}

void testFontBackendsAndManifests()
{
    using namespace lithe;

    expect(&builtInFont<BuiltInFontBackend::compact5x7>() == &font5x7() &&
               &builtInBoldFont<BuiltInFontBackend::compact5x7>() == &font5x7Bold(),
           "the compact 5x7 backend remains compile-time selectable");
    expect(&builtInFont<BuiltInFontBackend::generatedSans>() == &generatedSans() &&
               &builtInBoldFont<BuiltInFontBackend::generatedSans>() == &generatedSansBold(),
           "the generated font backend is independently compile-time selectable");

    const GeneratedFontManifest& regular = generatedSans().manifest();
    const GeneratedFontManifest& bold = generatedSansBold().manifest();
    expect(regular.family == "DejaVu Sans" && regular.style == "Regular" &&
               regular.coverage == GlyphCoverage::alpha8 && regular.glyphCount == 99U &&
               regular.coverageBytes == 7617U && regular.recordBytes == 1584U &&
               regular.estimatedRomBytes == 9201U && regular.dataChecksum == 0x0f4ed3a4U,
           "the regular generated-font manifest records its repertoire and exact ROM payload");
    expect(bold.family == "DejaVu Sans" && bold.style == "Bold" &&
               bold.coverage == GlyphCoverage::alpha8 && bold.glyphCount == 99U &&
               bold.coverageBytes == 8734U && bold.recordBytes == 1584U &&
               bold.estimatedRomBytes == 10318U && bold.dataChecksum == 0xf6f05603U,
           "the bold generated-font manifest records a distinct designed face");
    expect(regular.repertoire == "U+0020-U+007E,U+00B0,U+00E9,U+2026,U+FFFD" &&
               regular.sourceSha256.size() == 64U,
           "the generated manifest exposes the bounded UTF-8 repertoire and source identity");

    Glyph compactGlyph{};
    Glyph generatedGlyph{};
    Glyph regularWideGlyph{};
    Glyph boldGlyph{};
    Glyph fallbackGlyph{};
    Glyph questionGlyph{};
    expect(font5x7().glyph(U'A', compactGlyph) && compactGlyph.valid() &&
               compactGlyph.coverage == GlyphCoverage::rowMask,
           "the quick 5x7 backend keeps its compact row-mask representation");
    expect(generatedSans().glyph(U'\u00e9', generatedGlyph) && generatedGlyph.valid() &&
               generatedGlyph.coverage == GlyphCoverage::alpha8,
           "the generated backend returns anti-aliased lowercase and accented glyph coverage");
    expect(generatedSans().glyph(U'W', regularWideGlyph) && regularWideGlyph.valid() &&
               generatedSansBold().glyph(U'W', boldGlyph) && boldGlyph.valid() &&
               boldGlyph.width != regularWideGlyph.width &&
               boldGlyph.advance != regularWideGlyph.advance,
           "the bold backend supplies independently generated metrics and coverage");
    expect(generatedSans().glyph(U'\U0001f642', fallbackGlyph) &&
               generatedSans().glyph(U'?', questionGlyph) &&
               fallbackGlyph.data.data() == questionGlyph.data.data(),
           "a code point outside the selected repertoire uses the declared fallback glyph");

    constexpr std::array<std::uint8_t, 3> kOneBitCoverage{0xe0U, 0xa0U, 0xe0U};
    constexpr std::array<GeneratedGlyphRecord, 1> kOneBitRecords{{
        {U'?', 0U, 3U, 3U, 0U, 4U, 0, 0},
    }};
    const GeneratedFontData oneBitData{
        kOneBitRecords,
        std::as_bytes(std::span{kOneBitCoverage}),
        {"Test", "Regular", "U+003F", "", 0U, 3U, 16U, 19U, 1U, 3U, GlyphCoverage::alpha1},
        U'?',
        4U,
        3U,
        3U,
        1U};
    const GeneratedFont oneBitFont{oneBitData};
    Glyph oneBitGlyph{};
    expect(oneBitFont.glyph(U'X', oneBitGlyph) && oneBitGlyph.valid() &&
               oneBitGlyph.coverage == GlyphCoverage::alpha1 && oneBitGlyph.strideBytes == 0U &&
               oneBitGlyph.data.size() == 3U,
           "the smallest generated-font profile accepts implicit-stride one-bit coverage");

    std::array<Pixel, 16> implicitStridePixels{};
    DamageTracker implicitStrideDamage{};
    Painter implicitStridePainter{{implicitStridePixels, 4, 4, 4}, implicitStrideDamage};
    implicitStridePainter.drawText({0, 0, 4, 4},
                                   "?",
                                   oneBitFont,
                                   Color::rgb(0xffffffU),
                                   {HorizontalAlign::left, VerticalAlign::top});
    constexpr Pixel kWhite{0xffffU};
    expect(implicitStridePixels[0] == kWhite && implicitStridePixels[1] == kWhite &&
               implicitStridePixels[2] == kWhite && implicitStridePixels[4] == kWhite &&
               implicitStridePixels[5] == 0U && implicitStridePixels[6] == kWhite &&
               implicitStridePixels[8] == kWhite && implicitStridePixels[9] == kWhite &&
               implicitStridePixels[10] == kWhite,
           "Painter resolves implicit alpha1 stride independently for every glyph row");

    const GeneratedFontData truncatedData{kOneBitRecords,
                                          std::as_bytes(std::span{kOneBitCoverage}).first(1U),
                                          oneBitData.manifest,
                                          U'?',
                                          4U,
                                          3U,
                                          3U,
                                          1U};
    const GeneratedFont truncatedFont{truncatedData};
    Glyph truncatedGlyph{};
    expect(!truncatedFont.glyph(U'?', truncatedGlyph) && !truncatedGlyph.valid(),
           "generated-font lookup rejects a glyph whose coverage exceeds the borrowed table");
}

void testTextLayoutAndUtf8Policy()
{
    using namespace lithe;
    const Font& font = generatedSans();
    const TextMetrics natural = Painter::measureText("Caf\xc3\xa9 25\xc2\xb0", font);
    expect(natural.validUtf8 && natural.lineCount == 1U && natural.width > 0 &&
               natural.height == font.lineHeight(),
           "generated text measurement accepts the selected UTF-8 repertoire");

    TextLayoutOptions wrappedOptions{};
    wrappedOptions.wrap = TextWrap::word;
    wrappedOptions.lineSpacing = 2U;
    const TextMetrics wrapped =
        Painter::measureTextLayout("alpha beta gamma", font, {48, 100}, wrappedOptions);
    const std::int32_t wrappedLineCount = wrapped.lineCount;
    const std::int32_t wrappedLineHeight = font.lineHeight();
    const std::int32_t expectedWrappedHeight =
        (wrappedLineCount * wrappedLineHeight) + ((wrappedLineCount - 1) * 2);
    expect(wrapped.lineCount >= 2U && wrapped.width <= 48 &&
               wrapped.height == expectedWrappedHeight && !wrapped.truncated,
           "word wrapping and line spacing produce bounded allocation-free metrics");

    const TextMetrics windowsLines =
        Painter::measureTextLayout("alpha\r\nbeta", font, {100, 100}, wrappedOptions);
    expect(windowsLines.lineCount == 2U,
           "multiline layout treats a Windows CRLF sequence as one hard line break");

    TextLayoutOptions ellipsisOptions = wrappedOptions;
    ellipsisOptions.overflow = TextOverflow::ellipsis;
    ellipsisOptions.maxLines = 2U;
    const TextMetrics ellipsized =
        Painter::measureTextLayout("alpha beta gamma delta", font, {48, 100}, ellipsisOptions);
    expect(ellipsized.lineCount == 2U && ellipsized.width <= 48 && ellipsized.truncated,
           "max-line capacity truncates explicitly and reserves room for an ellipsis");

    constexpr std::string_view kInvalidUtf8{"\xc0\xaf", 2U};
    const TextMetrics replaced =
        Painter::measureTextLayout(kInvalidUtf8, font, {100, 40}, TextLayoutOptions{});
    TextLayoutOptions stopOptions{};
    stopOptions.invalidUtf8 = InvalidUtf8Policy::stop;
    const TextMetrics stopped =
        Painter::measureTextLayout(kInvalidUtf8, font, {100, 40}, stopOptions);
    expect(!replaced.validUtf8 && replaced.width > 0 && !replaced.truncated,
           "replacement policy reports invalid UTF-8 while rendering replacement glyphs");
    expect(!stopped.validUtf8 && stopped.width == 0 && stopped.truncated,
           "stop policy fails closed at the first invalid UTF-8 sequence");

    std::array<Rgb565Pixel, std::size_t{64U} * 24U> plainPixels{};
    std::array<Rgb565Pixel, std::size_t{64U} * 24U> underlinedPixels{};
    DamageTracker plainDamage{};
    DamageTracker underlinedDamage{};
    Painter plainPainter{{plainPixels, 64, 24, 64}, plainDamage};
    Painter underlinedPainter{{underlinedPixels, 64, 24, 64}, underlinedDamage};
    TextLayoutOptions drawOptions{};
    drawOptions.overflow = TextOverflow::ellipsis;
    plainPainter.drawTextLayout(
        {4, 3, 40, 17}, "Clipped text", font, Color::rgb(0x40c0ffU), drawOptions);
    drawOptions.underline = true;
    underlinedPainter.drawTextLayout(
        {4, 3, 40, 17}, "Clipped text", font, Color::rgb(0x40c0ffU), drawOptions);
    expect(crc32Pixels(plainPixels) != crc32Pixels(underlinedPixels),
           "underline is a text style independent of glyph color and coverage");
    expect(plainPixels[2U] == 0U && underlinedPixels[2U] == 0U,
           "text drawing clips glyph coverage to the caller-supplied rectangle");
}

void testGeneratedFontFormatsAndTiles()
{
    expect(generatedFontSceneMatchesTiles<lithe::PixelFormat::rgb565>(),
           "generated fonts match full-frame RGB565 rendering in arbitrary tiles");
    expect(generatedFontSceneMatchesTiles<lithe::PixelFormat::rgb888>(),
           "generated fonts match full-frame RGB888 rendering in arbitrary tiles");
    expect(generatedFontSceneMatchesTiles<lithe::PixelFormat::xrgb8888>(),
           "generated fonts match full-frame XRGB8888 rendering in arbitrary tiles");
    expect(generatedFontSceneMatchesTiles<lithe::PixelFormat::argb8888>(),
           "generated fonts match full-frame ARGB8888 rendering in arbitrary tiles");
}

void testGeometryAndColor()
{
    using namespace lithe;
    expect(intersect({0, 0, 10, 10}, {5, 5, 10, 10}).width == 5, "rectangle intersection");
    expect(Color::rgb(0xff0000U).rgb565() == 0xf800U, "red RGB565 conversion");
    expect(Color::rgb(0x00ff00U).rgb565() == 0x07e0U, "green RGB565 conversion");
    expect(Color::rgb(0x0000ffU).rgb565() == 0x001fU, "blue RGB565 conversion");

    constexpr auto kMinimum = std::numeric_limits<std::int32_t>::min();
    constexpr auto kMaximum = std::numeric_limits<std::int32_t>::max();
    expect(Rect{kMaximum - 1, 0, 10, 1}.right() == kMaximum,
           "rectangle right edge saturates instead of overflowing");
    expect(Rect{kMinimum + 1, 0, 1, 1}.translated({-10, 0}).x == kMinimum,
           "rectangle translation saturates instead of overflowing");
    View root{{0, 0, 10, 10}};
    View child{{0, 0, 5, 5}};
    expect(root.addSubview(child), "view accepts an unowned child");
    expect(!child.addSubview(root), "view rejects an ancestor cycle");

    std::array<Pixel, 3> undersizedSurfaceStorage{};
    expect(!Surface{undersizedSurfaceStorage, 2, 2, 2}.valid(),
           "surface rejects geometry larger than its pixel span");
    std::array<Pixel, 1> zeroStrideSurfaceStorage{};
    expect(!Surface{zeroStrideSurfaceStorage, 1, 1, 0}.valid(),
           "surface rejects a zero stride before division");
    std::array<std::byte, 1> undersizedBitmapStorage{};
    Bitmap undersizedBitmap{undersizedBitmapStorage, 1U, 1U, 0U, BitmapFormat::rgb565};
    expect(!undersizedBitmap.valid(), "bitmap rejects a byte span smaller than its pixel format");
    const Bitmap missingPalette{undersizedBitmapStorage, 1U, 1U, 0U, BitmapFormat::indexed4};
    expect(!missingPalette.valid(), "indexed bitmap rejects a missing palette");
    const std::array<Color, 17> oversizedPalette{};
    const Bitmap oversizedIndexedPalette{undersizedBitmapStorage,
                                         1U,
                                         1U,
                                         0U,
                                         BitmapFormat::indexed4,
                                         Color::rgb(0xffffffU),
                                         oversizedPalette};
    expect(!oversizedIndexedPalette.valid(), "indexed bitmap rejects more than 16 colors");
}

void testPixelFormatContracts()
{
    using namespace lithe;

    std::array<std::byte, 4> encoded{};
    PixelFormatTraits<PixelFormat::rgb565>::write(encoded, 0U, Color::rgb(0xff0000U));
    expect(encoded[0] == std::byte{0x00U} && encoded[1] == std::byte{0xf8U},
           "RGB565 uses the documented little-endian representation");
    PixelFormatTraits<PixelFormat::rgb888>::write(encoded, 0U, Color::rgb(0x123456U));
    expect(encoded[0] == std::byte{0x12U} && encoded[1] == std::byte{0x34U} &&
               encoded[2] == std::byte{0x56U},
           "RGB888 stores red, green, then blue");
    PixelFormatTraits<PixelFormat::xrgb8888>::write(encoded, 0U, Color::rgb(0x123456U));
    expect(encoded[0] == std::byte{0x56U} && encoded[1] == std::byte{0x34U} &&
               encoded[2] == std::byte{0x12U} && encoded[3] == std::byte{0xffU},
           "XRGB8888 stores a little-endian 0xFFRRGGBB word");
    PixelFormatTraits<PixelFormat::argb8888>::write(encoded, 0U, Color::rgba(0x12345678U));
    expect(encoded[0] == std::byte{0x56U} && encoded[1] == std::byte{0x34U} &&
               encoded[2] == std::byte{0x12U} && encoded[3] == std::byte{0x78U},
           "ARGB8888 stores a little-endian 0xAARRGGBB word");

    expect(referenceSceneMatches<PixelFormat::rgb565>() &&
               referenceSceneMatches<PixelFormat::rgb888>() &&
               referenceSceneMatches<PixelFormat::xrgb8888>() &&
               referenceSceneMatches<PixelFormat::argb8888>(),
           "all surface formats apply the same logical scene and clipping rules");

    const Color translucent{200U, 100U, 50U, 128U};
    expect(sameColor(translucent.blendedOver(Color::transparent()), translucent),
           "source-over composition preserves color and alpha over transparency");

    std::array<Xrgb8888Pixel, 7> targetPixels{};
    DamageTracker damage{};
    Painter painter{Surface{Xrgb8888Surface{targetPixels, 7, 1, 7}}, damage};
    painter.fillRect({0, 0, 7, 1}, Color::rgb(0x0000ffU));

    const std::array<std::byte, 3> rgb888Data{std::byte{0x12U}, std::byte{0x34U}, std::byte{0x56U}};
    const Bitmap rgb888Bitmap{rgb888Data, 1, 1, 0U, BitmapFormat::rgb888};
    painter.drawBitmap({0, 0}, rgb888Bitmap);

    const std::array<std::byte, 1> alpha1Data{std::byte{0x80U}};
    const Bitmap alpha1Bitmap{alpha1Data, 1, 1, 0U, BitmapFormat::alpha1, Color::rgb(0x00ff00U)};
    painter.drawBitmap({1, 0}, alpha1Bitmap);

    const std::array<std::byte, 1> alpha8Data{std::byte{0x80U}};
    const Bitmap alpha8Bitmap{alpha8Data, 1, 1, 0U, BitmapFormat::alpha8, Color::rgb(0xff0000U)};
    painter.drawBitmap({2, 0}, alpha8Bitmap);

    const std::array<std::byte, 4> argb8888Data{
        std::byte{0x00U}, std::byte{0xffU}, std::byte{0xffU}, std::byte{0x80U}};
    const Bitmap argb8888Bitmap{argb8888Data, 1, 1, 0U, BitmapFormat::argb8888};
    painter.drawBitmap({3, 0}, argb8888Bitmap);

    const std::array<std::byte, 1> indexed4Data{std::byte{0x12U}};
    const std::array<Color, 3> indexed4Palette{
        Color::transparent(), Color::rgb(0xffaa00U), Color::rgb(0x00aaffU)};
    const Bitmap indexed4Bitmap{
        indexed4Data, 2, 1, 0U, BitmapFormat::indexed4, Color::rgb(0xffffffU), indexed4Palette};
    painter.drawBitmap({4, 0}, indexed4Bitmap);

    const std::array<std::byte, 1> invalidIndexed4Data{std::byte{0xf0U}};
    const std::array<Color, 1> transparentOnlyPalette{Color::transparent()};
    const Bitmap invalidIndexed4Bitmap{invalidIndexed4Data,
                                       1,
                                       1,
                                       0U,
                                       BitmapFormat::indexed4,
                                       Color::rgb(0xffffffU),
                                       transparentOnlyPalette};
    painter.drawBitmap({6, 0}, invalidIndexed4Bitmap);

    const std::span<const std::byte> rendered = std::as_bytes(std::span{targetPixels});
    using XrgbTraits = PixelFormatTraits<PixelFormat::xrgb8888>;
    expect(sameColor(XrgbTraits::read(rendered, 0U), Color::rgb(0x123456U)) &&
               sameColor(XrgbTraits::read(rendered, 4U), Color::rgb(0x00ff00U)),
           "opaque and one-bit-alpha assets share the format-independent painter path");
    expect(sameColor(XrgbTraits::read(rendered, 8U), Color{128U, 0U, 127U, 255U}) &&
               sameColor(XrgbTraits::read(rendered, 12U), Color{128U, 128U, 127U, 255U}),
           "anti-aliased-alpha and ARGB assets use deterministic source-over composition");
    expect(sameColor(XrgbTraits::read(rendered, 16U), Color::rgb(0xffaa00U)) &&
               sameColor(XrgbTraits::read(rendered, 20U), Color::rgb(0x00aaffU)),
           "four-bit indexed assets unpack high then low nibbles through a bounded palette");
    expect(sameColor(XrgbTraits::read(rendered, 24U), Color::rgb(0x0000ffU)),
           "an out-of-range indexed color fails closed without touching the destination");

    std::array<Xrgb8888Pixel, 8> scaledPixels{};
    DamageTracker scaledDamage{};
    Painter scaledPainter{Surface{Xrgb8888Surface{scaledPixels, 4, 2, 4}}, scaledDamage};
    scaledPainter.drawBitmap({0, 0}, indexed4Bitmap, 2U);
    const std::span<const std::byte> scaledRendered = std::as_bytes(std::span{scaledPixels});
    expect(sameColor(XrgbTraits::read(scaledRendered, 0U), Color::rgb(0xffaa00U)) &&
               sameColor(XrgbTraits::read(scaledRendered, 4U), Color::rgb(0xffaa00U)) &&
               sameColor(XrgbTraits::read(scaledRendered, 8U), Color::rgb(0x00aaffU)) &&
               sameColor(XrgbTraits::read(scaledRendered, 12U), Color::rgb(0x00aaffU)) &&
               sameColor(XrgbTraits::read(scaledRendered, 16U), Color::rgb(0xffaa00U)) &&
               sameColor(XrgbTraits::read(scaledRendered, 28U), Color::rgb(0x00aaffU)),
           "integer bitmap scaling expands source pixels without allocating resized storage");
}

void testGallopAnimationResources()
{
    using namespace lithe;
    using namespace lithe::demo;

    GallopResources<GallopResourceFormat::alpha1> compact{};
    GallopResources<GallopResourceFormat::indexed4> indexed{};
    bool resourcesValid = true;
    for (const std::string_view name : kGallopResourceNames)
    {
        const Bitmap* const compactFrame = compact.bitmap(name);
        const Bitmap* const indexedFrame = indexed.bitmap(name);
        resourcesValid = resourcesValid && compactFrame != nullptr && compactFrame->valid() &&
                         compactFrame->format == BitmapFormat::alpha1 &&
                         compactFrame->width == 96 && compactFrame->height == 72 &&
                         compactFrame->strideBytes == 12U && indexedFrame != nullptr &&
                         indexedFrame->valid() && indexedFrame->format == BitmapFormat::indexed4 &&
                         indexedFrame->width == 96 && indexedFrame->height == 72 &&
                         indexedFrame->strideBytes == 48U &&
                         indexedFrame->palette.size() == demo::assets::kGallopPalette.size();
    }
    expect(resourcesValid,
           "compile-time gallop resources expose bounded alpha1 and indexed4 frames");
    expect(fnv1aBytes(std::as_bytes(std::span{demo::assets::kGallopBits})) ==
                   demo::assets::kGallopFramesChecksum &&
               fnv1aBytes(std::as_bytes(std::span{demo::assets::kGallopIndexed4Bits})) ==
                   demo::assets::kGallopIndexed4FramesChecksum,
           "gallop resource payloads match their recorded deterministic checksums");
}

void testDisplayTransferContract()
{
    using namespace lithe;
    std::array<Pixel, 4> pixels{};
    const PixelBufferView source{
        std::as_bytes(std::span{pixels}), PixelFormat::rgb565, 2, 2, 2U * sizeof(Pixel)};
    RecordingTransferObserver observer{};
    TestTransferAdapter adapter{};
    const DisplayTransferCapabilities capabilities = adapter.capabilities();
    expect(capabilities.dma && capabilities.dma2d && capabilities.ltdcLayers &&
               capabilities.asynchronous && capabilities.layerCount == 2U,
           "display transfer adapters report optional acceleration capabilities explicitly");

    const DisplayTransferRequest asynchronous{
        source, {0, 0, 2, 2}, DisplayTransferEngine::dma2d, 1U, &observer};
    const DisplayTransferResult submitted = adapter.submit(asynchronous);
    expect(submitted.accepted() && submitted.status == DisplayTransferStatus::pending &&
               adapter.status(submitted.token) == DisplayTransferStatus::pending,
           "asynchronous display transfer retains an explicit completion token");
    adapter.complete();
    expect(adapter.status(submitted.token) == DisplayTransferStatus::completed &&
               observer.completionCount == 1U &&
               observer.lastToken.value == submitted.token.value &&
               observer.lastStatus == DisplayTransferStatus::completed,
           "display transfer completion is available through polling and signaling");

    const DisplayTransferResult synchronous =
        adapter.submit({source, {0, 0, 2, 2}, DisplayTransferEngine::cpu, 0U, nullptr});
    expect(synchronous.accepted() && synchronous.status == DisplayTransferStatus::completed &&
               !synchronous.token.valid(),
           "synchronous display transfers release caller storage before returning");
    expect(!adapter.submit({source, {0, 0, 2, 2}, DisplayTransferEngine::ltdcLayer, 2U, nullptr})
                .accepted(),
           "display transfer adapters reject unsupported layer indexes");
}

void testWidgetSetLayouts()
{
    using namespace lithe;
    using PanelsAndLabels = WidgetSet<ViewKind::panel, ViewKind::label>;
    static_assert(PanelsAndLabels::size == 2U && PanelsAndLabels::contains(ViewKind::label) &&
                  !PanelsAndLabels::contains(ViewKind::button));

    std::array<ViewSpec, 3> specs{};
    specs[0].frame = {0, 0, 100, 40};
    specs[1].kind = ViewKind::label;
    specs[1].parent = 0U;
    specs[1].id = 5U;
    specs[1].frame = {4, 4, 60, 12};
    specs[1].text = "READY";
    specs[2].kind = ViewKind::button;
    specs[2].parent = 0U;
    specs[2].id = 6U;
    specs[2].frame = {4, 20, 60, 16};

    StaticLayout<3, PanelsAndLabels> layout{};
    const std::span<const ViewSpec> labelled = std::span<const ViewSpec>{specs}.first(2U);
    expect(layout.build(labelled).ok() && layout.find<LabelView>(5U) != nullptr &&
               layout.find<ButtonView>(6U) == nullptr,
           "a layout restricted to panels and labels builds them");
    const LayoutResult rejected = layout.build(specs);
    expect(rejected.error == LayoutError::invalidKind && rejected.node == 2U &&
               layout.root() == nullptr,
           "a kind outside the layout's widget set fails closed and leaves the layout empty");
    specs[2].font = "missing";
    expect(layout.build(specs).error == LayoutError::invalidKind,
           "the widget set is checked before the node's resources");
    const bool rebuilt = layout.build(labelled).ok();
    const LabelView* label = layout.find<LabelView>(5U);
    expect(rebuilt && label != nullptr && label->text() == "READY",
           "a restricted layout rebuilds after a rejected kind");

    StaticLayout<3> everything{};
    specs[2].font = {};
    expect(everything.build(specs).ok() && everything.find<ButtonView>(6U) != nullptr,
           "the default layout still builds every kind");
}

void testXmlLayout()
{
    using namespace lithe;
    constexpr std::string_view kXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,100,80\" runtimeID=\"1\" "
        "backgroundColor=\"16777215\">"
        "<object className=\"LCDBUTTONVIEW\" frame=\"4,4,92,24\" runtimeID=\"7\" "
        "text=\"OPEN\" detail=\"XML\" canFocus=\"1\"/>"
        "</object>";
    std::array<ViewSpec, 4> specs{};
    const XmlLayoutResult parsed = XmlLayoutParser::parse(kXml, specs);
    expect(parsed.ok(), "legacy object XML parses from memory");
    expect(parsed.nodeCount == 2U, "XML node count");
    expect(specs[1].parent == 0 && specs[1].id == 7U, "XML parent and runtime ID");
    expect(specs[1].text == "OPEN" && specs[1].detail == "XML", "XML text attributes remain views");

    std::array<ViewSpec, 2> guardedSpecs{};
    guardedSpecs[1].id = 0xa55aU;
    const XmlLayoutResult atCapacity =
        XmlLayoutParser::parse(kXml, std::span<ViewSpec>{guardedSpecs}.first(1U));
    expect(!atCapacity && atCapacity.error == XmlLayoutError::capacity &&
               guardedSpecs[1].id == 0xa55aU,
           "XML capacity exhaustion fails without writing past caller storage");

    StaticLayout<4> layout{};
    expect(layout.build(std::span<const ViewSpec>{specs}.first(parsed.nodeCount)).ok(),
           "parsed specs build a static layout");
    expect(layout.find(7U) != nullptr && layout.find(7U)->focusable(), "button is focusable");

    const XmlLayoutResult invalid = XmlLayoutParser::parse("<object>", specs);
    expect(!invalid && invalid.error == XmlLayoutError::missingClass,
           "malformed legacy XML fails closed");

    const XmlLayoutResult mismatched =
        XmlLayoutParser::parse("<view frame=\"0,0,10,10\"></label>", specs);
    expect(!mismatched && mismatched.error == XmlLayoutError::unbalancedElement,
           "mismatched closing elements fail closed");

    const XmlLayoutResult minimumInteger =
        XmlLayoutParser::parse("<view frame=\"-2147483648,0,1,1\"/>", specs);
    expect(minimumInteger.ok() && specs[0].frame.x == std::numeric_limits<std::int32_t>::min(),
           "freestanding XML integer parsing accepts INT32_MIN without overflow");
    const XmlLayoutResult overflowingInteger =
        XmlLayoutParser::parse("<view frame=\"2147483648,0,1,1\"/>", specs);
    expect(!overflowingInteger && overflowingInteger.error == XmlLayoutError::invalidAttribute,
           "freestanding XML integer parsing rejects values above INT32_MAX");
    const XmlLayoutResult overflowingInterval =
        XmlLayoutParser::parse("<view frame=\"0,0,1,1\" animationInterval=\"65536\"/>", specs);
    expect(!overflowingInterval && overflowingInterval.error == XmlLayoutError::invalidAttribute,
           "freestanding XML parsing rejects values above a bounded field type");

    constexpr std::string_view kUnresolvedFontXml =
        "<label frame=\"0,0,10,10\" font=\"generated-font\" text=\"A\"/>";
    const XmlLayoutResult unresolvedFont = XmlLayoutParser::parse(kUnresolvedFontXml, specs);
    expect(unresolvedFont.ok(), "named font XML parses independently of resources");
    expect(!layout.build(std::span<const ViewSpec>{specs}.first(unresolvedFont.nodeCount)),
           "a named resource must be supplied by the caller");
}

void testXmlMutationCorpus()
{
    using namespace lithe;
    constexpr auto kValidXml =
        std::to_array("<view frame=\"0,0,96,64\"><button frame=\"4,4,88,24\" id=\"7\" "
                      "text=\"OPEN\"/></view>");
    constexpr std::array<char, 4> kMutations{'\0', '<', '"', '&'};
    bool deterministicAndBounded = true;
    std::size_t cases = 0U;

    for (std::size_t offset = 0U; offset + 1U < kValidXml.size(); ++offset)
    {
        for (const char mutationValue : kMutations)
        {
            auto mutated = kValidXml;
            mutated[offset] = mutationValue;
            const std::string_view input{mutated.data(), mutated.size() - 1U};
            std::array<ViewSpec, 4> firstSpecs{};
            std::array<ViewSpec, 4> secondSpecs{};
            const XmlLayoutResult first =
                XmlLayoutParser::parse(input, firstSpecs, XmlParseOptions{true});
            const XmlLayoutResult second =
                XmlLayoutParser::parse(input, secondSpecs, XmlParseOptions{true});
            deterministicAndBounded =
                deterministicAndBounded && first.error == second.error &&
                first.offset == second.offset && first.nodeCount == second.nodeCount &&
                first.animationFrameCount == second.animationFrameCount &&
                first.offset <= input.size() && first.nodeCount <= firstSpecs.size();
            if (first.ok())
            {
                deterministicAndBounded = deterministicAndBounded && first.nodeCount > 0U;
                for (std::size_t index = 0U; index < first.nodeCount; ++index)
                {
                    deterministicAndBounded =
                        deterministicAndBounded &&
                        firstSpecs[index].kind == secondSpecs[index].kind &&
                        firstSpecs[index].parent == secondSpecs[index].parent &&
                        firstSpecs[index].frame.x == secondSpecs[index].frame.x &&
                        firstSpecs[index].frame.y == secondSpecs[index].frame.y &&
                        firstSpecs[index].frame.width == secondSpecs[index].frame.width &&
                        firstSpecs[index].frame.height == secondSpecs[index].frame.height &&
                        firstSpecs[index].id == secondSpecs[index].id;
                }
            }
            ++cases;
        }
    }

    expect(cases == (kValidXml.size() - 1U) * kMutations.size() && deterministicAndBounded,
           "deterministic XML mutation corpus remains bounded and repeatable");
}

void testPainterDamage()
{
    using namespace lithe;
    std::array<Pixel, 100> pixels{};
    DamageTracker damage{};
    Painter painter({pixels, 10, 10, 10}, damage);
    painter.fillRect({2, 3, 4, 2}, Color::rgb(0xff0000U));
    expect(pixels[(3U * 10U) + 2U] == 0xf800U, "painter fills RGB565 surface");
    expect(damage.size() == 1U && damage.regions().front().width == 4, "painter records damage");
}

void testAdvancedWidgets()
{
    using namespace lithe;
    constexpr std::string_view kXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,120,100\">"
        "<object className=\"LCDSCROLLVIEW\" frame=\"0,0,120,32\" contentHeight=\"62\" "
        "scrollStep=\"20\">"
        "<object className=\"LCDBUTTONVIEW\" frame=\"0,0,110,18\" runtimeID=\"11\" text=\"ONE\"/>"
        "<object className=\"LCDBUTTONVIEW\" frame=\"0,22,110,18\" runtimeID=\"12\" text=\"TWO\"/>"
        "<object className=\"LCDBUTTONVIEW\" frame=\"0,44,110,18\" runtimeID=\"13\" "
        "text=\"THREE\"/>"
        "</object>"
        "<object className=\"LCDPICKERVIEW\" frame=\"0,38,120,26\" runtimeID=\"20\" "
        "items=\"ECO|BALANCED|FAST\" selectedIndex=\"1\"/>"
        "<object className=\"LCDSOFTKEYVIEW\" frame=\"0,68,120,30\" runtimeID=\"30\" "
        "left_text=\"BACK\" right_text=\"OK\" leftAction=\"31\" rightAction=\"32\"/>"
        "</object>";
    std::array<ViewSpec, 8> specs{};
    const XmlLayoutResult parsed = XmlLayoutParser::parse(kXml, specs, XmlParseOptions{true});
    expect(parsed.ok() && parsed.nodeCount == 7U,
           "strict XML accepts the expanded portable widget dialect");
    expect(specs[1].kind == ViewKind::scroll && specs[5].kind == ViewKind::picker &&
               specs[6].kind == ViewKind::softkey,
           "XML maps scroll, picker, and softkey classes");

    StaticLayout<8> layout{};
    expect(layout.build(std::span<const ViewSpec>{specs}.first(parsed.nodeCount)).ok(),
           "expanded widget XML builds without allocation");
    auto* picker = layout.find<PickerView>(20U);
    expect(picker != nullptr && picker->selectedItem() == "BALANCED",
           "picker exposes selected item");
    expect(picker != nullptr && picker->handleKey(Key::right) && picker->selectedItem() == "FAST",
           "picker consumes left/right input");
    auto* softkey = layout.find<SoftkeyView>(30U);
    expect(softkey != nullptr && softkey->actionId() == 32U, "softkey defaults to right action");
    if (softkey != nullptr)
    {
        softkey->pointerPressed({1, 1});
        expect(softkey->actionId() == 31U, "softkey pointer selects left action");
    }

    const XmlLayoutResult unknown = XmlLayoutParser::parse(
        "<view frame=\"0,0,10,10\" mystery=\"1\"/>", specs, XmlParseOptions{true});
    expect(!unknown && unknown.error == XmlLayoutError::unsupportedAttribute,
           "strict XML rejects unknown configuration attributes");
}

void testSelectionAndTextWidgets()
{
    using namespace lithe;
    constexpr std::string_view kXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,200,140\">"
        "<object className=\"LCDCHECKBOXVIEW\" frame=\"4,4,192,28\" runtimeID=\"41\" "
        "text=\"ALERTS\" checked=\"1\"/>"
        "<object className=\"LCDRADIOBUTTONVIEW\" frame=\"4,36,92,28\" runtimeID=\"42\" "
        "text=\"AUTO\" selected=\"1\"/>"
        "<object className=\"LCDRADIOBUTTONVIEW\" frame=\"104,36,92,28\" runtimeID=\"43\" "
        "text=\"MANUAL\" selected=\"0\"/>"
        "<object className=\"LCDTEXTFIELDVIEW\" frame=\"4,70,192,30\" runtimeID=\"44\" "
        "binding=\"unit-name\" placeholder=\"NAME\" maxLength=\"8\"/>"
        "</object>";
    std::array<ViewSpec, 6> specs{};
    const XmlLayoutResult parsed = XmlLayoutParser::parse(kXml, specs, XmlParseOptions{true});
    expect(parsed.ok() && parsed.nodeCount == 5U && specs[1].kind == ViewKind::checkbox &&
               specs[2].kind == ViewKind::radio && specs[4].kind == ViewKind::textField,
           "XML maps bounded checkbox, radio, and text-field controls");

    AnimationResources resources{};
    StaticLayout<6> layout{};
    expect(
        layout
            .build(std::span<const ViewSpec>{specs}.first(parsed.nodeCount), font5x7(), &resources)
            .ok(),
        "selection and text controls resolve caller-owned resources");
    auto* checkbox = layout.find<CheckboxView>(41U);
    auto* automatic = layout.find<RadioButtonView>(42U);
    auto* manual = layout.find<RadioButtonView>(43U);
    auto* text = layout.find<TextFieldView>(44U);
    expect(checkbox != nullptr && checkbox->checked(), "checkbox preserves XML state");
    ViewController controller{*layout.root()};
    controller.activate();
    expect(controller.handleKey(Key::select) && checkbox != nullptr && !checkbox->checked(),
           "select activates a checkbox even when no action delegate is installed");
    if (automatic != nullptr && manual != nullptr)
    {
        manual->activate();
        automatic->setSelected(false);
        expect(manual->selected() && !automatic->selected(),
               "radio state is explicit so a bounded controller can enforce its group");
    }
    if (text != nullptr)
    {
        expect(text->text() == "LTE" && text->handleTextInput(U'-') &&
                   text->handleTextInput(U'2') && text->handleTextInput(U'0'),
               "text field edits the supplied buffer without allocating");
        expect(text->handleTextInput(U'\u00e9') && text->text().size() == 8U,
               "text field stores valid UTF-8 up to its byte bound");
        expect(!text->handleTextInput(U'X') && text->handleTextInput(U'\b') &&
                   text->text() == "LTE-20",
               "text field fails closed at capacity and backspace removes one UTF-8 scalar");
    }

    std::array<char, 4096> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{specs}.first(parsed.nodeCount), output);
    std::array<ViewSpec, 6> roundTrip{};
    const XmlLayoutResult reparsed = XmlLayoutParser::parse(
        std::string_view{output.data(), written.bytesWritten}, roundTrip, XmlParseOptions{true});
    expect(written.ok() && reparsed.ok() && roundTrip[1].checked &&
               roundTrip[4].resource == "unit-name" && roundTrip[4].maxLength == 8U,
           "XML writer round-trips selection state and text-buffer binding");
}

void testWidgetStateResolution()
{
    using namespace lithe;

    Style inherited{};
    inherited.accent = Color::rgb(0x112233U);
    const Style inheritedFocused = resolveWidgetStyle(inherited, WidgetState::focused);
    const Style inheritedPressed = resolveWidgetStyle(inherited, WidgetState::pressed);
    const Style inheritedDisabled = resolveWidgetStyle(inherited, WidgetState::disabled);
    expect(inheritedFocused.background.legacyValue() == inherited.accent.legacyValue() &&
               inheritedPressed.background.legacyValue() == inherited.accent.legacyValue() &&
               inheritedFocused.foreground.legacyValue() == Color::rgb(0xffffffU).legacyValue() &&
               inheritedDisabled.background.legacyValue() ==
                   Color::rgb(detail::kDefaultDisabledBackgroundRgb).legacyValue(),
           "omitted state tokens preserve legacy accent, white, and disabled color policy");
    WidgetStateStyle transparentFocused{};
    transparentFocused.focusedBackground = Color::transparent();
    inherited.states = &transparentFocused;
    expect(resolveWidgetStyle(inherited, WidgetState::focused).background.alpha == 0U,
           "explicit transparent state colors remain distinct from omitted state colors");

    constexpr std::string_view kReservedStateColorXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,20,20\" "
        "focusedBackgroundColor=\"0xffc35aa5\"/>";
    std::array<ViewSpec, 1> reservedSpecs{};
    std::array<WidgetStateStyle, 1> reservedStateStyles{};
    const XmlLayoutResult reservedStateColor = XmlLayoutParser::parse(
        kReservedStateColorXml, reservedSpecs, XmlParseOptions{true, {}, reservedStateStyles});
    expect(reservedStateColor.error == XmlLayoutError::invalidAttribute,
           "the internal omitted-state sentinel is rejected as an explicit XML color");
    constexpr std::string_view kStateColorWithoutStorageXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,20,20\" "
        "focusedBackgroundColor=\"0x00112233\"/>";
    const XmlLayoutResult missingStateStorage =
        XmlLayoutParser::parse(kStateColorWithoutStorageXml, reservedSpecs, XmlParseOptions{true});
    expect(missingStateStorage.error == XmlLayoutError::stateStyleCapacity,
           "explicit XML state colors fail closed without caller-owned state-style storage");

    WidgetStateStyle stateTokens{};
    Style tokens{};
    tokens.background = Color::rgb(0x101112U);
    tokens.foreground = Color::rgb(0x202122U);
    stateTokens.focusedBackground = Color::rgb(0x303132U);
    stateTokens.focusedForeground = Color::rgb(0x404142U);
    stateTokens.pressedBackground = Color::rgb(0x505152U);
    stateTokens.pressedForeground = Color::rgb(0x606162U);
    stateTokens.disabledBackground = Color::rgb(0x707172U);
    stateTokens.disabledForeground = Color::rgb(0x808182U);
    tokens.states = &stateTokens;

    const Style normal = resolveWidgetStyle(tokens, WidgetState::normal);
    const Style focused = resolveWidgetStyle(tokens, WidgetState::focused);
    const Style pressed = resolveWidgetStyle(tokens, WidgetState::pressed);
    const Style disabled = resolveWidgetStyle(tokens, WidgetState::disabled);
    expect(normal.background.red == 0x10U && normal.foreground.red == 0x20U &&
               focused.background.red == 0x30U && focused.foreground.red == 0x40U &&
               pressed.background.red == 0x50U && pressed.foreground.red == 0x60U &&
               disabled.background.red == 0x70U && disabled.foreground.red == 0x80U,
           "bounded widget-state tokens resolve without allocation or implicit color policy");
    expect(resolveWidgetState(false, true, true) == WidgetState::disabled &&
               resolveWidgetState(true, true, true) == WidgetState::pressed &&
               resolveWidgetState(true, true, false) == WidgetState::focused,
           "widget-state precedence is disabled, pressed, focused, then normal");

    ButtonView button{{0, 0, 40, 20}, font5x7(), "STATE", {}, tokens};
    button.setFocused(true);
    button.handlePointerEvent({{4, 4}, PointerPhase::pressed, 1U});
    expect(button.pressed() && button.widgetState() == WidgetState::pressed,
           "pointer press enters the common pressed state");
    button.handlePointerEvent({{44, 4}, PointerPhase::moved, 1U});
    expect(!button.pressed() && button.widgetState() == WidgetState::focused,
           "captured pointer movement outside restores the focused state");
    button.handlePointerEvent({{4, 4}, PointerPhase::moved, 1U});
    button.handlePointerEvent({{4, 4}, PointerPhase::cancelled, 1U});
    expect(!button.pressed(), "pointer cancellation always clears the common pressed state");
    button.setEnabled(false);
    expect(button.widgetState() == WidgetState::disabled,
           "disabled state wins and cannot retain focus or press state");
}

constexpr std::int32_t kButtonSceneWidth{240};
constexpr std::int32_t kButtonSceneHeight{96};
constexpr std::size_t kButtonSceneCapacity{8U};
constexpr std::int32_t kHomeIconSize{12};
constexpr std::size_t kHomeIconStrideBytes{2U};
// A 12-by-12 alpha1 house, most-significant bit first, two bytes per row.
constexpr std::array<std::uint8_t, 24> kHomeIconBits{
    0x06U, 0x00U, 0x0fU, 0x00U, 0x1fU, 0x80U, 0x3fU, 0xc0U, 0x7fU, 0xe0U, 0xffU, 0xf0U,
    0x7fU, 0xe0U, 0x70U, 0xe0U, 0x70U, 0xe0U, 0x70U, 0xe0U, 0x70U, 0xe0U, 0x7fU, 0xe0U,
};
// Text, icon-only, and icon-plus-text momentary buttons above latched, released, and
// disabled-latched toggles. Every color is explicit so goldens do not depend on defaults.
constexpr std::string_view kButtonSceneXml =
    "<panel frame=\"0,0,240,96\" backgroundColor=\"0x00302010\">"
    "<button frame=\"4,4,112,28\" runtimeID=\"1\" text=\"OPEN\" detail=\"2\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\"/>"
    "<button frame=\"120,4,36,28\" runtimeID=\"2\" icon=\"home\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x0080f0ff\"/>"
    "<button frame=\"160,4,76,28\" runtimeID=\"3\" icon=\"home\" text=\"HOME\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\"/>"
    "<button frame=\"4,36,112,28\" runtimeID=\"4\" text=\"ALARM\" behavior=\"toggle\" "
    "checked=\"1\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\" "
    "selectedBackgroundColor=\"0x00fae4d6\" selectedForegroundColor=\"0x00803d1a\"/>"
    "<button frame=\"120,36,116,28\" runtimeID=\"5\" text=\"MUTE\" behavior=\"toggle\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\"/>"
    "<button frame=\"4,66,112,26\" runtimeID=\"6\" text=\"LOCK\" icon=\"home\" behavior=\"toggle\" "
    "checked=\"1\" enabled=\"0\"/>"
    "</panel>";
constexpr std::uint32_t kButtonSceneRgb565Golden{0x712f30f3U};
constexpr std::uint32_t kButtonSceneXrgb8888Golden{0x6efb49ebU};
constexpr lithe::Point kIconOnlySolidPixel{132, 17};

class ButtonResources final : public lithe::ResourceProvider
{
public:
    ButtonResources() noexcept
    {
        mIcon.data = std::as_bytes(std::span{kHomeIconBits});
        mIcon.width = kHomeIconSize;
        mIcon.height = kHomeIconSize;
        mIcon.strideBytes = kHomeIconStrideBytes;
        mIcon.format = lithe::BitmapFormat::alpha1;
    }

    const lithe::Bitmap* bitmap(std::string_view name) const noexcept override
    {
        return name == "home" ? &mIcon : nullptr;
    }

private:
    lithe::Bitmap mIcon{};
};

struct ButtonScene
{
    std::array<lithe::ViewSpec, kButtonSceneCapacity> specs{};
    std::array<lithe::WidgetStateStyle, kButtonSceneCapacity> stateStyles{};
    ButtonResources resources{};
    lithe::StaticLayout<kButtonSceneCapacity> layout{};
    std::size_t count{0U};

    bool build() noexcept
    {
        const lithe::XmlLayoutResult parsed = lithe::XmlLayoutParser::parse(
            kButtonSceneXml, specs, lithe::XmlParseOptions{true, {}, stateStyles});
        count = parsed.nodeCount;
        return parsed.ok() && layout
                                  .build(std::span<const lithe::ViewSpec>{specs}.first(count),
                                         lithe::font5x7(),
                                         &resources)
                                  .ok();
    }
};

struct ButtonSceneRender
{
    std::uint32_t crc{0U};
    bool tilesMatch{false};
};

template <lithe::PixelFormat Format> ButtonSceneRender renderButtonScene() noexcept
{
    static ButtonScene scene{};
    static FormatTestDisplay<Format, kButtonSceneWidth, kButtonSceneHeight> display{};
    if (!scene.build() || scene.layout.root() == nullptr)
    {
        return {};
    }
    lithe::ViewController controller{*scene.layout.root()};
    lithe::Application application{display};
    if (!application.push(controller) || !application.render())
    {
        return {};
    }

    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    std::array<Storage, std::size_t{73U} * 11U> tilePixels{};
    const lithe::Surface workspace{lithe::PixelSurface<Format>{tilePixels, 73, 11, 73}};
    ComparingTileSink sink{display.buffer(), {kButtonSceneWidth, kButtonSceneHeight}};
    const lithe::TileRenderResult tiles = lithe::TiledRenderer::render(
        application, {kButtonSceneWidth, kButtonSceneHeight}, workspace, sink);
    return {crc32Buffer(display.buffer()), tiles.ok() && sink.matches()};
}

void testButtonVariantXml()
{
    using namespace lithe;
    static ButtonScene scene{};
    expect(scene.build() && scene.count == 7U, "strict XML builds text, icon, and toggle buttons");
    expect(scene.specs[1].buttonBehavior == ButtonBehavior::momentary &&
               scene.specs[2].resource == "home" &&
               scene.specs[4].buttonBehavior == ButtonBehavior::toggle && scene.specs[4].checked &&
               scene.specs[4].style.states != nullptr,
           "XML maps button behavior, latched state, icon, and selected-state colors");

    std::array<ViewSpec, 2> specs{};
    std::array<WidgetStateStyle, 2> stateStyles{};
    const XmlParseOptions strict{true, {}, stateStyles};
    expect(XmlLayoutParser::parse("<button frame=\"0,0,40,20\" checked=\"1\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects a checked momentary button");
    expect(XmlLayoutParser::parse("<label frame=\"0,0,40,20\" behavior=\"toggle\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects button behavior on another widget kind");
    expect(XmlLayoutParser::parse("<button frame=\"0,0,40,20\" behavior=\"latch\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "XML rejects an unknown button behavior");
    expect(
        XmlLayoutParser::parse(
            "<button frame=\"0,0,40,20\" selectedBackgroundColor=\"0xffc35aa5\"/>", specs, strict)
                .error == XmlLayoutError::invalidAttribute,
        "the omitted-state sentinel is rejected as an explicit selected color");

    const XmlLayoutResult compatible = XmlLayoutParser::parse(
        "<object className=\"LCDBUTTONVIEW\" frame=\"0,0,40,20\" checked=\"1\"/>", specs);
    StaticLayout<2> legacy{};
    expect(compatible.ok() &&
               legacy.build(std::span<const ViewSpec>{specs}.first(compatible.nodeCount)).ok() &&
               legacy.root() != nullptr && !legacy.root()->selected(),
           "compatibility XML keeps a checked momentary button unselected");

    const XmlLayoutResult missingIcon = XmlLayoutParser::parse(
        "<panel frame=\"0,0,40,40\"><button frame=\"0,0,40,20\" icon=\"absent\"/></panel>",
        specs,
        strict);
    ButtonResources resources{};
    StaticLayout<2> missing{};
    const LayoutResult built = missing.build(
        std::span<const ViewSpec>{specs}.first(missingIcon.nodeCount), font5x7(), &resources);
    expect(missingIcon.ok() && built.error == LayoutError::missingResource && built.node == 1U,
           "an unresolved button icon fails layout construction closed");

    std::array<char, 4096> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{scene.specs}.first(scene.count), output);
    std::array<ViewSpec, kButtonSceneCapacity> roundTrip{};
    std::array<WidgetStateStyle, kButtonSceneCapacity> roundTripStates{};
    const XmlLayoutResult reparsed =
        XmlLayoutParser::parse(std::string_view{output.data(), written.bytesWritten},
                               roundTrip,
                               XmlParseOptions{true, {}, roundTripStates});
    expect(written.ok() && reparsed.ok() && reparsed.nodeCount == scene.count &&
               roundTrip[4].buttonBehavior == ButtonBehavior::toggle && roundTrip[4].checked &&
               !roundTrip[5].checked && roundTrip[6].checked && !roundTrip[6].enabled &&
               roundTrip[2].resource == "home" && roundTrip[1].resource.empty() &&
               roundTrip[4].style.states != nullptr &&
               sameColor(roundTrip[4].style.states->selectedBackground,
                         scene.specs[4].style.states->selectedBackground) &&
               sameColor(roundTrip[4].style.states->selectedForeground,
                         scene.specs[4].style.states->selectedForeground),
           "XML writer round-trips button behavior, icon, latched state, and selected colors");
}

void testButtonVariantBehavior()
{
    using namespace lithe;
    expect(resolveWidgetState(true, false, false, true) == WidgetState::selected &&
               resolveWidgetState(true, true, false, true) == WidgetState::focused &&
               resolveWidgetState(true, true, true, true) == WidgetState::pressed &&
               resolveWidgetState(false, true, true, true) == WidgetState::disabled,
           "selected state ranks below disabled, pressed, and focused");
    Style plain{};
    plain.background = Color::rgb(0x102030U);
    plain.foreground = Color::rgb(0x405060U);
    const Style unstyledSelected = resolveWidgetStyle(plain, WidgetState::selected);
    expect(sameColor(unstyledSelected.background, plain.background) &&
               sameColor(unstyledSelected.foreground, plain.foreground),
           "omitted selected colors keep the normal pair");
    WidgetStateStyle selectedOnly{};
    selectedOnly.selectedBackground = Color::rgb(0x708090U);
    plain.states = &selectedOnly;
    const Style partlySelected = resolveWidgetStyle(plain, WidgetState::selected);
    expect(sameColor(partlySelected.background, selectedOnly.selectedBackground) &&
               sameColor(partlySelected.foreground, plain.foreground),
           "selected background and foreground resolve independently");

    static ButtonScene scene{};
    expect(scene.build(), "button behavior scene builds");
    View* root = scene.layout.root();
    auto* open = scene.layout.find<ButtonView>(1U);
    auto* iconOnly = scene.layout.find<ButtonView>(2U);
    auto* alarm = scene.layout.find<ButtonView>(4U);
    auto* mute = scene.layout.find<ButtonView>(5U);
    auto* lock = scene.layout.find<ButtonView>(6U);
    if (root == nullptr || open == nullptr || iconOnly == nullptr || alarm == nullptr ||
        mute == nullptr || lock == nullptr)
    {
        expect(false, "button behavior scene exposes every button");
        return;
    }
    expect(open->behavior() == ButtonBehavior::momentary && !open->selected() &&
               iconOnly->icon() != nullptr && alarm->selected() && !mute->selected(),
           "layout applies behavior, icon, and initial latched state");
    expect(lock->selected() && lock->widgetState() == WidgetState::disabled && !lock->focusable(),
           "a disabled toggle keeps its value but presents and behaves as disabled");
    expect(!open->setSelected(true) && !open->selected() && open->setSelected(false),
           "a momentary button rejects selection");

    static FormatTestDisplay<PixelFormat::rgb565, kButtonSceneWidth, kButtonSceneHeight> display{};
    CountingDelegate delegate{};
    ViewController controller{*root, delegate};
    Application application{display};
    expect(application.push(controller) && application.render(), "button scene renders");
    const auto pixelAt = [](Point point) noexcept
    {
        return display.pixels[(detail::nonnegativeSize(point.y) *
                               detail::nonnegativeSize(kButtonSceneWidth)) +
                              detail::nonnegativeSize(point.x)];
    };
    expect(pixelAt(kIconOnlySolidPixel) == Color::fromLegacyValue(0x0080f0ffU).rgb565(),
           "an alpha icon is tinted with the normal foreground");

    controller.activate();
    expect(controller.focusedView() == open && controller.handleKey(Key::select) &&
               delegate.lastAction == 1U && !open->selected(),
           "push-button activation reports a momentary action without latching");
    expect(controller.handleKey(Key::down) && controller.focusedView() == iconOnly &&
               application.render() &&
               pixelAt(kIconOnlySolidPixel) == Color::rgb(0xffffffU).rgb565(),
           "an alpha icon follows the resolved focused foreground");

    const Rect muteFrame = mute->frameInRoot();
    const Point inside{muteFrame.x + 8, muteFrame.y + 8};
    const Point outside{muteFrame.x + 8, muteFrame.bottom() + 8};
    expect(controller.handlePointer({inside, PointerPhase::pressed, 1U}) && application.render() &&
               controller.focusedView() == mute && mute->pressed() && !mute->selected(),
           "pointer press focuses and presses a toggle without latching it");
    const std::size_t presentsBefore = display.presentCount;
    expect(controller.handlePointer({inside, PointerPhase::released, 1U}) && mute->selected() &&
               delegate.lastAction == 5U,
           "a completed pointer activation latches a toggle before reporting its action");
    expect(application.render() && display.presentCount == presentsBefore + 1U &&
               display.lastRegionCount == 1U && display.lastRegion.x == muteFrame.x &&
               display.lastRegion.y == muteFrame.y && display.lastRegion.width == muteFrame.width &&
               display.lastRegion.height == muteFrame.height,
           "latching a toggle presents only that button, not the whole display");
    const std::size_t actionsBefore = delegate.actions;
    expect(controller.handlePointer({inside, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({outside, PointerPhase::moved, 1U}) &&
               !controller.handlePointer({outside, PointerPhase::released, 1U}) &&
               mute->selected() && delegate.actions == actionsBefore,
           "releasing outside a toggle cancels activation and keeps the latched value");
    expect(controller.handleKey(Key::select) && !mute->selected() && delegate.lastAction == 5U,
           "push-button activation releases a latched toggle");

    alarm->setBehavior(ButtonBehavior::momentary);
    expect(!alarm->selected() && alarm->behavior() == ButtonBehavior::momentary,
           "changing a toggle to momentary clears its latched value");
    alarm->setBehavior(ButtonBehavior::toggle);
    expect(alarm->setSelected(true) && alarm->selected(), "a toggle accepts an explicit value");
    iconOnly->clearIcon();
    expect(iconOnly->icon() == nullptr, "an icon can be cleared at run time");
}

void testButtonVariantGoldens()
{
    const ButtonSceneRender rgb565 = renderButtonScene<lithe::PixelFormat::rgb565>();
    const ButtonSceneRender xrgb = renderButtonScene<lithe::PixelFormat::xrgb8888>();
    expect(rgb565.tilesMatch && xrgb.tilesMatch,
           "button variant tiles match full-frame RGB565 and XRGB8888 output");
    expectCrc32(rgb565.crc, kButtonSceneRgb565Golden, "RGB565 button variant golden");
    expectCrc32(xrgb.crc, kButtonSceneXrgb8888Golden, "XRGB8888 button variant golden");
}

constexpr std::int32_t kTabSceneWidth{240};
constexpr std::int32_t kTabSceneHeight{120};
constexpr std::size_t kTabSceneCapacity{12U};
// A three-page tab view with a selected-color strip above a four-tab bar that
// does not wrap. Every color is explicit so goldens do not depend on defaults.
constexpr std::string_view kTabSceneXml =
    "<panel frame=\"0,0,240,120\" backgroundColor=\"0x00302010\">"
    "<tabview frame=\"4,4,232,84\" runtimeID=\"20\" items=\"HOME|SETUP|INFO\" selectedIndex=\"1\" "
    "tabHeight=\"24\" backgroundColor=\"0x00402818\" textColor=\"0x00ffffff\" "
    "borderColor=\"0x00a0a0a0\" borderWidth=\"1\" focusedBackgroundColor=\"0x00df6c2d\" "
    "selectedBackgroundColor=\"0x00fae4d6\" selectedForegroundColor=\"0x00803d1a\">"
    "<panel frame=\"0,24,232,60\" runtimeID=\"21\" backgroundColor=\"0x00203040\">"
    "<label frame=\"8,8,120,20\" text=\"HOME PAGE\" textColor=\"0x00ffffff\"/>"
    "</panel>"
    "<panel frame=\"0,24,232,60\" runtimeID=\"22\" backgroundColor=\"0x00402818\">"
    "<button frame=\"8,8,100,28\" runtimeID=\"25\" text=\"APPLY\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\"/>"
    "</panel>"
    "<panel frame=\"0,24,232,60\" runtimeID=\"23\" backgroundColor=\"0x00183828\">"
    "<label frame=\"8,8,120,20\" text=\"VERSION 1\" textColor=\"0x00ffffff\"/>"
    "</panel>"
    "</tabview>"
    "<tabbar frame=\"4,92,232,24\" runtimeID=\"30\" items=\"A|B|C|D\" wrap=\"0\" "
    "backgroundColor=\"0x00604020\" textColor=\"0x00ffffff\"/>"
    "</panel>";
constexpr std::uint32_t kTabSceneRgb565Golden{0x9663d32bU};
constexpr std::uint32_t kTabSceneXrgb8888Golden{0x57db23ebU};

struct TabScene
{
    std::array<lithe::ViewSpec, kTabSceneCapacity> specs{};
    std::array<lithe::WidgetStateStyle, kTabSceneCapacity> stateStyles{};
    lithe::StaticLayout<kTabSceneCapacity> layout{};
    std::size_t count{0U};

    bool build() noexcept
    {
        const lithe::XmlLayoutResult parsed = lithe::XmlLayoutParser::parse(
            kTabSceneXml, specs, lithe::XmlParseOptions{true, {}, stateStyles});
        count = parsed.nodeCount;
        return parsed.ok() &&
               layout.build(std::span<const lithe::ViewSpec>{specs}.first(count)).ok();
    }
};

template <lithe::PixelFormat Format> ButtonSceneRender renderTabScene() noexcept
{
    static TabScene scene{};
    static FormatTestDisplay<Format, kTabSceneWidth, kTabSceneHeight> display{};
    if (!scene.build() || scene.layout.root() == nullptr)
    {
        return {};
    }
    lithe::ViewController controller{*scene.layout.root()};
    lithe::Application application{display};
    if (!application.push(controller) || !application.render())
    {
        return {};
    }

    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    std::array<Storage, std::size_t{73U} * 11U> tilePixels{};
    const lithe::Surface workspace{lithe::PixelSurface<Format>{tilePixels, 73, 11, 73}};
    ComparingTileSink sink{display.buffer(), {kTabSceneWidth, kTabSceneHeight}};
    const lithe::TileRenderResult tiles = lithe::TiledRenderer::render(
        application, {kTabSceneWidth, kTabSceneHeight}, workspace, sink);
    return {crc32Buffer(display.buffer()), tiles.ok() && sink.matches()};
}

void testTabXml()
{
    using namespace lithe;
    static TabScene scene{};
    expect(scene.build() && scene.count == 9U, "strict XML builds a tab view and a tab bar");
    expect(scene.specs[1].kind == ViewKind::tabView && scene.specs[1].tabHeight == 24U &&
               scene.specs[1].selectedIndex == 1U && scene.specs[1].text == "HOME|SETUP|INFO" &&
               scene.specs[8].kind == ViewKind::tabBar && !scene.specs[8].wraps &&
               scene.specs[8].tabHeight == detail::kDefaultTabHeightPixels,
           "XML maps tab titles, selection, wrap, and strip height");

    std::array<ViewSpec, 2> specs{};
    std::array<WidgetStateStyle, 2> stateStyles{};
    const XmlParseOptions strict{true, {}, stateStyles};
    expect(XmlLayoutParser::parse("<tab-bar frame=\"0,0,40,20\" items=\"A|B\"/>", specs, strict)
                   .ok() &&
               specs[0].kind == ViewKind::tabBar &&
               XmlLayoutParser::parse("<tab-view frame=\"0,0,40,40\" items=\"A\"/>", specs, strict)
                   .ok() &&
               specs[0].kind == ViewKind::tabView,
           "hyphenated tab element aliases are accepted");
    expect(XmlLayoutParser::parse("<tabbar frame=\"0,0,40,20\"/>", specs, strict).error ==
               XmlLayoutError::invalidAttribute,
           "strict XML rejects a tab bar without tabs");
    expect(XmlLayoutParser::parse(
               "<tabbar frame=\"0,0,40,20\" items=\"A|B|C|D|E|F|G|H|I|J|K|L|M|N|O|P|Q\"/>",
               specs,
               strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects more than sixteen tabs");
    expect(XmlLayoutParser::parse(
               "<tabview frame=\"0,0,40,40\" items=\"A|B\" selectedIndex=\"2\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects a selected tab beyond the titles");
    expect(XmlLayoutParser::parse(
               "<tabbar frame=\"0,0,40,20\" items=\"A\" tabHeight=\"20\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects a strip height on a tab bar");
    expect(XmlLayoutParser::parse(
               "<picker frame=\"0,0,40,20\" items=\"A\" tabHeight=\"20\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects a strip height on another widget kind");
    expect(XmlLayoutParser::parse(
               "<tabview frame=\"0,0,40,40\" items=\"A\" tabHeight=\"7\"/>", specs, strict)
                       .error == XmlLayoutError::invalidAttribute &&
               XmlLayoutParser::parse(
                   "<tabview frame=\"0,0,40,40\" items=\"A\" tabHeight=\"256\"/>", specs, strict)
                       .error == XmlLayoutError::invalidAttribute,
           "XML rejects a strip height outside eight through 255 pixels");

    const XmlLayoutResult compatible = XmlLayoutParser::parse(
        "<object className=\"LCDTABVIEW\" frame=\"0,0,40,40\" items=\"A|B\" selectedIndex=\"9\"/>",
        specs);
    StaticLayout<2> legacy{};
    expect(compatible.ok() &&
               legacy.build(std::span<const ViewSpec>{specs}.first(compatible.nodeCount)).ok() &&
               legacy.find<TabView>(0U) != nullptr &&
               legacy.find<TabView>(0U)->selectedIndex() == 1U,
           "compatibility XML clamps an out-of-range tab selection");

    std::array<char, 4096> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{scene.specs}.first(scene.count), output);
    std::array<ViewSpec, kTabSceneCapacity> roundTrip{};
    std::array<WidgetStateStyle, kTabSceneCapacity> roundTripStates{};
    const XmlLayoutResult reparsed =
        XmlLayoutParser::parse(std::string_view{output.data(), written.bytesWritten},
                               roundTrip,
                               XmlParseOptions{true, {}, roundTripStates});
    expect(written.ok() && reparsed.ok() && reparsed.nodeCount == scene.count &&
               roundTrip[1].kind == ViewKind::tabView && roundTrip[1].tabHeight == 24U &&
               roundTrip[1].selectedIndex == 1U && roundTrip[1].text == "HOME|SETUP|INFO" &&
               roundTrip[8].kind == ViewKind::tabBar && !roundTrip[8].wraps &&
               roundTrip[2].parent == 1U && roundTrip[1].style.states != nullptr &&
               sameColor(roundTrip[1].style.states->selectedBackground,
                         scene.specs[1].style.states->selectedBackground),
           "XML writer round-trips tab kinds, titles, selection, wrap, and strip height");
}

void testTabBehavior()
{
    using namespace lithe;
    static TabScene scene{};
    expect(scene.build(), "tab behavior scene builds");
    View* root = scene.layout.root();
    auto* tabs = scene.layout.find<TabView>(20U);
    auto* bar = scene.layout.find<TabBarView>(30U);
    View* home = scene.layout.find(21U);
    View* setup = scene.layout.find(22U);
    View* info = scene.layout.find(23U);
    View* apply = scene.layout.find(25U);
    if (root == nullptr || tabs == nullptr || bar == nullptr || home == nullptr ||
        setup == nullptr || info == nullptr || apply == nullptr)
    {
        expect(false, "tab behavior scene exposes every view");
        return;
    }
    expect(tabs->tabCount() == 3U && tabs->title(0U) == "HOME" && tabs->title(2U) == "INFO" &&
               tabs->title(3U).empty() && tabs->tabHeight() == 24U &&
               tabs->contentFrame().y == 24 && tabs->contentFrame().height == 60,
           "a tab view exposes bounded titles and its content frame");
    expect(!home->visible() && setup->visible() && !info->visible(),
           "layout shows only the initially selected page");

    static FormatTestDisplay<PixelFormat::rgb565, kTabSceneWidth, kTabSceneHeight> display{};
    CountingDelegate delegate{};
    ViewController controller{*root, delegate};
    Application application{display};
    expect(application.push(controller) && application.render(), "tab scene renders");

    controller.activate();
    expect(controller.focusedView() == tabs && controller.handleKey(Key::down) &&
               controller.focusedView() == apply && controller.handleKey(Key::down) &&
               controller.focusedView() == bar && controller.handleKey(Key::down) &&
               controller.focusedView() == tabs,
           "focus visits the strip, the visible page, and the bar; hidden pages are skipped");

    const Rect tabsFrame = tabs->frameInRoot();
    expect(application.render(), "focus traversal renders");
    const std::size_t presentsBefore = display.presentCount;
    expect(controller.handleKey(Key::right) && tabs->selectedIndex() == 2U && info->visible() &&
               !setup->visible() && delegate.lastAction == 20U && delegate.actions == 1U,
           "right selects the next tab, shows its page, and reports the action");
    expect(application.render() && display.presentCount == presentsBefore + 1U &&
               display.lastRegionCount == 1U && display.lastRegion.x == tabsFrame.x &&
               display.lastRegion.y == tabsFrame.y && display.lastRegion.width == tabsFrame.width &&
               display.lastRegion.height == tabsFrame.height,
           "a page change presents only the tab view");
    expect(controller.handleKey(Key::right) && tabs->selectedIndex() == 0U && home->visible() &&
               controller.handleKey(Key::left) && tabs->selectedIndex() == 2U &&
               delegate.actions == 3U,
           "a wrapping tab view steps across both ends");
    expect(controller.handleKey(Key::select) && tabs->selectedIndex() == 0U &&
               delegate.actions == 4U && delegate.lastAction == 20U,
           "select advances to the next tab and wraps for encoder users");

    controller.handleKey(Key::up);
    expect(controller.focusedView() == bar && bar->selectedIndex() == 0U, "the bar takes focus");
    const std::size_t actionsAtStart = delegate.actions;
    expect(controller.handleKey(Key::left) && bar->selectedIndex() == 0U &&
               delegate.actions == actionsAtStart,
           "a non-wrapping bar consumes left at its first tab without an action");
    expect(controller.handleKey(Key::right) && bar->selectedIndex() == 1U &&
               delegate.lastAction == 30U && delegate.actions == actionsAtStart + 1U,
           "a bar reports a key-driven change once");
    bar->setSelectedIndex(9U);
    expect(bar->selectedIndex() == 3U && controller.handleKey(Key::right) &&
               bar->selectedIndex() == 3U && delegate.actions == actionsAtStart + 1U,
           "programmatic selection clamps and the last tab does not wrap");
    expect(controller.handleKey(Key::select) && bar->selectedIndex() == 0U,
           "select always wraps so an encoder can reach every tab");

    // Strip pointer presses: HOME occupies the first third of the tab view strip.
    const Point homeTab{tabsFrame.x + 20, tabsFrame.y + 10};
    const Point setupTab{tabsFrame.x + 100, tabsFrame.y + 10};
    const Point outside{tabsFrame.x + 20, tabsFrame.bottom() + 20};
    tabs->setSelectedIndex(2U);
    const std::size_t actionsBeforePointer = delegate.actions;
    expect(controller.handlePointer({homeTab, PointerPhase::pressed, 1U}) &&
               controller.focusedView() == tabs && tabs->pressed() && tabs->selectedIndex() == 2U,
           "a pointer press focuses the tab view without changing the selection");
    expect(controller.handlePointer({homeTab, PointerPhase::released, 1U}) &&
               tabs->selectedIndex() == 0U && home->visible() &&
               delegate.actions == actionsBeforePointer + 1U && delegate.lastAction == 20U,
           "releasing on a tab selects it and reports one action");
    expect(controller.handlePointer({homeTab, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({setupTab, PointerPhase::moved, 1U}) &&
               controller.handlePointer({setupTab, PointerPhase::released, 1U}) &&
               tabs->selectedIndex() == 1U,
           "dragging along the strip selects the tab under the release");
    const std::size_t actionsBeforeCancel = delegate.actions;
    expect(controller.handlePointer({homeTab, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({outside, PointerPhase::moved, 1U}) &&
               !controller.handlePointer({outside, PointerPhase::released, 1U}) &&
               tabs->selectedIndex() == 1U && delegate.actions == actionsBeforeCancel,
           "releasing outside the tab view cancels the selection");
    const Point pageBackground{tabsFrame.x + 200, tabsFrame.y + 70};
    expect(controller.handlePointer({pageBackground, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({pageBackground, PointerPhase::released, 1U}) &&
               tabs->selectedIndex() == 1U,
           "a release on the page background does not change the tab");
    expect(controller.handleKey(Key::select) && tabs->selectedIndex() == 2U,
           "a pointer release leaves no pending activation for the next select");

    tabs->setSelectedIndex(1U);
    controller.handleKey(Key::down);
    expect(controller.focusedView() == apply, "focus reaches a control on the visible page");
    tabs->setSelectedIndex(0U);
    expect(!apply->visible() || !setup->visible(), "programmatic selection hides the page");
    expect(controller.focusedView() == nullptr,
           "focus inside a page that becomes hidden is released");

    bar->setEnabled(false);
    expect(!bar->focusable() && bar->widgetState() == WidgetState::disabled,
           "a disabled tab bar cannot take focus");

    View overlapRoot{{0, 0, 120, 80}};
    TabView overlapTabs{{0, 0, 120, 80}, font5x7(), "ONE|TWO", 0U, 20U};
    View overlapPage{{0, 0, 120, 80}};
    ButtonView overlapButton{{0, 0, 120, 40}, font5x7(), "COVER"};
    expect(overlapRoot.addSubview(overlapTabs) && overlapTabs.addSubview(overlapPage) &&
               overlapPage.addSubview(overlapButton),
           "an overlapping page constructs");
    overlapTabs.syncPages();
    ViewController overlapController{overlapRoot};
    expect(overlapController.handlePointer({{90, 8}, PointerPhase::pressed, 1U}) &&
               overlapController.focusedView() == &overlapTabs &&
               overlapController.handlePointer({{90, 8}, PointerPhase::released, 1U}) &&
               overlapTabs.selectedIndex() == 1U && !overlapPage.visible(),
           "the strip receives its own presses even under an overlapping page");
}

void testTabGoldens()
{
    const ButtonSceneRender rgb565 = renderTabScene<lithe::PixelFormat::rgb565>();
    const ButtonSceneRender xrgb = renderTabScene<lithe::PixelFormat::xrgb8888>();
    expect(rgb565.tilesMatch && xrgb.tilesMatch,
           "tab tiles match full-frame RGB565 and XRGB8888 output");
    expectCrc32(rgb565.crc, kTabSceneRgb565Golden, "RGB565 tab golden");
    expectCrc32(xrgb.crc, kTabSceneXrgb8888Golden, "XRGB8888 tab golden");
}

constexpr std::int32_t kWheelSceneWidth{240};
constexpr std::int32_t kWheelSceneHeight{150};
constexpr std::size_t kWheelSceneCapacity{6U};
// A wrapping day wheel with selected colors beside a non-wrapping, bordered step wheel and a
// button. Every color is explicit so goldens do not depend on defaults.
constexpr std::string_view kWheelSceneXml =
    "<panel frame=\"0,0,240,150\" backgroundColor=\"0x00302010\">"
    "<wheelpicker frame=\"8,8,100,104\" runtimeID=\"40\" items=\"MON|TUE|WED|THU|FRI|SAT|SUN\" "
    "selectedIndex=\"2\" rowHeight=\"24\" backgroundColor=\"0x00402818\" textColor=\"0x00ffffff\" "
    "accentColor=\"0x00df6c2d\" focusedBackgroundColor=\"0x00df6c2d\" "
    "pressedBackgroundColor=\"0x00b35724\" selectedBackgroundColor=\"0x00fae4d6\" "
    "selectedForegroundColor=\"0x00803d1a\"/>"
    "<wheel-picker frame=\"120,8,112,104\" runtimeID=\"41\" items=\"0|5|10|15|20\" wrap=\"0\" "
    "rowHeight=\"20\" backgroundColor=\"0x00183828\" textColor=\"0x00ffffff\" "
    "accentColor=\"0x00a0a0a0\" borderColor=\"0x00a0a0a0\" borderWidth=\"1\"/>"
    "<button frame=\"8,116,224,28\" runtimeID=\"42\" text=\"SAVE\" backgroundColor=\"0x00604020\" "
    "textColor=\"0x00ffffff\"/>"
    "</panel>";
constexpr std::uint32_t kWheelSceneRgb565Golden{0x784208d4U};
constexpr std::uint32_t kWheelSceneXrgb8888Golden{0xcc7c5099U};

struct WheelScene
{
    std::array<lithe::ViewSpec, kWheelSceneCapacity> specs{};
    std::array<lithe::WidgetStateStyle, kWheelSceneCapacity> stateStyles{};
    lithe::StaticLayout<kWheelSceneCapacity> layout{};
    std::size_t count{0U};

    bool build() noexcept
    {
        const lithe::XmlLayoutResult parsed = lithe::XmlLayoutParser::parse(
            kWheelSceneXml, specs, lithe::XmlParseOptions{true, {}, stateStyles});
        count = parsed.nodeCount;
        return parsed.ok() &&
               layout.build(std::span<const lithe::ViewSpec>{specs}.first(count)).ok();
    }
};

template <lithe::PixelFormat Format> ButtonSceneRender renderWheelScene() noexcept
{
    static WheelScene scene{};
    static FormatTestDisplay<Format, kWheelSceneWidth, kWheelSceneHeight> display{};
    if (!scene.build() || scene.layout.root() == nullptr)
    {
        return {};
    }
    lithe::ViewController controller{*scene.layout.root()};
    lithe::Application application{display};
    if (!application.push(controller) || !application.render())
    {
        return {};
    }

    using Storage = typename lithe::PixelFormatTraits<Format>::Storage;
    std::array<Storage, std::size_t{73U} * 11U> tilePixels{};
    const lithe::Surface workspace{lithe::PixelSurface<Format>{tilePixels, 73, 11, 73}};
    ComparingTileSink sink{display.buffer(), {kWheelSceneWidth, kWheelSceneHeight}};
    const lithe::TileRenderResult tiles = lithe::TiledRenderer::render(
        application, {kWheelSceneWidth, kWheelSceneHeight}, workspace, sink);
    return {crc32Buffer(display.buffer()), tiles.ok() && sink.matches()};
}

void testWheelPickerXml()
{
    using namespace lithe;
    static WheelScene scene{};
    expect(scene.build() && scene.count == 4U, "strict XML builds two wheel pickers and a button");
    expect(scene.specs[1].kind == ViewKind::wheelPicker && scene.specs[1].rowHeight == 24U &&
               scene.specs[1].selectedIndex == 2U && scene.specs[1].wraps &&
               scene.specs[2].kind == ViewKind::wheelPicker && !scene.specs[2].wraps &&
               scene.specs[2].rowHeight == 20U &&
               scene.specs[3].rowHeight == detail::kDefaultWheelRowHeightPixels,
           "XML maps wheel items, selection, wrap, and row height");

    std::array<ViewSpec, 2> specs{};
    std::array<WidgetStateStyle, 2> stateStyles{};
    const XmlParseOptions strict{true, {}, stateStyles};
    expect(XmlLayoutParser::parse("<wheelpicker frame=\"0,0,40,60\"/>", specs, strict).error ==
               XmlLayoutError::invalidAttribute,
           "strict XML rejects a wheel without items");
    expect(
        XmlLayoutParser::parse(
            "<wheelpicker frame=\"0,0,40,60\" items=\"A|B\" selectedIndex=\"2\"/>", specs, strict)
                .error == XmlLayoutError::invalidAttribute,
        "strict XML rejects a selection beyond the items");
    std::array<char, 1024> many{};
    std::size_t length = 0U;
    for (const char character : std::string_view{"<wheelpicker frame=\"0,0,40,60\" items=\"A"})
    {
        many[length++] = character;
    }
    for (std::size_t index = 0U; index < WheelPickerView::kMaximumItems; ++index)
    {
        many[length++] = '|';
        many[length++] = 'A';
    }
    for (const char character : std::string_view{"\"/>"})
    {
        many[length++] = character;
    }
    expect(XmlLayoutParser::parse(std::string_view{many.data(), length}, specs, strict).error ==
               XmlLayoutError::invalidAttribute,
           "strict XML rejects more than 255 wheel items");
    expect(XmlLayoutParser::parse(
               "<wheelpicker frame=\"0,0,40,60\" items=\"A\" rowHeight=\"11\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "XML rejects a row height below twelve pixels");
    expect(XmlLayoutParser::parse(
               "<picker frame=\"0,0,40,20\" items=\"A\" rowHeight=\"20\"/>", specs, strict)
                   .error == XmlLayoutError::invalidAttribute,
           "strict XML rejects a row height on another widget kind");

    const XmlLayoutResult compatible = XmlLayoutParser::parse(
        "<object className=\"LCDWHEELPICKERVIEW\" frame=\"0,0,40,60\" items=\"A|B\" "
        "selectedIndex=\"9\"/>",
        specs);
    StaticLayout<2> legacy{};
    expect(compatible.ok() &&
               legacy.build(std::span<const ViewSpec>{specs}.first(compatible.nodeCount)).ok() &&
               legacy.find<WheelPickerView>(0U) != nullptr &&
               legacy.find<WheelPickerView>(0U)->selectedIndex() == 1U,
           "compatibility XML clamps an out-of-range wheel selection");

    std::array<char, 4096> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{scene.specs}.first(scene.count), output);
    std::array<ViewSpec, kWheelSceneCapacity> roundTrip{};
    std::array<WidgetStateStyle, kWheelSceneCapacity> roundTripStates{};
    const XmlLayoutResult reparsed =
        XmlLayoutParser::parse(std::string_view{output.data(), written.bytesWritten},
                               roundTrip,
                               XmlParseOptions{true, {}, roundTripStates});
    expect(written.ok() && reparsed.ok() && reparsed.nodeCount == scene.count &&
               roundTrip[1].kind == ViewKind::wheelPicker && roundTrip[1].rowHeight == 24U &&
               roundTrip[1].selectedIndex == 2U &&
               roundTrip[1].text == "MON|TUE|WED|THU|FRI|SAT|SUN" && !roundTrip[2].wraps &&
               roundTrip[2].rowHeight == 20U,
           "XML writer round-trips wheel items, selection, wrap, and row height");
}

void testWheelPickerBehavior()
{
    using namespace lithe;
    static WheelScene scene{};
    expect(scene.build(), "wheel behavior scene builds");
    View* root = scene.layout.root();
    auto* days = scene.layout.find<WheelPickerView>(40U);
    auto* steps = scene.layout.find<WheelPickerView>(41U);
    View* save = scene.layout.find(42U);
    if (root == nullptr || days == nullptr || steps == nullptr || save == nullptr)
    {
        expect(false, "wheel behavior scene exposes every view");
        return;
    }
    expect(days->itemCount() == 7U && days->selectedItem() == "WED" && days->item(6U) == "SUN" &&
               days->item(7U).empty() && days->rowHeight() == 24U && steps->itemCount() == 5U,
           "a wheel exposes bounded items, the selection, and its row height");

    static FormatTestDisplay<PixelFormat::rgb565, kWheelSceneWidth, kWheelSceneHeight> display{};
    CountingDelegate delegate{};
    ViewController controller{*root, delegate};
    Application application{display};
    expect(application.push(controller) && application.render(), "wheel scene renders");

    controller.activate();
    expect(controller.focusedView() == days && controller.handleKey(Key::down) &&
               controller.focusedView() == steps && controller.handleKey(Key::up) &&
               controller.focusedView() == days && !days->editing(),
           "outside editing, up and down (encoder rotation) move focus past a wheel");

    expect(controller.handleKey(Key::select) && days->editing() && delegate.actions == 0U,
           "select (encoder push) starts editing without an action");
    expect(application.render(), "editing state renders");
    const Rect daysFrame = days->frameInRoot();
    const std::size_t presentsBefore = display.presentCount;
    expect(controller.handleKey(Key::down) && days->selectedItem() == "THU" &&
               delegate.actions == 1U && delegate.lastAction == 40U,
           "down (rotation) steps the value while editing and reports it once");
    expect(application.render() && display.presentCount == presentsBefore + 1U &&
               display.lastRegionCount == 1U && display.lastRegion.x == daysFrame.x &&
               display.lastRegion.y == daysFrame.y && display.lastRegion.width == daysFrame.width &&
               display.lastRegion.height == daysFrame.height,
           "a value step presents only the wheel");
    expect(controller.handleKey(Key::down) && controller.handleKey(Key::up) &&
               days->selectedItem() == "THU" && delegate.actions == 3U,
           "each step while editing is reported");
    expect(controller.handleKey(Key::back) && !days->editing() && days->selectedItem() == "WED" &&
               delegate.actions == 4U,
           "back cancels editing and restores the value from before editing");
    expect(controller.handleKey(Key::select) && controller.handleKey(Key::up) &&
               controller.handleKey(Key::select) && !days->editing() &&
               days->selectedItem() == "TUE",
           "select confirms the edited value");
    days->setSelectedIndex(6U);
    expect(controller.handleKey(Key::select) && controller.handleKey(Key::down) &&
               days->selectedItem() == "MON" && controller.handleKey(Key::select),
           "a wrapping wheel steps from the last item to the first");

    controller.handleKey(Key::down);
    expect(controller.focusedView() == steps, "focus reaches the second wheel");
    const std::size_t actionsAtStart = delegate.actions;
    expect(controller.handleKey(Key::select) && controller.handleKey(Key::up) &&
               steps->selectedIndex() == 0U && delegate.actions == actionsAtStart,
           "a non-wrapping wheel consumes up at its first item without an action");
    for (int step = 0; step < 6; ++step)
    {
        controller.handleKey(Key::down);
    }
    expect(steps->selectedItem() == "20" && delegate.actions == actionsAtStart + 4U,
           "a non-wrapping wheel stops at its last item");
    expect(controller.handleKey(Key::select) && !steps->editing(), "select ends editing");

    expect(controller.handleKey(Key::select) && steps->editing(), "editing restarts");
    const Point saveCenter{save->frameInRoot().x + 20, save->frameInRoot().y + 10};
    expect(controller.handlePointer({saveCenter, PointerPhase::pressed, 1U}) &&
               controller.focusedView() == save && !steps->editing() &&
               controller.handlePointer({saveCenter, PointerPhase::released, 1U}),
           "losing focus ends editing and keeps the value");

    // The day wheel's band spans local y 40 through 63 (104-pixel frame, 24-pixel rows).
    days->setSelectedIndex(2U);
    const std::int32_t x = daysFrame.x + 50;
    const std::int32_t center = daysFrame.y + 52;
    const std::size_t actionsBeforeTap = delegate.actions;
    expect(controller.handlePointer({{x, daysFrame.y + 20}, PointerPhase::pressed, 1U}) &&
               controller.focusedView() == days &&
               controller.handlePointer({{x, daysFrame.y + 20}, PointerPhase::released, 1U}) &&
               days->selectedItem() == "TUE" && delegate.actions == actionsBeforeTap + 1U &&
               delegate.lastAction == 40U,
           "a tap on the row above the band selects it and reports one action");
    expect(controller.handlePointer({{x, center}, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({{x, center + 50}, PointerPhase::moved, 1U}) &&
               controller.handlePointer({{x, center + 50}, PointerPhase::released, 1U}) &&
               days->selectedItem() == "SUN",
           "dragging down two rows snaps to the item two before (wrapping)");
    const std::size_t actionsBeforeOutside = delegate.actions;
    expect(controller.handlePointer({{x, center}, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({{x, center - 30}, PointerPhase::moved, 1U}) &&
               controller.handlePointer({{x + 200, center - 30}, PointerPhase::moved, 1U}) &&
               controller.handlePointer({{x + 200, center - 30}, PointerPhase::released, 1U}) &&
               days->selectedItem() == "MON" && delegate.actions == actionsBeforeOutside + 1U,
           "a drag that lifts outside the wheel still snaps and reports its change");
    expect(controller.handlePointer({{x, center}, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({{x, center + 40}, PointerPhase::moved, 1U}) &&
               controller.handlePointer({{x, center + 40}, PointerPhase::cancelled, 1U}) &&
               days->selectedItem() == "MON",
           "a cancelled drag changes nothing");
    // A contact thousands of cycles away: the wrapping offset keeps only the remainder, so the
    // wheel still draws just its visible rows and snaps exactly like a two-row drag.
    const std::int32_t cycle = 7 * 24;
    const std::int32_t far = center + (5000 * cycle) + 48;
    expect(controller.handlePointer({{x, center}, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({{x, far}, PointerPhase::moved, 1U}) &&
               application.render() &&
               controller.handlePointer({{x, far}, PointerPhase::released, 1U}) &&
               days->selectedItem() == "SAT",
           "a drag many cycles long draws only visible rows and snaps like its remainder");

    steps->setSelectedIndex(0U);
    const Rect stepsFrame = steps->frameInRoot();
    const std::int32_t stepsX = stepsFrame.x + 50;
    const std::int32_t stepsCenter = stepsFrame.y + 52;
    const std::size_t actionsBeforeClamp = delegate.actions;
    expect(controller.handlePointer({{stepsX, stepsCenter}, PointerPhase::pressed, 1U}) &&
               controller.handlePointer({{stepsX, stepsCenter + 45}, PointerPhase::moved, 1U}) &&
               controller.handlePointer({{stepsX, stepsCenter + 45}, PointerPhase::released, 1U}) &&
               steps->selectedIndex() == 0U,
           "dragging a non-wrapping wheel past its first item is a clamped drag, not a tap");
    expect(delegate.actions == actionsBeforeClamp + 1U,
           "a release inside still reports the widget action, as for any focusable view");

    steps->setEnabled(false);
    expect(!steps->focusable() && steps->widgetState() == WidgetState::disabled,
           "a disabled wheel cannot take focus");
}

void testWheelPickerGoldens()
{
    const ButtonSceneRender rgb565 = renderWheelScene<lithe::PixelFormat::rgb565>();
    const ButtonSceneRender xrgb = renderWheelScene<lithe::PixelFormat::xrgb8888>();
    expect(rgb565.tilesMatch && xrgb.tilesMatch,
           "wheel tiles match full-frame RGB565 and XRGB8888 output");
    expectCrc32(rgb565.crc, kWheelSceneRgb565Golden, "RGB565 wheel picker golden");
    expectCrc32(xrgb.crc, kWheelSceneXrgb8888Golden, "XRGB8888 wheel picker golden");
}

void testAutomaticScrollReveal()
{
    using namespace lithe;
    View root{{0, 0, 100, 80}};
    ScrollView scroll{{0, 0, 100, 30}};
    ButtonView first{{0, 0, 100, 18}, font5x7(), "ONE"};
    ButtonView second{{0, 22, 100, 18}, font5x7(), "TWO"};
    ButtonView third{{0, 44, 100, 18}, font5x7(), "THREE"};
    first.setId(1U);
    second.setId(2U);
    third.setId(3U);
    expect(root.addSubview(scroll) && scroll.addSubview(first) && scroll.addSubview(second) &&
               scroll.addSubview(third),
           "intrusive retained tree accepts bounded children");
    scroll.fitContentToSubviews();
    ViewController controller{root};
    controller.activate();
    expect(controller.focusedView() == &first, "controller focuses first scroll child");
    expect(controller.handleKey(Key::down) && controller.focusedView() == &second &&
               scroll.contentOffset() == 10,
           "scroll view reveals the next focused child");
    expect(controller.handleKey(Key::down) && controller.focusedView() == &third &&
               scroll.contentOffset() == 32,
           "scroll view clamps reveal offset to content extent");
}

void testPointerLifecycle()
{
    using namespace lithe;
    View root{{0, 0, 100, 60}};
    ButtonView button{{5, 5, 40, 24}, font5x7(), "OPEN"};
    button.setId(77U);
    expect(root.addSubview(button), "touch test constructs a retained tree");
    CountingDelegate delegate{};
    ViewController controller{root, delegate};
    controller.activate();

    expect(controller.handlePointer({{10, 10}, PointerPhase::pressed, 1U}) &&
               delegate.actions == 0U,
           "touch press captures and focuses without activating");
    expect(controller.handlePointer({{80, 50}, PointerPhase::moved, 1U}),
           "captured touch continues outside the control");
    expect(!controller.handlePointer({{80, 50}, PointerPhase::released, 1U}) &&
               delegate.actions == 0U,
           "release outside cancels activation");

    expect(controller.handlePointer({{10, 10}, PointerPhase::pressed, 2U}) &&
               !controller.handlePointer({{10, 10}, PointerPhase::moved, 3U}),
           "a captured contact rejects unrelated contact IDs");
    expect(controller.handlePointer({{20, 15}, PointerPhase::released, 2U}) &&
               delegate.actions == 1U && delegate.lastAction == 77U,
           "release inside activates the captured view once");

    expect(controller.handlePointer({{10, 10}, PointerPhase::pressed, 4U}) &&
               controller.handlePointer({{10, 10}, PointerPhase::cancelled, 4U}) &&
               delegate.actions == 1U,
           "cancelled touch clears capture without an action");

    View softkeyRoot{{0, 0, 100, 40}};
    SoftkeyView softkey{{0, 0, 100, 40}, font5x7(), "BACK", "OK", 11U, 12U};
    expect(softkeyRoot.addSubview(softkey), "softkey touch test constructs a retained tree");
    CountingDelegate softkeyDelegate{};
    ViewController softkeyController{softkeyRoot, softkeyDelegate};
    softkeyController.activate();
    expect(softkeyController.handlePointer({{10, 20}, PointerPhase::pressed, 0U}) &&
               softkeyController.handlePointer({{90, 20}, PointerPhase::moved, 0U}) &&
               softkeyController.handlePointer({{90, 20}, PointerPhase::released, 0U}) &&
               softkeyDelegate.lastAction == 12U,
           "softkey selection follows a captured touch before release");

    View scrollRoot{{0, 0, 100, 80}};
    ScrollView scroll{{0, 0, 100, 60}, 180};
    ButtonView scrollChild{{0, 0, 75, 24}, font5x7(), "FIRST"};
    expect(scrollRoot.addSubview(scroll) && scroll.addSubview(scrollChild),
           "scrollbar drag test constructs a retained tree");
    CountingDelegate scrollDelegate{};
    ViewController scrollController{scrollRoot, scrollDelegate};
    scrollController.activate();
    expect(scrollController.handlePointer({{95, 8}, PointerPhase::pressed, 5U}) &&
               scrollController.handlePointer({{95, 55}, PointerPhase::moved, 5U}) &&
               scrollController.handlePointer({{95, 55}, PointerPhase::released, 5U}),
           "scrollbar thumb keeps pointer capture throughout a drag");
    expect(scroll.contentOffset() == 120 && scrollDelegate.actions == 0U,
           "dragging the scrollbar reaches the content end without dispatching an action");
}

void testViewportAndGestureContracts()
{
    using namespace lithe;

    constexpr Viewport round = Viewport::circle({0, 0, 20, 20});
    expect(round.valid() && round.contains({9, 0}) && round.contains({10, 0}) &&
               round.contains({0, 9}) && round.contains({19, 10}) && !round.contains({0, 0}) &&
               !round.contains({19, 19}) && !round.contains({20, 10}),
           "circular viewport uses symmetric pixel-center containment and rejects corners");

    std::array<Pixel, 400> pixels{};
    DamageTracker damage{};
    Painter painter{Surface{pixels, 20, 20, 20}, damage, round};
    painter.fillRect({0, 0, 20, 20}, Color::rgb(0xff0000U));
    expect(pixels[0U] == Pixel{} && pixels[19U] == Pixel{} &&
               pixels[(10U * 20U) + 10U] == Color::rgb(0xff0000U).rgb565() && damage.size() == 1U,
           "Painter applies the circular viewport while retaining bounded rectangular damage");

    FormatTestDisplay<PixelFormat::rgb565, 20, 20> viewportDisplay{};
    Style viewportStyle{};
    viewportStyle.background = Color::rgb(0xff0000U);
    View viewportRoot{{0, 0, 20, 20}, viewportStyle};
    ViewController viewportController{viewportRoot};
    Application viewportApplication{viewportDisplay, round};
    std::array<Pixel, 21> viewportTilePixels{};
    const Surface viewportWorkspace{viewportTilePixels, 7, 3, 7};
    ComparingTileSink viewportSink{viewportDisplay.buffer(), {20, 20}};
    expect(viewportApplication.pushScene(viewportController) && viewportApplication.render() &&
               TiledRenderer::render(viewportApplication, {20, 20}, viewportWorkspace, viewportSink)
                   .ok() &&
               viewportSink.matches(),
           "application preserves its global circular viewport across tile-local surfaces");

    GestureRecognizer tapRecognizer{};
    const GestureRecognition tapPress =
        tapRecognizer.handlePointer({{10, 10}, PointerPhase::pressed, 1U, 100U});
    const GestureRecognition tapRelease =
        tapRecognizer.handlePointer({{18, 10}, PointerPhase::released, 1U, 220U});
    expect(tapPress.tracking && !tapPress.hasEvent && tapRelease.hasEvent &&
               tapRelease.event.type == GestureType::tap && tapRelease.event.durationMs == 120U,
           "tap recognition includes the configured slop edge and event timestamps");

    GestureRecognizer wrappingTimestampRecognizer{};
    wrappingTimestampRecognizer.handlePointer(
        {{3, 3}, PointerPhase::pressed, 2U, std::numeric_limits<std::uint32_t>::max() - 50U});
    const GestureRecognition wrappedTap =
        wrappingTimestampRecognizer.handlePointer({{3, 3}, PointerPhase::released, 2U, 25U});
    expect(wrappedTap.hasEvent && wrappedTap.event.type == GestureType::tap &&
               wrappedTap.event.durationMs == 76U,
           "gesture timestamps preserve elapsed time across one unsigned millisecond wrap");

    GestureRecognizer longPressRecognizer{};
    longPressRecognizer.handlePointer({{4, 4}, PointerPhase::pressed, 3U, 500U});
    const GestureRecognition beforeLongPress = longPressRecognizer.advanceTime(599U);
    const GestureRecognition longPress = longPressRecognizer.advanceTime(1U);
    expect(!beforeLongPress.hasEvent && longPress.hasEvent &&
               longPress.event.type == GestureType::longPress &&
               longPress.event.durationMs == 600U && longPressRecognizer.tracking(),
           "stationary long press uses the application-owned elapsed-time source");
    const GestureRecognition longRelease =
        longPressRecognizer.handlePointer({{4, 4}, PointerPhase::released, 3U, 1100U});
    expect(!longRelease.hasEvent && !longRelease.tracking,
           "release after a long press does not also dispatch a tap");

    GestureRecognizer dragRecognizer{};
    dragRecognizer.handlePointer({{0, 0}, PointerPhase::pressed, 4U, 10U});
    const GestureRecognition dragStart =
        dragRecognizer.handlePointer({{10, 0}, PointerPhase::moved, 4U, 20U});
    const GestureRecognition dragMove =
        dragRecognizer.handlePointer({{20, 0}, PointerPhase::moved, 4U, 30U});
    const GestureRecognition dragEnd =
        dragRecognizer.handlePointer({{30, 0}, PointerPhase::released, 4U, 900U});
    expect(dragStart.hasEvent && dragStart.event.type == GestureType::dragStarted &&
               dragMove.hasEvent && dragMove.event.type == GestureType::dragged &&
               dragEnd.hasEvent && dragEnd.event.type == GestureType::dragEnded,
           "drag recognition has deterministic threshold, update, and non-swipe completion events");

    const auto recognizeSwipe = [](Point destination) noexcept
    {
        GestureRecognizer recognizer{};
        recognizer.handlePointer({{0, 0}, PointerPhase::pressed, 5U, 100U});
        return recognizer.handlePointer({destination, PointerPhase::released, 5U, 200U});
    };
    expect(recognizeSwipe({-60, 5}).event.type == GestureType::swipeLeft &&
               recognizeSwipe({60, -5}).event.type == GestureType::swipeRight &&
               recognizeSwipe({5, -60}).event.type == GestureType::swipeUp &&
               recognizeSwipe({-5, 60}).event.type == GestureType::swipeDown,
           "dominant-axis recognition covers all four swipe directions");

    GestureRecognizer cancellationRecognizer{};
    cancellationRecognizer.handlePointer({{0, 0}, PointerPhase::pressed, 6U, 0U});
    const GestureRecognition secondPress =
        cancellationRecognizer.handlePointer({{100, 0}, PointerPhase::pressed, 7U, 1U});
    const GestureRecognition unrelated =
        cancellationRecognizer.handlePointer({{100, 0}, PointerPhase::moved, 7U, 1U});
    const GestureRecognition originalRelease =
        cancellationRecognizer.handlePointer({{0, 0}, PointerPhase::released, 6U, 2U});
    expect(!secondPress.hasEvent && secondPress.tracking && !unrelated.hasEvent &&
               unrelated.tracking && originalRelease.hasEvent &&
               originalRelease.event.type == GestureType::tap &&
               originalRelease.event.contactId == 6U,
           "gesture capture rejects unrelated presses and preserves the original contact");

    GestureRecognizer explicitCancellationRecognizer{};
    explicitCancellationRecognizer.handlePointer({{0, 0}, PointerPhase::pressed, 8U, 0U});
    const GestureRecognition cancelled = explicitCancellationRecognizer.cancel();
    expect(cancelled.hasEvent && cancelled.event.type == GestureType::cancelled &&
               !cancelled.tracking,
           "gesture recognizer exposes deterministic explicit cancellation");

    GestureRecognizer edgeRecognizer{};
    edgeRecognizer.handlePointer(
        {{std::numeric_limits<std::int32_t>::min(), 0}, PointerPhase::pressed, 9U, 0U});
    const GestureRecognition edgeSwipe = edgeRecognizer.handlePointer(
        {{std::numeric_limits<std::int32_t>::max(), 0}, PointerPhase::released, 9U, 1U});
    expect(edgeSwipe.hasEvent && edgeSwipe.event.type == GestureType::swipeRight,
           "gesture distance handling remains defined at extreme logical coordinates");
}

void testSceneOverlayAndTransitionContracts()
{
    using namespace lithe;
    using SceneDisplay = FormatTestDisplay<PixelFormat::rgb565, 40, 40>;

    SceneDisplay display{};
    Style firstStyle{};
    firstStyle.background = Color::rgb(0x8c2030U);
    Style secondStyle{};
    secondStyle.background = Color::rgb(0x204f9cU);
    Style overlayStyle{};
    overlayStyle.background = Color::transparent();
    Style panelStyle{};
    panelStyle.background = Color::rgb(0x24a36aU);

    View first{{0, 0, 40, 40}, firstStyle};
    ButtonView captured{{14, 14, 12, 12}, font5x7(), "A"};
    captured.setId(90U);
    View second{{0, 0, 40, 40}, secondStyle};
    View overlay{{0, 0, 40, 40}, overlayStyle};
    View panel{{10, 10, 20, 20}, panelStyle};
    expect(first.addSubview(captured) && overlay.addSubview(panel),
           "scene contract test builds caller-owned scene and overlay trees");

    CountingDelegate firstDelegate{};
    CountingDelegate secondDelegate{};
    CountingDelegate overlayDelegate{};
    ViewController firstController{first, firstDelegate};
    ViewController secondController{second, secondDelegate};
    ViewController overlayController{overlay, overlayDelegate};
    Application application{display, Viewport::circle({0, 0, 40, 40})};
    expect(application.pushScene(firstController) && application.render(),
           "fixed-capacity scene stack renders its initial circular scene");
    expect(display.pixels[0U] == Pixel{} && display.pixels[(20U * 40U) + 20U] != Pixel{},
           "application viewport keeps framebuffer corners outside the round panel untouched");
    expect(!application.handlePointer({{0, 0}, PointerPhase::pressed, 1U, 10U}),
           "round viewport rejects a new contact in a clipped corner");

    expect(application.handlePointer({{20, 20}, PointerPhase::pressed, 2U, 20U}) &&
               application.pushScene(secondController, {SceneTransition::slideLeft, 200U}) &&
               !application.handlePointer({{20, 20}, PointerPhase::released, 2U, 30U}) &&
               firstDelegate.actions == 0U,
           "navigation cancels captured view input before starting a transition");
    expect(application.advanceTime(100U) && application.render(),
           "scene transition advances from the caller-owned clock");
    expect(display.pixels[(20U * 40U) + 19U] == firstStyle.background.rgb565() &&
               display.pixels[(20U * 40U) + 20U] == secondStyle.background.rgb565(),
           "halfway slide renders outgoing and incoming scenes through translated clips");
    expect(!application.pushOverlay(overlayController) && !application.popScene() &&
               application.transitionRunning() && application.controllerDepth() == 2U &&
               application.activeController() == &secondController,
           "navigation fails closed without mutating an active transition");
    expect(application.cancelTransition() && application.controllerDepth() == 1U &&
               application.activeController() == &firstController && application.render(),
           "cancelled push transition rolls back to the outgoing scene deterministically");

    expect(application.pushScene(secondController, {SceneTransition::slideLeft, 200U}) &&
               application.advanceTime(200U) && !application.transitionRunning() &&
               application.controllerDepth() == 2U && application.render(),
           "completed scene transition commits the incoming scene without allocation");
    expect(application.popScene({SceneTransition::slideRight, 200U}) &&
               application.advanceTime(100U) && application.cancelTransition() &&
               !application.transitionRunning() && application.controllerDepth() == 2U &&
               application.activeController() == &secondController && application.render(),
           "cancelled pop transition restores the outgoing scene deterministically");

    expect(application.pushOverlay(overlayController, {SceneTransition::slideUp, 200U}) &&
               application.advanceTime(100U) && application.render() &&
               display.pixels[(35U * 40U) + 20U] == panelStyle.background.rgb565(),
           "slide-up overlay composites its translated intermediate frame");
    expect(application.advanceTime(100U) && application.render() &&
               display.pixels[(20U * 40U) + 20U] == panelStyle.background.rgb565() &&
               display.pixels[(35U * 40U) + 20U] == secondStyle.background.rgb565(),
           "completed transparent overlay clears its previous translated position");
    panelStyle.background = Color::rgb(0xf0c040U);
    panel.setStyle(panelStyle);
    expect(application.render() && display.lastRegionCount == 1U && display.lastRegion.x == 10 &&
               display.lastRegion.y == 10 && display.lastRegion.width == 20 &&
               display.lastRegion.height == 20,
           "small overlay-child invalidation remains damage-limited after the transition");
    expect(application.popOverlay({SceneTransition::dropAway, 200U}) &&
               application.advanceTime(200U) && application.render() &&
               application.controllerDepth() == 2U &&
               display.pixels[(20U * 40U) + 20U] == secondStyle.background.rgb565(),
           "drop-away overlay restores the scene and removes the overlay layer");
}

void testRoundDisplayDemo()
{
    using namespace lithe;
    RoundTestDisplay display{};
    demo::RoundDemo roundDemo{display};
    expect(roundDemo.initialize() && roundDemo.render(),
           "240x240 round-display demo initializes and renders through RGB565");
    expect(roundDemo.viewport().shape == ViewportShape::circle &&
               roundDemo.viewport().contains({120, 0}) && !roundDemo.viewport().contains({0, 0}),
           "round-display demo retains its circular application viewport");
    expect(display.pixels[0U] == Pixel{} &&
               display.pixels[demo::RoundDemo::kWidth - 1U] == Pixel{} &&
               display.pixels[(120U * 240U) + 120U] != Pixel{},
           "round-display dashboard leaves backing corners black");
    expectCrc32(crc32Pixels(display.pixels),
                kRoundDemoGoldens.dashboardRgb565,
                "round-display dashboard matches its golden");

    std::array<Pixel, std::size_t{73U} * 11U> tilePixels{};
    const Surface workspace{tilePixels, 73, 11, 73};
    ComparingTileSink initialSink{display.buffer(),
                                  {demo::RoundDemo::kWidth, demo::RoundDemo::kHeight}};
    const TileRenderResult initialTiles = roundDemo.renderTiled(initialSink, workspace);
    expect(initialTiles.ok() && initialSink.matches(),
           "round viewport produces identical full-frame and arbitrary-tile output");

    expect(roundDemo.handlePointer({{180, 120}, PointerPhase::pressed, 1U, 100U}) &&
               roundDemo.handlePointer({{100, 120}, PointerPhase::moved, 1U, 150U}) &&
               roundDemo.handlePointer({{50, 120}, PointerPhase::released, 1U, 200U}) &&
               roundDemo.transitionRunning() && roundDemo.sceneDepth() == 2U,
           "left swipe starts round-demo scene navigation through the shared recognizer");
    expect(roundDemo.advanceTime(120U) && roundDemo.render(),
           "round-demo transition renders at a deterministic halfway point");
    ComparingTileSink transitionSink{display.buffer(),
                                     {demo::RoundDemo::kWidth, demo::RoundDemo::kHeight}};
    const TileRenderResult transitionTiles = roundDemo.renderTiled(transitionSink, workspace);
    expect(transitionTiles.ok() && transitionSink.matches(),
           "translated round scenes remain byte-identical through arbitrary tiles");
    expect(roundDemo.advanceTime(120U) && !roundDemo.transitionRunning() && roundDemo.render(),
           "round-demo gesture lab commits after the bounded transition duration");
    expectCrc32(crc32Pixels(display.pixels),
                kRoundDemoGoldens.gestureLabRgb565,
                "round-demo gesture lab matches its golden");

    const std::uint32_t idleGestureLab = crc32Pixels(display.pixels);
    expect(roundDemo.handlePointer({{82, 86}, PointerPhase::pressed, 2U, 300U}) &&
               roundDemo.handlePointer({{82, 86}, PointerPhase::released, 2U, 350U}) &&
               roundDemo.render() && crc32Pixels(display.pixels) != idleGestureLab,
           "round-demo gesture lab visibly reports a recognized tap");
    const std::uint32_t tappedGestureLab = crc32Pixels(display.pixels);
    expect(roundDemo.handlePointer({{82, 86}, PointerPhase::pressed, 3U, 400U}) &&
               roundDemo.handlePointer({{98, 94}, PointerPhase::moved, 3U, 440U}) &&
               roundDemo.handlePointer({{112, 104}, PointerPhase::moved, 3U, 470U}) &&
               roundDemo.render() && crc32Pixels(display.pixels) != tappedGestureLab,
           "round-demo gesture lab visibly reports bounded drag movement");
    const std::uint32_t draggedGestureLab = crc32Pixels(display.pixels);
    expect(roundDemo.handlePointer({{112, 104}, PointerPhase::released, 3U, 500U}) &&
               roundDemo.render() && crc32Pixels(display.pixels) != draggedGestureLab,
           "round-demo gesture lab visibly reports drag completion");
    ComparingTileSink gestureSink{display.buffer(),
                                  {demo::RoundDemo::kWidth, demo::RoundDemo::kHeight}};
    const TileRenderResult gestureTiles = roundDemo.renderTiled(gestureSink, workspace);
    expect(gestureTiles.ok() && gestureSink.matches(),
           "gesture feedback remains byte-identical through arbitrary tiles");
}

void testRepeatedPointerRendering()
{
    using namespace lithe;
    TestDisplay display{};
    View root{{0, 0, 100, 60}};
    ButtonView button{{5, 5, 40, 24}, font5x7(), "OPEN"};
    button.setId(77U);
    expect(root.addSubview(button), "repeated touch test constructs a retained tree");
    CountingDelegate delegate{};
    ViewController controller{root, delegate};
    Application application{display};
    expect(application.push(controller) && application.render(),
           "repeated touch test renders its initial frame");
    const std::size_t initialPresentCount = display.presentCount;

    for (std::uint8_t contact = 0U; contact < 2U; ++contact)
    {
        expect(application.handlePointer({{10, 10}, PointerPhase::pressed, contact}) &&
                   application.render(),
               "a handled press with no visual damage remains a successful frame");
        expect(application.handlePointer({{10, 10}, PointerPhase::moved, contact}) &&
                   application.render(),
               "a handled move with no visual damage remains a successful frame");
        expect(application.handlePointer({{10, 10}, PointerPhase::released, contact}) &&
                   application.render(),
               "a handled release with no visual damage remains a successful frame");
    }

    expect(delegate.actions == 2U && display.presentCount == initialPresentCount,
           "two complete touch cycles activate twice without presenting clean frames");
}

void testXmlWriterRoundTrip()
{
    using namespace lithe;
    constexpr std::string_view kXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,100,60\">"
        "<object className=\"LCDLABELVIEW\" frame=\"0,0,100,20\" runtimeID=\"7\" "
        "text=\"XML &amp; MVC\"/>"
        "</object>";
    std::array<ViewSpec, 4> source{};
    const XmlLayoutResult parsed = XmlLayoutParser::parse(kXml, source, XmlParseOptions{true});
    std::array<char, 2048> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{source}.first(parsed.nodeCount), output);
    expect(written.ok() && written.bytesWritten > 0U,
           "caller-buffer XML writer serializes a layout");
    std::array<ViewSpec, 4> reparsed{};
    const XmlLayoutResult roundTrip = XmlLayoutParser::parse(
        std::string_view{output.data(), written.bytesWritten}, reparsed, XmlParseOptions{true});
    expect(roundTrip.ok() && reparsed[1].text == "XML &amp; MVC",
           "XML writer round-trips encoded attribute text");
    std::array<char, 16> tooSmall{};
    const XmlWriteResult truncated =
        XmlLayoutWriter::write(std::span<const ViewSpec>{source}.first(parsed.nodeCount), tooSmall);
    expect(!truncated && truncated.error == XmlWriteError::bufferTooSmall &&
               tooSmall.back() == '\0',
           "XML writer fails closed on a small caller buffer");

    std::array<ViewSpec, 1> extreme{{ViewSpec{}}};
    extreme[0].frame = {std::numeric_limits<std::int32_t>::min(), 0, 1, 1};
    const XmlWriteResult extremeWritten = XmlLayoutWriter::write(extreme, output);
    std::array<ViewSpec, 1> extremeRoundTrip{};
    const XmlLayoutResult extremeParsed = XmlLayoutParser::parse(
        std::string_view{output.data(), extremeWritten.bytesWritten}, extremeRoundTrip);
    expect(extremeWritten.ok() && extremeParsed.ok() &&
               extremeRoundTrip[0].frame.x == std::numeric_limits<std::int32_t>::min(),
           "XML writer round-trips INT32_MIN without unsigned magnitude conversion");

    expect(Painter::measureText("A &amp; B", font5x7()).width ==
               Painter::measureText("A & B", font5x7()).width,
           "text measurement decodes XML entities without an allocation arena");
}

void testXmlAnimationSequence()
{
    using namespace lithe;
    constexpr std::string_view kXml =
        "<object className=\"LCDVIEW\" frame=\"0,0,40,40\">"
        "<object className=\"LCDIMAGEVIEW\" frame=\"4,4,32,32\" runtimeID=\"9\" "
        "animationInterval=\"200\" repeatIndex=\"1\" "
        "animationImage0=\"strip_f1\" animationImage1=\"strip_f2\" "
        "animationImage2=\"strip_f3\"/>"
        "</object>";
    std::array<ViewSpec, 4> specs{};
    std::array<std::string_view, 4> frames{};
    const XmlLayoutResult parsed =
        XmlLayoutParser::parse(kXml, specs, XmlParseOptions{true, frames});
    expect(parsed.ok() && parsed.animationFrameCount == 3U &&
               specs[1].kind == ViewKind::animatedImage,
           "original animationImageN XML parses into bounded caller storage");
    expect(frames[0] == "strip_f1" && frames[2] == "strip_f3",
           "animation frame order remains source-defined");

    AnimationResources resources{};
    StaticLayout<4> layout{};
    expect(layout
               .build(std::span<const ViewSpec>{specs}.first(parsed.nodeCount),
                      font5x7(),
                      &resources,
                      std::span<const std::string_view>{frames}.first(parsed.animationFrameCount))
               .ok(),
           "parsed animation resolves frame resources without allocation");
    auto* animation = layout.find<AnimatedImageView>(9U);
    expect(animation != nullptr && animation->frameIndex() == 0U &&
               layout.root()->animationsRunning(),
           "animation starts on its first frame and advertises its clock requirement");
    expect(layout.root()->advanceTime(200U) && animation->frameIndex() == 1U,
           "caller-supplied elapsed time advances one frame");
    expect(!layout.root()->advanceTime(400U) && animation->frameIndex() == 1U,
           "repeatIndex preserves intro-then-loop behavior without a redundant redraw");

    std::array<char, 2048> output{};
    const XmlWriteResult written =
        XmlLayoutWriter::write(std::span<const ViewSpec>{specs}.first(parsed.nodeCount),
                               output,
                               XmlWriteOptions{std::span<const std::string_view>{frames}.first(
                                   parsed.animationFrameCount)});
    std::array<ViewSpec, 4> roundTripSpecs{};
    std::array<std::string_view, 4> roundTripFrames{};
    const XmlLayoutResult roundTrip =
        XmlLayoutParser::parse(std::string_view{output.data(), written.bytesWritten},
                               roundTripSpecs,
                               XmlParseOptions{true, roundTripFrames});
    expect(written.ok() && roundTrip.ok() && roundTripFrames[2] == "strip_f3" &&
               roundTripSpecs[1].animationRepeatIndex == 1U,
           "XML writer round-trips legacy animation sequences");

    const XmlLayoutResult noStorage = XmlLayoutParser::parse(kXml, specs, XmlParseOptions{true});
    expect(!noStorage && noStorage.error == XmlLayoutError::animationCapacity,
           "animation XML fails closed when no frame-name arena is supplied");
}

void testSharedDemo()
{
    TestDisplay display{};
    lithe::demo::Demo demo{display};
    expect(demo.initialize(), "shared XML demo initializes");
    expect(demo.render(), "initial retained view tree renders");
    expect(display.presentCount == 1U && display.lastRegionCount > 0U,
           "display receives dirty regions");
    expect(crc32Pixels(display.pixels) == kDemoGoldens.initialRgb565,
           "full-frame RGB565 output matches the initial-menu golden signature");
    std::array<lithe::Pixel, std::size_t{73U} * 11U> tilePixels{};
    ComparingTileSink tileSink{display.buffer(),
                               {lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight}};
    const lithe::TileRenderResult tiled = demo.renderTiled(tileSink, {tilePixels, 73, 11, 73});
    expect(tiled.ok() && tileSink.matches(),
           "arbitrary small tiles reproduce the full RGB565 framebuffer exactly");
    expect(tiled.pixelsTransferred == display.pixels.size() && tiled.tileCount == tileSink.tiles(),
           "tiled rendering reports a complete bounded transfer");
    const std::size_t firstFrameTileCount = tiled.tileCount;
    expect(tileSink.beginFrame({lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight},
                               lithe::PixelFormat::rgb565) &&
               !tileSink.matches() && tileSink.tiles() == 0U && !tileSink.endFrame(false),
           "beginning a tile frame resets completion and per-frame tile state");
    const lithe::Pixel expectedFirstPixel = display.pixels.front();
    display.pixels.front() =
        expectedFirstPixel == lithe::Pixel{} ? lithe::Pixel{1U} : lithe::Pixel{};
    const lithe::TileRenderResult mismatched = demo.renderTiled(tileSink, {tilePixels, 73, 11, 73});
    expect(mismatched.error == lithe::TileRenderError::sinkEndFailed && !tileSink.matches(),
           "a mismatched tile frame fails at the sink boundary");
    display.pixels.front() = expectedFirstPixel;
    const lithe::TileRenderResult recovered = demo.renderTiled(tileSink, {tilePixels, 73, 11, 73});
    expect(recovered.ok() && tileSink.matches() && recovered.tileCount == firstFrameTileCount &&
               tileSink.tiles() == firstFrameTileCount,
           "a new tile frame resets a previous mismatch and reports only its own tiles");
    const auto samplePixel =
        (std::size_t{10U} * lithe::detail::nonnegativeSize(lithe::demo::Demo::kWidth)) + 10U;
    const lithe::Pixel before = display.pixels[samplePixel];
    expect(demo.handleKey(lithe::Key::select), "first menu item opens the widget gallery");
    expect(demo.render() && demo.animationsRunning(),
           "widget gallery visibly requests the application animation clock");
    const std::uint32_t animationBefore = crc32Pixels(display.pixels);
    const bool animationAdvanced =
        demo.advanceTime(lithe::demo::kGallopIntervalMs) && demo.render();
    // cppcheck-suppress knownConditionTrueFalse -- render mutates the display through a virtual
    // API.
    const bool animationChanged = crc32Pixels(display.pixels) != animationBefore;
    expect(animationAdvanced && animationChanged,
           "large gallery animation changes rendered RGB565 pixels on schedule");
    expect(display.lastRegionCount == 1U && display.lastRegion.x == 50 &&
               display.lastRegion.y == 64 && display.lastRegion.width == 120 &&
               display.lastRegion.height == 72,
           "transparent animation redraw is clipped to its invalidated frame");
    expect(demo.handleKey(lithe::Key::back) && demo.render(),
           "back returns from the widget gallery");
    expect(demo.handleKey(lithe::Key::down), "focus navigation handles down");
    expect(demo.render(), "focus change renders");
    expect(demo.handleKey(lithe::Key::select), "selected menu item opens controller");
    expect(demo.render(), "pushed controller renders");
    expect(demo.handleKey(lithe::Key::back), "back pops controller");
    expect(demo.render(), "popped controller renders");
    expect(display.pixels[(10U * lithe::demo::Demo::kWidth) + 10U] == before,
           "returning to menu restores header pixel");
}

void testCrossFormatDemo()
{
    TestDisplay rgb565Display{};
    lithe::demo::Demo rgb565Demo{rgb565Display};
    XrgbTestDisplay xrgbDisplay{};
    lithe::demo::Demo xrgbDemo{xrgbDisplay};
    expect(rgb565Demo.initialize() && rgb565Demo.render() && xrgbDemo.initialize() &&
               xrgbDemo.render(),
           "the common demo renders through RGB565 and XRGB8888 displays");

    const lithe::PixelBufferView xrgbBuffer = xrgbDisplay.buffer();
    const std::uint32_t xrgbCrc = crc32Buffer(xrgbBuffer);
    expect(xrgbCrc == kDemoGoldens.initialXrgb8888,
           "full-frame XRGB8888 output matches the initial-menu golden signature");
    if constexpr (kUsesCompactDemoFont)
    {
        expect(rgb565Equivalent(rgb565Display.pixels, xrgbBuffer),
               "compact-font XRGB8888 and RGB565 demos are pixel equivalent");
    }

    std::array<lithe::Xrgb8888Pixel, std::size_t{73U} * 11U> tilePixels{};
    ComparingTileSink tileSink{xrgbBuffer, {lithe::demo::Demo::kWidth, lithe::demo::Demo::kHeight}};
    const lithe::Surface workspace{lithe::Xrgb8888Surface{tilePixels, 73, 11, 73}};
    const lithe::TileRenderResult tiled = xrgbDemo.renderTiled(tileSink, workspace);
    expect(tiled.ok() && tileSink.matches(),
           "arbitrary XRGB8888 tiles reproduce the full XRGB8888 framebuffer exactly");

    expect(rgb565Demo.handleKey(lithe::Key::down) && xrgbDemo.handleKey(lithe::Key::down) &&
               rgb565Demo.render() && xrgbDemo.render(),
           "both display formats render the focused reference scene");
    const std::uint32_t focusedXrgbCrc = crc32Buffer(xrgbDisplay.buffer());
    expect(focusedXrgbCrc == kDemoGoldens.focusedXrgb8888 &&
               crc32Pixels(rgb565Display.pixels) == kDemoGoldens.focusedRgb565,
           "focused RGB565 and XRGB8888 scenes match their configured-font goldens");
    if constexpr (kUsesCompactDemoFont)
    {
        expect(rgb565Equivalent(rgb565Display.pixels, xrgbDisplay.buffer()),
               "focused compact-font XRGB8888 and RGB565 demos are pixel equivalent");
    }
}

void testU545Probe()
{
    lithe::stm32u545::GraphicsProbe probe{};
    const lithe::stm32u545::ProbeResult first = probe.renderFrame();
    expect(first.ok() && first.frameCrc32 == kDemoGoldens.initialRgb565,
           "U545 tiled rendering matches the initial-menu golden signature");
    expect(first.tileCount == 68U &&
               first.pixelsRendered ==
                   lithe::detail::nonnegativeSize(lithe::demo::Demo::kWidth) *
                       lithe::detail::nonnegativeSize(lithe::demo::Demo::kHeight),
           "U545 probe uses 68 four-row strips without a full framebuffer");
    expect(probe.handleKey(lithe::Key::down), "U545 probe accepts portable key input");
    const lithe::stm32u545::ProbeResult second = probe.renderFrame();
    expect(second.ok() && second.frameCrc32 == kDemoGoldens.focusedRgb565,
           "U545 tiled rendering matches the focused-menu golden signature");
}

void testU545RoundProbe()
{
    lithe::stm32u545::RoundGraphicsProbe probe{};
    const lithe::stm32u545::ProbeResult first = probe.renderFrame();
    expect(first.ok() && first.tileCount == 60U &&
               first.pixelsRendered ==
                   lithe::detail::nonnegativeSize(lithe::demo::RoundDemo::kWidth) *
                       lithe::detail::nonnegativeSize(lithe::demo::RoundDemo::kHeight),
           "U545 round-panel probe renders 240x240 through 60 four-row strips");
    expectCrc32(first.frameCrc32,
                kRoundDemoGoldens.dashboardRgb565,
                "U545 round-panel dashboard matches its golden");
    expect(probe.handlePointer({{180, 120}, lithe::PointerPhase::pressed, 1U, 100U}) &&
               probe.handlePointer({{50, 120}, lithe::PointerPhase::released, 1U, 200U}) &&
               probe.advanceTime(240U),
           "U545 round-panel probe builds the same swipe and transition state machines");
    const lithe::stm32u545::ProbeResult gestureLab = probe.renderFrame();
    expect(gestureLab.ok(), "U545 round-panel probe commits the gesture lab without a framebuffer");
    expectCrc32(gestureLab.frameCrc32,
                kRoundDemoGoldens.gestureLabRgb565,
                "U545 round-panel gesture lab matches its golden");
}

void testGeneratedWidgetMetadata()
{
    expect(lithe::tests::generatedWidgetMetadataValid(),
           "generated C++ widget metadata is complete, bounded, and catalog-locked");
}

void testGeneratedForm()
{
    expect(lithe::tests::generatedFormValid(),
           "designer-generated form matches canonical XML semantics");
    expect(lithe::tests::generatedButtonFormValid(),
           "designer-generated button variants match canonical XML semantics and pixels");
    expect(lithe::tests::generatedTabFormValid(),
           "designer-generated tabs match canonical XML semantics, pixels, and page visibility");
    expect(lithe::tests::generatedWheelFormValid(),
           "designer-generated wheel pickers match canonical XML semantics, pixels, and selection");
}

void testHostFootprintBudgets()
{
    expect(sizeof(lithe::View) <= 128U,
           "host View footprint remains within its regression ceiling");
    expect(sizeof(lithe::ViewSpec) <= 192U,
           "host ViewSpec footprint remains within its regression ceiling");
    expect(sizeof(lithe::PickerView) <= 192U,
           "host PickerView footprint remains within its regression ceiling");
    expect(sizeof(lithe::StaticLayout<16>) <= 3200U,
           "host StaticLayout<16> footprint remains within its regression ceiling");
}

void testRenderTimeBudget()
{
    constexpr std::size_t kFrameCount{8U};
    constexpr std::chrono::milliseconds kTotalBudget{4000};
    lithe::stm32u545::GraphicsProbe probe{};
    const auto started = std::chrono::steady_clock::now();
    bool rendered = true;
    for (std::size_t frame = 0U; frame < kFrameCount; ++frame)
    {
        rendered = rendered && probe.renderFrame().ok();
    }
    const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - started);
    expect(rendered && elapsed <= kTotalBudget,
           "eight complete tiled reference frames remain within the host timing ceiling");
    std::cout << "Render timing: " << kFrameCount << " tiled frames in " << elapsed.count()
              << " ms (budget " << kTotalBudget.count() << " ms)\n";
}

} // namespace

int main()
{
    testBuildInfo();
    testFontBackendsAndManifests();
    testTextLayoutAndUtf8Policy();
    testGeneratedFontFormatsAndTiles();
    testGeometryAndColor();
    testPixelFormatContracts();
    testGallopAnimationResources();
    testDisplayTransferContract();
    testXmlLayout();
    testWidgetSetLayouts();
    testXmlMutationCorpus();
    testPainterDamage();
    testAdvancedWidgets();
    testSelectionAndTextWidgets();
    testWidgetStateResolution();
    testButtonVariantXml();
    testButtonVariantBehavior();
    testButtonVariantGoldens();
    testTabXml();
    testTabBehavior();
    testTabGoldens();
    testWheelPickerXml();
    testWheelPickerBehavior();
    testWheelPickerGoldens();
    testAutomaticScrollReveal();
    testPointerLifecycle();
    testViewportAndGestureContracts();
    testSceneOverlayAndTransitionContracts();
    testRepeatedPointerRendering();
    testXmlWriterRoundTrip();
    testXmlAnimationSequence();
    testSharedDemo();
    testCrossFormatDemo();
    testRoundDisplayDemo();
    testU545Probe();
    testU545RoundProbe();
    testGeneratedWidgetMetadata();
    testGeneratedForm();
    testHostFootprintBudgets();
    testRenderTimeBudget();
    std::cout << "Footprint: View=" << sizeof(lithe::View)
              << " ViewSpec=" << sizeof(lithe::ViewSpec)
              << " PickerView=" << sizeof(lithe::PickerView)
              << " StaticLayout<16>=" << sizeof(lithe::StaticLayout<16>) << " bytes\n";
    // cppcheck-suppress knownConditionTrueFalse
    // cppcheck does not track increments through the reference returned by failureCount().
    if (failureCount() == 0)
    {
        std::cout << "LitheView tests passed\n";
    }
    // cppcheck-suppress knownConditionTrueFalse
    // cppcheck does not track increments through the reference returned by failureCount().
    return failureCount() == 0 ? 0 : 1;
}

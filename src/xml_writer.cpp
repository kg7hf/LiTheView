// SPDX-FileCopyrightText: 2012-2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

#include "lithe/xml_writer.hpp"

#include <array>
#include <concepts>
#include <string_view>

namespace lithe
{

namespace
{

constexpr std::size_t kMaxDepth = 32U;

template <std::integral Integer> char decimalDigit(Integer value) noexcept
{
    constexpr std::string_view kDigits{"0123456789"};
    for (const char digit : kDigits)
    {
        if (value == 0U)
        {
            return digit;
        }
        --value;
    }
    return '0';
}

class BufferWriter
{
public:
    explicit BufferWriter(std::span<char> destination) noexcept : mDestination(destination)
    {
        if (!mDestination.empty())
        {
            mDestination[0] = '\0';
        }
    }

    bool good() const noexcept
    {
        return mGood;
    }
    std::size_t size() const noexcept
    {
        return mSize;
    }

    void append(char value) noexcept
    {
        if (!reserve(1U))
        {
            return;
        }
        mDestination[mSize++] = value;
        mDestination[mSize] = '\0';
    }

    void append(std::string_view value) noexcept
    {
        if (!reserve(value.size()))
        {
            return;
        }
        for (const char character : value)
        {
            mDestination[mSize++] = character;
        }
        mDestination[mSize] = '\0';
    }

    void appendUnsigned(std::uint64_t value) noexcept
    {
        std::array<char, 20> digits{};
        std::size_t count = 0U;
        while (true)
        {
            digits[count++] = decimalDigit(value % 10U);
            value /= 10U;
            if (value == 0U)
            {
                break;
            }
        }
        while (count > 0U)
        {
            append(digits[--count]);
        }
    }

    void appendSigned(std::int32_t value) noexcept
    {
        const bool negative = value < 0;
        std::array<char, 11> digits{};
        std::size_t count = 0U;
        while (true)
        {
            std::int32_t remainder = value % 10;
            if (remainder < 0)
            {
                remainder = -remainder;
            }
            digits[count++] = decimalDigit(remainder);
            value /= 10;
            if (value == 0)
            {
                break;
            }
        }

        if (negative)
        {
            append('-');
        }
        while (count > 0U)
        {
            append(digits[--count]);
        }
    }

    void appendHex(std::uint32_t value) noexcept
    {
        static constexpr std::string_view kDigits{"0123456789ABCDEF"};
        append("0x");
        for (std::uint32_t shift = 28U;; shift -= 4U)
        {
            append(kDigits[(value >> shift) & 0x0fU]);
            if (shift == 0U)
            {
                break;
            }
        }
    }

    void appendAttributeText(std::string_view value, bool alreadyEncoded) noexcept
    {
        if (alreadyEncoded)
        {
            append(value);
            return;
        }
        for (const char character : value)
        {
            switch (character)
            {
            case '&':
                append("&amp;");
                break;
            case '<':
                append("&lt;");
                break;
            case '>':
                append("&gt;");
                break;
            case '"':
                append("&quot;");
                break;
            case '\'':
                append("&apos;");
                break;
            default:
                append(character);
                break;
            }
        }
    }

private:
    bool reserve(std::size_t amount) noexcept
    {
        if (!mGood || mDestination.empty() ||
            amount >=
                mDestination.size() - (mSize < mDestination.size() ? mSize : mDestination.size()))
        {
            mGood = false;
            return false;
        }
        return true;
    }

    std::span<char> mDestination;
    std::size_t mSize{0};
    bool mGood{true};
};

std::string_view className(ViewKind kind) noexcept
{
    switch (kind)
    {
    case ViewKind::panel:
        return "LCDVIEW";
    case ViewKind::label:
        return "LCDLABELVIEW";
    case ViewKind::button:
        return "LCDBUTTONVIEW";
    case ViewKind::progress:
        return "LCDPROGRESSVIEW";
    case ViewKind::image:
    case ViewKind::animatedImage:
        return "LCDIMAGEVIEW";
    case ViewKind::icon:
        return "LCDICONVIEW";
    case ViewKind::line:
        return "LCDLINEVIEW";
    case ViewKind::scroll:
        return "LCDSCROLLVIEW";
    case ViewKind::table:
        return "LCDTABLEVIEW";
    case ViewKind::list:
        return "LCDLISTVIEW";
    case ViewKind::plot:
        return "LCDPLOTVIEW";
    case ViewKind::waterfall:
        return "LCDWATERFALLVIEW";
    case ViewKind::axis:
        return "LCDAXISVIEW";
    case ViewKind::markerRange:
        return "LCDMARKERRANGEVIEW";
    case ViewKind::meter:
        return "LCDMETERVIEW";
    case ViewKind::picker:
        return "LCDPICKERVIEW";
    case ViewKind::checkbox:
        return "LCDCHECKBOXVIEW";
    case ViewKind::radio:
        return "LCDRADIOBUTTONVIEW";
    case ViewKind::textField:
        return "LCDTEXTFIELDVIEW";
    case ViewKind::softkey:
        return "LCDSOFTKEYVIEW";
    case ViewKind::tabBar:
        return "LCDTABBARVIEW";
    case ViewKind::tabView:
        return "LCDTABVIEW";
    case ViewKind::dropDown:
        return "LCDDROPDOWNVIEW";
    case ViewKind::wheelPicker:
        return "LCDWHEELPICKERVIEW";
    }
    return {};
}

std::uint32_t alignmentFlags(TextAlign alignment) noexcept
{
    std::uint32_t horizontal = 0U;
    if (alignment.horizontal == HorizontalAlign::right)
    {
        horizontal = 1U;
    }
    else if (alignment.horizontal == HorizontalAlign::center)
    {
        horizontal = 2U;
    }

    std::uint32_t vertical = 0U;
    if (alignment.vertical == VerticalAlign::bottom)
    {
        vertical = 1U;
    }
    else if (alignment.vertical == VerticalAlign::center)
    {
        vertical = 3U;
    }
    return horizontal | (vertical << 2U);
}

void indent(BufferWriter& writer, std::size_t depth) noexcept
{
    for (std::size_t index = 0; index < depth; ++index)
    {
        writer.append("  ");
    }
}

void stringAttribute(BufferWriter& writer,
                     std::string_view name,
                     std::string_view value,
                     bool encoded) noexcept
{
    if (value.empty())
    {
        return;
    }
    writer.append(' ');
    writer.append(name);
    writer.append("=\"");
    writer.appendAttributeText(value, encoded);
    writer.append('"');
}

void unsignedAttribute(BufferWriter& writer, std::string_view name, std::uint64_t value) noexcept
{
    writer.append(' ');
    writer.append(name);
    writer.append("=\"");
    writer.appendUnsigned(value);
    writer.append('"');
}

void signedAttribute(BufferWriter& writer, std::string_view name, std::int32_t value) noexcept
{
    writer.append(' ');
    writer.append(name);
    writer.append("=\"");
    writer.appendSigned(value);
    writer.append('"');
}

void colorAttribute(BufferWriter& writer, std::string_view name, Color value) noexcept
{
    if (!isSpecifiedStyleStateColor(value))
    {
        return;
    }
    writer.append(' ');
    writer.append(name);
    writer.append("=\"");
    writer.appendHex(value.legacyValue());
    writer.append('"');
}

class LayoutEmitter
{
public:
    LayoutEmitter(std::span<const ViewSpec> specs,
                  BufferWriter& writer,
                  XmlWriteOptions options) noexcept
        : mSpecs(specs), mWriter(writer), mOptions(options)
    {
    }

    XmlWriteError emit(std::size_t root) noexcept
    {
        return emitNode(root, 0U);
    }

private:
    XmlWriteError emitNode(std::size_t index, std::size_t depth) noexcept
    {
        if (depth >= kMaxDepth)
        {
            return XmlWriteError::nestingTooDeep;
        }
        const ViewSpec& spec = mSpecs[index];
        const std::string_view type = className(spec.kind);
        if (type.empty())
        {
            return XmlWriteError::invalidHierarchy;
        }

        indent(mWriter, depth);
        mWriter.append("<object className=\"");
        mWriter.append(type);
        mWriter.append("\" frame=\"");
        mWriter.appendSigned(spec.frame.x);
        mWriter.append(',');
        mWriter.appendSigned(spec.frame.y);
        mWriter.append(',');
        mWriter.appendSigned(spec.frame.width);
        mWriter.append(',');
        mWriter.appendSigned(spec.frame.height);
        mWriter.append('"');
        if (spec.id != 0U)
        {
            unsignedAttribute(mWriter, "runtimeID", spec.id);
        }

        mWriter.append(" backgroundColor=\"");
        mWriter.appendHex(spec.style.background.legacyValue());
        mWriter.append("\" textColor=\"");
        mWriter.appendHex(spec.style.foreground.legacyValue());
        mWriter.append("\" accentColor=\"");
        mWriter.appendHex(spec.style.accent.legacyValue());
        mWriter.append("\" borderColor=\"");
        mWriter.appendHex(spec.style.border.legacyValue());
        mWriter.append('"');
        if (spec.style.states != nullptr)
        {
            colorAttribute(mWriter, "focusedBackgroundColor", spec.style.states->focusedBackground);
            colorAttribute(mWriter, "focusedForegroundColor", spec.style.states->focusedForeground);
            colorAttribute(mWriter, "pressedBackgroundColor", spec.style.states->pressedBackground);
            colorAttribute(mWriter, "pressedForegroundColor", spec.style.states->pressedForeground);
            colorAttribute(
                mWriter, "disabledBackgroundColor", spec.style.states->disabledBackground);
            colorAttribute(
                mWriter, "disabledForegroundColor", spec.style.states->disabledForeground);
            colorAttribute(
                mWriter, "selectedBackgroundColor", spec.style.states->selectedBackground);
            colorAttribute(
                mWriter, "selectedForegroundColor", spec.style.states->selectedForeground);
        }
        unsignedAttribute(mWriter, "textAlignmentFlags", alignmentFlags(spec.style.textAlign));
        unsignedAttribute(mWriter, "textScale", spec.style.textScale);
        unsignedAttribute(mWriter, "borderWidth", spec.style.borderWidth);

        switch (spec.kind)
        {
        case ViewKind::label:
            stringAttribute(mWriter, "text", spec.text, spec.xmlEncodedText);
            stringAttribute(mWriter, "detail", spec.detail, spec.xmlEncodedText);
            break;
        case ViewKind::button:
            stringAttribute(mWriter, "text", spec.text, spec.xmlEncodedText);
            stringAttribute(mWriter, "detail", spec.detail, spec.xmlEncodedText);
            stringAttribute(mWriter, "icon", spec.resource, spec.xmlEncodedText);
            if (spec.buttonBehavior == ButtonBehavior::toggle)
            {
                mWriter.append(" behavior=\"toggle\"");
                mWriter.append(spec.checked ? " checked=\"1\"" : " checked=\"0\"");
            }
            break;
        case ViewKind::checkbox:
            stringAttribute(mWriter, "text", spec.text, spec.xmlEncodedText);
            mWriter.append(spec.checked ? " checked=\"1\"" : " checked=\"0\"");
            break;
        case ViewKind::radio:
            stringAttribute(mWriter, "text", spec.text, spec.xmlEncodedText);
            mWriter.append(spec.checked ? " selected=\"1\"" : " selected=\"0\"");
            break;
        case ViewKind::textField:
            stringAttribute(mWriter, "binding", spec.resource, spec.xmlEncodedText);
            stringAttribute(mWriter, "placeholder", spec.detail, spec.xmlEncodedText);
            if (spec.maxLength != 0U)
            {
                unsignedAttribute(mWriter, "maxLength", spec.maxLength);
            }
            break;
        case ViewKind::picker:
        case ViewKind::tabBar:
        case ViewKind::tabView:
        case ViewKind::dropDown:
        case ViewKind::list:
        case ViewKind::wheelPicker:
            stringAttribute(mWriter, "items", spec.text, spec.xmlEncodedText);
            unsignedAttribute(mWriter, "selectedIndex", spec.selectedIndex);
            if (spec.kind != ViewKind::list)
            {
                mWriter.append(spec.wraps ? " wrap=\"1\"" : " wrap=\"0\"");
            }
            if (spec.kind == ViewKind::tabView)
            {
                unsignedAttribute(mWriter, "tabHeight", spec.tabHeight);
            }
            if (spec.kind == ViewKind::wheelPicker || spec.kind == ViewKind::dropDown || spec.kind == ViewKind::list)
            {
                unsignedAttribute(mWriter, "rowHeight", spec.rowHeight);
            }
            break;
        case ViewKind::softkey:
            stringAttribute(mWriter, "left_text", spec.text, spec.xmlEncodedText);
            stringAttribute(mWriter, "right_text", spec.detail, spec.xmlEncodedText);
            if (spec.leftAction != 0U)
            {
                unsignedAttribute(mWriter, "leftAction", spec.leftAction);
            }
            if (spec.rightAction != 0U)
            {
                unsignedAttribute(mWriter, "rightAction", spec.rightAction);
            }
            break;
        case ViewKind::progress:
            signedAttribute(mWriter, "value", spec.value);
            break;
        case ViewKind::animatedImage:
        {
            const std::size_t first = spec.animationFrameOffset;
            const std::size_t count = spec.animationFrameCount;
            if (count == 0U || first > mOptions.animationFrames.size() ||
                count > mOptions.animationFrames.size() - first)
            {
                return XmlWriteError::missingAnimationFrames;
            }
            unsignedAttribute(mWriter, "animationInterval", spec.animationIntervalMs);
            if (spec.animationRepeatIndex != 0U)
            {
                unsignedAttribute(mWriter, "repeatIndex", spec.animationRepeatIndex);
            }
            if (!spec.animationAutoplay)
            {
                mWriter.append(" autoplay=\"0\"");
            }
            for (std::size_t frame = 0U; frame < count; ++frame)
            {
                mWriter.append(" animationImage");
                mWriter.appendUnsigned(frame);
                mWriter.append("=\"");
                mWriter.appendAttributeText(mOptions.animationFrames[first + frame],
                                            spec.xmlEncodedText);
                mWriter.append('"');
            }
            break;
        }
        case ViewKind::scroll:
        case ViewKind::table:
            if (spec.contentHeight > 0)
            {
                mWriter.append(" contentHeight=\"");
                mWriter.appendSigned(spec.contentHeight);
                mWriter.append('"');
            }
            if (spec.scrollOffset > 0)
            {
                mWriter.append(" scrollOffset=\"");
                mWriter.appendSigned(spec.scrollOffset);
                mWriter.append('"');
            }
            mWriter.append(" scrollStep=\"");
            mWriter.appendSigned(spec.scrollStep);
            mWriter.append('"');
            mWriter.append(spec.showsIndicator ? " showsIndicator=\"1\"" : " showsIndicator=\"0\"");
            break;
        default:
            break;
        }
        if (spec.kind == ViewKind::image || spec.kind == ViewKind::icon)
        {
            stringAttribute(mWriter, "imageName", spec.resource, spec.xmlEncodedText);
        }
        stringAttribute(mWriter, "font", spec.font, spec.xmlEncodedText);
        if (!spec.visible)
        {
            mWriter.append(" hide=\"1\"");
        }
        if (!spec.enabled)
        {
            mWriter.append(" enabled=\"0\"");
        }
        if (spec.focusable >= 0)
        {
            mWriter.append(spec.focusable == 0 ? " canFocus=\"0\"" : " canFocus=\"1\"");
        }

        bool hasChildren = false;
        for (std::size_t child = index + 1U; child < mSpecs.size(); ++child)
        {
            if (mSpecs[child].parent == index)
            {
                hasChildren = true;
                break;
            }
        }
        if (!hasChildren)
        {
            mWriter.append("/>\n");
            return mWriter.good() ? XmlWriteError::none : XmlWriteError::bufferTooSmall;
        }
        mWriter.append(">\n");
        for (std::size_t child = index + 1U; child < mSpecs.size(); ++child)
        {
            if (mSpecs[child].parent != index)
            {
                continue;
            }
            const XmlWriteError result = emitNode(child, depth + 1U);
            if (result != XmlWriteError::none)
            {
                return result;
            }
        }
        indent(mWriter, depth);
        mWriter.append("</object>\n");
        return mWriter.good() ? XmlWriteError::none : XmlWriteError::bufferTooSmall;
    }

    std::span<const ViewSpec> mSpecs;
    BufferWriter& mWriter;
    XmlWriteOptions mOptions;
};

} // namespace

XmlWriteResult XmlLayoutWriter::write(std::span<const ViewSpec> specs,
                                      std::span<char> destination,
                                      XmlWriteOptions options) noexcept
{
    if (!destination.empty())
    {
        destination[0] = '\0';
    }
    if (specs.empty() || destination.empty())
    {
        return {XmlWriteError::empty, 0U};
    }
    std::size_t root = specs.size();
    for (std::size_t index = 0; index < specs.size(); ++index)
    {
        const ViewSpec& spec = specs[index];
        if (spec.frame.empty())
        {
            return {XmlWriteError::invalidHierarchy, 0U};
        }
        if (!spec.parent.has_value())
        {
            if (root != specs.size())
            {
                return {XmlWriteError::invalidHierarchy, 0U};
            }
            root = index;
        }
        else if (spec.parent.value() >= index)
        {
            return {XmlWriteError::invalidHierarchy, 0U};
        }
    }
    if (root == specs.size())
    {
        return {XmlWriteError::invalidHierarchy, 0U};
    }

    BufferWriter writer(destination);
    LayoutEmitter emitter(specs, writer, options);
    const XmlWriteError error = emitter.emit(root);
    return {error, writer.size()};
}

} // namespace lithe

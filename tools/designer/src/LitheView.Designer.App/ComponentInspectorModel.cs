// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using LitheView.Designer.Core.Documents;
using LitheView.Designer.Core.Editing;
using LitheView.Designer.Core.Schema;

namespace LitheView.Designer.App;

internal sealed class ComponentInspectorModel(
    DesignerDocumentSession session,
    WidgetCatalog catalog,
    string componentId,
    Action<FormEditResult> reportEdit) : ICustomTypeDescriptor
{
    [Category("Identity")]
    [DisplayName("Name")]
    public string Name
    {
        get => Component.Name;
        set => reportEdit(session.Rename(componentId, value));
    }

    [Category("Identity")]
    [DisplayName("Component ID")]
    [ReadOnly(true)]
    public string Id => Component.ComponentId;

    [Category("Identity")]
    [DisplayName("Widget")]
    [ReadOnly(true)]
    public string WidgetId => Component.WidgetId;

    [Category("Layout")]
    public int X
    {
        get => Frame.X;
        set => SetFrame(new FormRectangle(value, Frame.Y, Frame.Width, Frame.Height));
    }

    [Category("Layout")]
    public int Y
    {
        get => Frame.Y;
        set => SetFrame(new FormRectangle(Frame.X, value, Frame.Width, Frame.Height));
    }

    [Category("Layout")]
    public int Width
    {
        get => Frame.Width;
        set => SetFrame(new FormRectangle(Frame.X, Frame.Y, value, Frame.Height));
    }

    [Category("Layout")]
    public int Height
    {
        get => Frame.Height;
        set => SetFrame(new FormRectangle(Frame.X, Frame.Y, Frame.Width, value));
    }

    [Category("Content")]
    public string Text
    {
        get => Component.Properties
            .OfType<FormTextValue>()
            .FirstOrDefault(item => item.PropertyId == "text")?.Value ?? string.Empty;
        set => reportEdit(session.SetText(componentId, value ?? string.Empty));
    }

    private FormComponent Component => session.Document.Components.Single(
        item => item.ComponentId == componentId);

    private WidgetDefinition Widget => catalog.Widgets.Single(item => item.Id == Component.WidgetId);

    private FormRectangle Frame => Component.Properties
        .OfType<FormRectangleValue>()
        .Single(item => item.PropertyId == "frame").Value;

    public AttributeCollection GetAttributes()
    {
        return TypeDescriptor.GetAttributes(this, noCustomTypeDesc: true);
    }

    public string? GetClassName()
    {
        return TypeDescriptor.GetClassName(this, noCustomTypeDesc: true);
    }

    public string? GetComponentName()
    {
        return Component.Name;
    }

    public TypeConverter GetConverter()
    {
        return TypeDescriptor.GetConverter(this, noCustomTypeDesc: true);
    }

    public EventDescriptor? GetDefaultEvent()
    {
        return TypeDescriptor.GetDefaultEvent(this, noCustomTypeDesc: true);
    }

    public PropertyDescriptor? GetDefaultProperty()
    {
        return TypeDescriptor.GetDefaultProperty(this, noCustomTypeDesc: true);
    }

    public object? GetEditor(Type editorBaseType)
    {
        return TypeDescriptor.GetEditor(this, editorBaseType, noCustomTypeDesc: true);
    }

    public EventDescriptorCollection GetEvents()
    {
        return TypeDescriptor.GetEvents(this, noCustomTypeDesc: true);
    }

    public EventDescriptorCollection GetEvents(Attribute[]? attributes)
    {
        return TypeDescriptor.GetEvents(this, attributes, noCustomTypeDesc: true);
    }

    public PropertyDescriptorCollection GetProperties()
    {
        return GetProperties(attributes: null);
    }

    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
    {
        bool supportsText = Widget.PropertyIds.Contains("text", StringComparer.Ordinal);
        List<PropertyDescriptor> properties = [.. TypeDescriptor
            .GetProperties(this, attributes, noCustomTypeDesc: true)
            .Cast<PropertyDescriptor>()
            .Where(item => item.Name != nameof(Text) || supportsText)];
        foreach (string propertyId in Widget.PropertyIds.Where(item =>
                     item is not "frame" and not "text"))
        {
            WidgetPropertyDefinition definition = catalog.Properties.Single(item => item.Id == propertyId);
            if (SchemaPropertyDescriptor.IsSupported(definition))
            {
                properties.Add(new SchemaPropertyDescriptor(
                    session,
                    componentId,
                    definition,
                    reportEdit));
            }
        }
        return new PropertyDescriptorCollection([.. properties], readOnly: true);
    }

    public object GetPropertyOwner(PropertyDescriptor? propertyDescriptor)
    {
        return this;
    }

    private void SetFrame(FormRectangle frame)
    {
        reportEdit(session.SetFrame(componentId, frame));
    }

    private sealed class SchemaPropertyDescriptor(
        DesignerDocumentSession session,
        string componentId,
        WidgetPropertyDefinition definition,
        Action<FormEditResult> reportEdit) : PropertyDescriptor(definition.Id, [])
    {
        private readonly DesignerDocumentSession _session = session;
        private readonly string _componentId = componentId;
        private readonly WidgetPropertyDefinition _definition = definition;
        private readonly Action<FormEditResult> _reportEdit = reportEdit;

        public override Type ComponentType => typeof(ComponentInspectorModel);

        public override bool IsReadOnly => false;

        public override Type PropertyType => _definition.Kind switch
        {
            WidgetPropertyKind.Boolean => typeof(bool),
            WidgetPropertyKind.SignedInteger => typeof(long),
            WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration => typeof(ulong),
            _ => typeof(string)
        };

        public override string Category => _definition.Kind switch
        {
            WidgetPropertyKind.Color => "Appearance",
            WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText => "Content",
            WidgetPropertyKind.Boolean or WidgetPropertyKind.Enum => "Behavior",
            WidgetPropertyKind.Bitmap or WidgetPropertyKind.Font => "Resources",
            _ => "Properties"
        };

        public override string Description => _definition.Documentation;

        public override string DisplayName => _definition.DisplayName;

        public override TypeConverter Converter => _definition.Kind switch
        {
            WidgetPropertyKind.Enum => new SchemaEnumConverter(_definition.EnumValues),
            WidgetPropertyKind.Color => new ThemeColorConverter(
                [.. _session.Document.ThemeColors.Select(item => item.Id)]),
            WidgetPropertyKind.Bitmap or WidgetPropertyKind.Font => new ResourceReferenceConverter(
                [.. _session.Document.Resources
                    .Where(item => item.Kind == ResourceKind)
                    .Select(item => item.Id)]),
            _ => base.Converter
        };

        public static bool IsSupported(WidgetPropertyDefinition definition)
        {
            return definition.Kind is WidgetPropertyKind.Boolean or
                WidgetPropertyKind.SignedInteger or
                WidgetPropertyKind.UnsignedInteger or
                WidgetPropertyKind.Enum or
                WidgetPropertyKind.Color or
                WidgetPropertyKind.Text or
                WidgetPropertyKind.BoundedText or
                WidgetPropertyKind.Duration or
                WidgetPropertyKind.Bitmap or
                WidgetPropertyKind.Font;
        }

        private FormResourceKind ResourceKind => _definition.Kind == WidgetPropertyKind.Font
            ? FormResourceKind.Font
            : FormResourceKind.Bitmap;

        public override bool CanResetValue(object component)
        {
            return FindValue() is not null && !_definition.Required;
        }

        public override object? GetValue(object? component)
        {
            return FindValue() switch
            {
                FormBooleanValue value => value.Value,
                FormSignedIntegerValue value => value.Value,
                FormUnsignedIntegerValue value => value.Value,
                FormColorValue { EncodedColor: uint encoded } => $"#{encoded:x8}",
                FormColorValue { ThemeTokenId: string token } => token,
                FormTextValue value => value.Value,
                FormReferenceValue value => value.ReferenceId,
                null => DefaultValue(),
                _ => null
            };
        }

        public override void ResetValue(object component)
        {
            _reportEdit(_session.ResetProperty(_componentId, _definition.Id));
        }

        public override void SetValue(object? component, object? value)
        {
            if (value is null)
            {
                return;
            }
            if (_definition.Kind is WidgetPropertyKind.Bitmap or WidgetPropertyKind.Font &&
                string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)))
            {
                // An empty optional resource clears the reference; a required one stays invalid
                // and is reported by strict document validation rather than silently kept.
                _reportEdit(_session.ResetProperty(_componentId, _definition.Id));
                OnValueChanged(component, EventArgs.Empty);
                return;
            }
            FormPropertyValue replacement = _definition.Kind switch
            {
                WidgetPropertyKind.Boolean => new FormBooleanValue(
                    _definition.Id,
                    Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
                WidgetPropertyKind.SignedInteger => new FormSignedIntegerValue(
                    _definition.Id,
                    Convert.ToInt64(value, CultureInfo.InvariantCulture)),
                WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration =>
                    new FormUnsignedIntegerValue(
                        _definition.Id,
                        Convert.ToUInt64(value, CultureInfo.InvariantCulture)),
                WidgetPropertyKind.Color => ParseColorValue(
                    _definition.Id,
                    Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
                WidgetPropertyKind.Enum or WidgetPropertyKind.Text or WidgetPropertyKind.BoundedText =>
                    new FormTextValue(
                        _definition.Id,
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
                WidgetPropertyKind.Bitmap => new FormReferenceValue(
                    _definition.Id,
                    FormReferenceKind.Bitmap,
                    Convert.ToString(value, CultureInfo.InvariantCulture)!),
                WidgetPropertyKind.Font => new FormReferenceValue(
                    _definition.Id,
                    FormReferenceKind.Font,
                    Convert.ToString(value, CultureInfo.InvariantCulture)!),
                _ => throw new InvalidOperationException($"Property '{_definition.Id}' is not editable.")
            };
            _reportEdit(_session.SetProperty(_componentId, replacement));
            OnValueChanged(component, EventArgs.Empty);
        }

        public override bool ShouldSerializeValue(object component)
        {
            return FindValue() is not null;
        }

        private FormPropertyValue? FindValue()
        {
            return _session.Document.Components.Single(item => item.ComponentId == _componentId)
                .Properties.SingleOrDefault(item => item.PropertyId == _definition.Id);
        }

        private object? DefaultValue()
        {
            JsonElement value = _definition.DefaultValue;
            return _definition.Kind switch
            {
                WidgetPropertyKind.Boolean => value.GetBoolean(),
                WidgetPropertyKind.SignedInteger => value.GetInt64(),
                WidgetPropertyKind.UnsignedInteger or WidgetPropertyKind.Duration => value.GetUInt64(),
                WidgetPropertyKind.Color => value.GetString(),
                _ => value.GetString()
            };
        }

        private static FormColorValue ParseColorValue(string propertyId, string value)
        {
            string text = value.Trim();
            if (text.StartsWith("theme.", StringComparison.Ordinal))
            {
                return new FormColorValue(propertyId, text);
            }
            string digits = text.StartsWith('#') ? text[1..] : text;
            if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                digits = digits[2..];
            }
            uint encoded = uint.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new FormColorValue(propertyId, encoded);
        }
    }

    private sealed class SchemaEnumConverter(string[] values) : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(values);
        }
    }

    // Lists the form's declared resources of one kind. Imported fonts, icons, images, and
    // animation frames become form resources, so assignment never invents a runtime name.
    private sealed class ResourceReferenceConverter(string[] resourceIds) : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(resourceIds.Order(StringComparer.Ordinal).ToArray());
        }
    }

    private sealed class ThemeColorConverter(string[] tokenIds) : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return tokenIds.Length > 0;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(tokenIds.Order(StringComparer.Ordinal).ToArray());
        }
    }

}

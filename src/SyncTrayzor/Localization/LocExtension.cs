using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace SyncTrayzor.Localization
{
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public Binding KeyBinding { get; set; }

        public Binding ValueBinding { get; set; }

        public MultiBinding ValueBindings { get; set; }

        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Key == null && KeyBinding == null)
                throw new ArgumentException("Either Key or KeyBinding must be set");
            if (Key != null && KeyBinding != null)
                throw new ArgumentException("Either Key or KeyBinding must be set, but not both");
            if (ValueBinding != null && ValueBindings != null)
                throw new ArgumentException("ValueBinding and ValueBindings may not be set at the same time");

            // Most of these conditions are redundent, according to the assertions above. However I'll still state them,
            // for clarity.

            // A static key, and no values — bind to UiLanguage so chrome updates when GUI language changes
            if (Key != null && KeyBinding == null && ValueBinding == null && ValueBindings == null)
            {
                var binding = new Binding(nameof(UiLanguage.Revision))
                {
                    Source = UiLanguage.Instance,
                    Mode = BindingMode.OneWay,
                    Converter = new StaticKeyRevisionConverter { Key = Key }
                };
                return binding.ProvideValue(serviceProvider);
            }
            // A static key, and a single value
            if (Key != null && KeyBinding == null && ValueBinding != null && ValueBindings == null)
            {
                var converter = new StaticKeySingleValueConverter() { Key = Key, Converter = ValueBinding.Converter };
                ValueBinding.Converter = converter;
                return ValueBinding.ProvideValue(serviceProvider);
            }
            // A static key, and multiple values
            if (Key != null && KeyBinding == null && ValueBinding == null && ValueBindings != null)
            {
                var converter = new StaticKeyMultipleValuesConverter() { Key = Key, Converter = ValueBindings.Converter };
                ValueBindings.Converter = converter;
                return ValueBindings.ProvideValue(serviceProvider);
            }
            // A bound key, no values
            if (Key == null && KeyBinding != null && ValueBinding == null && ValueBindings == null)
            {
                var converter = new BoundKeyNoValuesConverter() { Converter = KeyBinding.Converter };
                KeyBinding.Converter = converter;
                return KeyBinding.ProvideValue(serviceProvider);
            }
            // A bound key, and one value
            if (Key == null && KeyBinding != null && ValueBinding != null && ValueBindings == null)
            {
                var converter = new BoundKeyWithValuesConverter();
                var multiBinding = new MultiBinding() { Converter = converter };
                multiBinding.Bindings.Add(KeyBinding);
                multiBinding.Bindings.Add(ValueBinding);
                return multiBinding.ProvideValue(serviceProvider);
            }
            // A bound key, and multiple values
            if (Key == null && KeyBinding != null && ValueBinding == null && ValueBindings != null)
            {
                var converter = new BoundKeyWithValuesConverter() { ValuesConverter = ValueBindings.Converter };
                ValueBindings.Bindings.Insert(0, KeyBinding);
                ValueBindings.Converter = converter;
                return ValueBindings.ProvideValue(serviceProvider);
            }

            throw new Exception("Should never get here");
        }
    }
}

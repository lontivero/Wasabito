using System.ComponentModel;
using System.Globalization;
using NBitcoin;

namespace WalletWasabi.TypeConverters;

public class UriConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
	}

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (Uri.TryCreate((string) value, UriKind.Absolute, out var uri))
		{
			return uri;
		}

		throw new ArgumentException($"'{(string)value}' is not a valid absolute URI.");
	}
	
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return destinationType == typeof (string) && value is Uri casted
			? casted.ToString()
			: base.ConvertTo(context, culture, value, destinationType);
	}
}
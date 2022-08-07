using System.ComponentModel;
using System.Globalization;
using NBitcoin;
using WalletWasabi.Userfacing;

namespace WalletWasabi.TypeConverters;

public class EndPointConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
	}

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (EndPointParser.TryParse((string) value, 0, out var ep))
		{
			return ep;
		}

		throw new ArgumentException($"'{(string)value}' is not a valid endpoint.");
	}
	
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return destinationType == typeof (string) && value is Uri casted
			? casted.ToString()
			: base.ConvertTo(context, culture, value, destinationType);
	}
}
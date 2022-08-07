using System.ComponentModel;
using System.Globalization;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.TypeConverters;

public class ExtPubKeyConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
	}

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		return NBitcoinHelpers.BetterParseExtPubKey((string)value);
	}
	
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return destinationType == typeof (string) && value is ExtPubKey casted
			? casted.ToString()
			: base.ConvertTo(context, culture, value, destinationType);
	}
}
using System.ComponentModel;
using System.Globalization;
using NBitcoin;

namespace WalletWasabi.TypeConverters;

public class MoneyConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
	}

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		return decimal.TryParse((string)value, out var casted)
			? Money.Coins(casted)
			: base.ConvertFrom(context, culture, value);
	}
	
	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		return destinationType == typeof (string) && value is Money casted
			? casted.ToDecimal(MoneyUnit.BTC).ToString(CultureInfo.InvariantCulture)
			: base.ConvertTo(context, culture, value, destinationType);
	}
}
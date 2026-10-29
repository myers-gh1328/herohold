namespace Herohold.Rules;
public sealed record EquipmentQuantityLabel(string Name,int Quantity);
public static class EquipmentLabelRules
{
    public static EquipmentQuantityLabel Normalize(string label,IEnumerable<string> trustedNames)
    {
        ArgumentNullException.ThrowIfNull(label);ArgumentNullException.ThrowIfNull(trustedNames);
        if(label.Length>2000)throw new ArgumentException("An equipment source label is too long.");
        var names=trustedNames.Take(50001).ToArray();if(names.Length>50000||names.Any(name=>name is null||name.Length>2000))throw new ArgumentException("Trusted equipment names exceed supported bounds.");
        if(names.Contains(label,StringComparer.Ordinal))return new(label,1);
        var suffix=label.LastIndexOf(" x",StringComparison.Ordinal);if(suffix<=0)return new(label,1);
        var name=label[..suffix];var quantityText=label[(suffix+2)..];
        if(!names.Contains(name,StringComparer.Ordinal)||quantityText.Length==0||quantityText.Any(character=>character is <'0' or >'9'))return new(label,1);
        if(!int.TryParse(quantityText,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var quantity)||quantity is <1 or >100000)throw new ArgumentException("A trusted equipment package quantity must be between 1 and 100000.");
        return new(name,quantity);
    }
}

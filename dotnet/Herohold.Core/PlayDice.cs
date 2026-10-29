using System.Security.Cryptography;

namespace Herohold.Core;

internal static class PlayDice
{
    internal static void ValidateFormula(DiceFormula dice)
    {
        if (dice.Count is < 1 or > 100 || dice.Sides is < 2 or > 100) throw new ArgumentException("Dice require 1–100 dice with 2–100 sides.");
    }

    internal static List<int> Roll(DiceFormula formula, DiceMode mode, IReadOnlyList<int> physical, Func<int, int>? dice)
    {
        ValidateFormula(formula);
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Choose digital or physical dice.");
        if (mode == DiceMode.Physical)
        {
            if (physical.Count != formula.Count || physical.Any(x => x < 1 || x > formula.Sides)) throw new ArgumentException($"Enter exactly {formula.Count} dice, each between 1 and {formula.Sides}.");
            return [.. physical];
        }
        return Enumerable.Range(0, formula.Count).Select(_ =>
        {
            var value = dice?.Invoke(formula.Sides) ?? RandomNumberGenerator.GetInt32(1, formula.Sides + 1);
            if (value < 1 || value > formula.Sides) throw new ArgumentException("The dice source returned an invalid result.");
            return value;
        }).ToList();
    }
}

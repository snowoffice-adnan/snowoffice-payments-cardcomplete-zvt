namespace Snowoffice.Payments.CardCompleteZvt.Models;

public sealed class CardCompleteRegistrationOptions
{
    /// <summary>
    /// ZVT password (numeric).
    /// </summary>
    public int Password { get; set; }

    public CardCompleteLanguage Language { get; set; } = CardCompleteLanguage.English;
}

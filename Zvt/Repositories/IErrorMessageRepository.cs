namespace Snowoffice.Payments.CardCompleteZvt.Zvt.Repositories;

public interface IErrorMessageRepository
{
    string GetMessage(byte key);
}

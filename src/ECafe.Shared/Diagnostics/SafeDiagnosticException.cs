using System.Diagnostics;

namespace ECafe.Shared.Diagnostics;

public sealed class SafeDiagnosticException : Exception
{
    private readonly string trace;

    /// <summary>Yalnız kodda müəyyən edilmiş xəta növünü və parametrsiz çağırış izini saxlayır; mətn, Data və inner exception köçürülmür.</summary>
    private SafeDiagnosticException(Exception error) : base(error.GetType().Name)
        => trace = new StackTrace(error, false).ToString();

    public override string StackTrace => trace;

    /// <summary>Logger-in orijinal exception mətnini, SQL detallarını və daxili xəta obyektlərini serializasiya etməsinin qarşısını alır.</summary>
    public static Exception ForLogging(Exception error)
        => error is SafeDiagnosticException ? error : new SafeDiagnosticException(error);

    /// <summary>Outbox-da sərbəst xəta mətni əvəzinə şəxsi məlumat daşımayan xəta növünü saxlayır.</summary>
    public static string Code(Exception error) => error.GetType().Name;
}

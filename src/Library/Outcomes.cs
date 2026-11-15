namespace Library;

public abstract record Result<T>
{
    private Result() { }
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Error(string Code, string Message) : Result<T>;

    public Result<U> Map<U>(Func<T, U> map) => Bind(value => new Result<U>.Ok(map(value)));

    public Result<U> Bind<U>(Func<T, Result<U>> next) => this switch
    {
        Ok success => next(success.Value),
        Error failure => new Result<U>.Error(failure.Code, failure.Message),
        _ => throw new InvalidOperationException()
    };
}

public abstract record Option<T>
{
    private Option() { }
    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;

    public Result<T> Require(string message) => this switch
    {
        Some found => new Result<T>.Ok(found.Value),
        _ => new Result<T>.Error("not_found", message)
    };
}

public static class Extensions
{
    public static Option<T> Find<T>(this IEnumerable<T> source, Func<T, bool> matches) where T : class
    {
        foreach (var item in source)
            if (matches(item))
                return new Option<T>.Some(item);
        return new Option<T>.None();
    }

    public static bool IsOverdue(this Loan loan, DateOnly date) =>
        loan.ReturnedOn is null && loan.DueOn < date;
}

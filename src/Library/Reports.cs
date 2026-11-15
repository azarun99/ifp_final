namespace Library;

public sealed record OverdueRow(int LoanId, string Member, string Title, int DaysLate);
public sealed record DemandRow(string Category, int Loans, int Titles);

public static class Reports
{
    public static IEnumerable<OverdueRow> OverdueRows(LibraryState state, DateOnly date,
        Action? onScan = null)
    {
        onScan?.Invoke();
        var books = state.Books.ToDictionary(book => book.Id);
        var members = state.Members.ToDictionary(member => member.Id);
        foreach (var loan in state.Loans.Where(loan => loan.IsOverdue(date)))
            yield return new OverdueRow(loan.Id, members[loan.MemberId].Name, books[loan.BookId].Title,
                date.DayNumber - loan.DueOn.DayNumber);
    }

    public static IOrderedEnumerable<OverdueRow> Overdue(LibraryState state, DateOnly date,
        Action? onScan = null) => OverdueRows(state, date, onScan)
            .OrderByDescending(row => row.DaysLate).ThenBy(row => row.LoanId);

    public static IOrderedEnumerable<DemandRow> Demand(LibraryState state, DateOnly from, DateOnly through) =>
        state.Loans.Where(loan => loan.IssuedOn >= from && loan.IssuedOn <= through)
            .Join(state.Books, loan => loan.BookId, book => book.Id, (_, book) => book)
            .GroupBy(book => book.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DemandRow(
                group.Select(book => book.Category).Min(StringComparer.Ordinal)!,
                group.Count(), group.Select(book => book.Id).Distinct().Count()))
            .OrderByDescending(row => row.Loans).ThenBy(row => row.Category, StringComparer.Ordinal);
}
